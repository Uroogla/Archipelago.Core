using Archipelago.Core.Models;
using Archipelago.Core.Util;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core
{
    public class ArchipelagoClient : IDisposable
    {
        private const int SaveLoadTimeoutMs = 5000;
        public bool IsConnected { get; set; }
        public bool IsLoggedIn { get; set; }
        public event EventHandler<ItemReceivedEventArgs> ItemReceived;
        public event EventHandler<ConnectionChangedEventArgs> Disconnected;
        public event EventHandler<ConnectionChangedEventArgs> Connected;
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;
        public ArchipelagoSession CurrentSession { get; set; }
        private List<Location> Locations { get; set; }
        private string GameName { get; set; }
        private string Seed { get; set; }
        private Dictionary<string, object> _options;
        public Dictionary<string, object> Options { get { return _options; } }
        public GameState GameState { get; set; }
        private const int BATCH_SIZE = 25;
        public ArchipelagoClient(IGameClient gameClient)
        {
            Memory.CurrentProcId = gameClient.ProcId;
        }
        public async Task Connect(string host, string gameName)
        {
            try
            {
                CurrentSession = ArchipelagoSessionFactory.CreateSession(host);
                var roomInfo = await CurrentSession.ConnectAsync();
                Seed = roomInfo.SeedName;
                GameName = gameName;

                CurrentSession.Socket.SocketClosed += Socket_SocketClosed;
                IsConnected = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Couldn't connect to Archipelago");
                Console.WriteLine(ex.Message);
            }
        }

        private void Socket_SocketClosed(string reason)
        {
            Console.WriteLine($"Connection Closed: {reason}");
            Disconnect();
        }

        public void Disconnect()
        {
            CurrentSession.Socket.DisconnectAsync();
            CurrentSession.Socket.SocketClosed -= Socket_SocketClosed;
            CurrentSession.MessageLog.OnMessageReceived -= HandleMessageReceived;
            CurrentSession.Items.ItemReceived -= ReceiveItems;
            CurrentSession = null;
            IsConnected = false;
            IsLoggedIn = false;
            Disconnected?.Invoke(this, new ConnectionChangedEventArgs(false));
        }

        public async Task Login(string playerName, string password = null)
        {

            var loginResult = await CurrentSession.LoginAsync(GameName, playerName, ItemsHandlingFlags.AllItems, Version.Parse("5.0.0"), password: password, requestSlotData: true);
            Console.WriteLine($"Login Result: {(loginResult.Successful ? "Success" : "Failed")}");
            if (loginResult.Successful)
            {
                Console.WriteLine($"Connected as Player: {playerName} playing {GameName}");
            }
            else
            {
                Console.WriteLine($"Login failed.");
                return;
            }
            var currentSlot = CurrentSession.ConnectionInfo.Slot;
            var slotData = await CurrentSession.DataStorage.GetSlotDataAsync(currentSlot);
            var optionData = slotData["options"];
            if (optionData != null)
            {
                _options = JsonConvert.DeserializeObject<Dictionary<string, object>>(optionData.ToString());
            }

            IsLoggedIn = true;
            await LoadGameStateAsync();
            AppDomain.CurrentDomain.ProcessExit += async (sender, e) => await SaveGameStateAsync();


            CurrentSession.MessageLog.OnMessageReceived += HandleMessageReceived;
            Connected?.Invoke(this, new ConnectionChangedEventArgs(true));
            InitItemReceiver();
            return;
        }

        private async void HandleMessageReceived(LogMessage message)
        {
            MessageReceived?.Invoke(this, new MessageReceivedEventArgs(message));
        }
        public async void SendGoalCompletion()
        {
            if (IsConnected && IsLoggedIn)
            {
                var update = new StatusUpdatePacket();
                update.Status = ArchipelagoClientState.ClientGoal;
                CurrentSession.Socket.SendPacket(update);
            }
        }

        private void ProcessNewItems(IEnumerable<ItemInfo> newItems, bool notifyReceivedItems = false)
        {
            Console.WriteLine(notifyReceivedItems ? "Item received" : "Checking for offline items received");

            if (!IsConnected) return;

            var existingItems = GameState.ReceivedItems.ToDictionary(x => x.Id, x => x);
            bool newItemFound = false;

            foreach (var newItem in newItems)
            {
                if (existingItems.TryGetValue(newItem.ItemId, out var existingItem))
                {
                    // Item exists, increment quantity
                    existingItem.Quantity++;
                    newItemFound = true;
                }
                else
                {
                    // New item, add it to existing items
                    existingItems[newItem.ItemId] = new Item
                    {
                        Id = newItem.ItemId,
                        Name = newItem.ItemName,
                        Quantity = 1
                    };
                    newItemFound = true;
                }
            }

            if (newItemFound)
            {
                GameState.ReceivedItems = existingItems.Values.ToList();

                if (notifyReceivedItems)
                {
                    // Notify about new or updated items
                    foreach (var item in existingItems.Values)
                    {
                        ItemReceived?.Invoke(this, new ItemReceivedEventArgs() { Item = item });
                    }
                }
            }
        }

        private void InitItemReceiver()
        {
            ProcessNewItems(CurrentSession.Items.AllItemsReceived);
            CurrentSession.Items.ItemReceived += ReceiveItems;
        }

        private void ReceiveItems(ReceivedItemsHelper helper = null)
        {
            ProcessNewItems(CurrentSession.Items.AllItemsReceived, true);
        }
        public async void PopulateLocations(List<Location> locations)
        {
            if (!IsConnected || CurrentSession == null)
            {
                Console.WriteLine("Ensure client is connected before populating locations!");
                return;
            }
            Locations = locations;
            MonitorLocations(Locations);


        }
        public async Task MonitorLocations(List<Location> locations)
        {
            var locationBatches = locations
                .Select((location, index) => new { Location = location, Index = index })
                .GroupBy(x => x.Index / BATCH_SIZE)
                .Select(g => g.Select(x => x.Location).ToList())
                .ToList();
            var tasks = locationBatches.Select(x => MonitorBatch(x));
            await Task.WhenAll(tasks);

        }
        private async Task MonitorBatch(List<Location> batch)
        {

            while (batch.Any(x => CurrentSession.Locations.AllMissingLocations.Contains(x.Id)))
            {
                foreach (var location in batch)
                {
                    if (!CurrentSession.Locations.AllLocationsChecked.Contains(location.Id))
                    {
                        var isCompleted = await Helpers.CheckLocation(location);
                        if (isCompleted) SendLocation(location);
                    }
                }
                await Task.Delay(500);
            }
        }
        public async void SendLocation(Location location)
        {
            if (!(IsConnected))
            {
                Console.WriteLine("Must be connected and logged in to send locations.");
                return;
            }
            await CurrentSession.Locations.CompleteLocationChecksAsync(new[] { (long)location.Id });
            GameState.CompletedLocations.Add(location);
        }

        public async Task SaveGameStateAsync()
        {
            if (!IsConnected || !IsLoggedIn) return;

            try
            {
                var fileName = $"{GameName}_{CurrentSession.ConnectionInfo.Slot}_{Seed}.json";
                var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                $"AP_{GameName}", fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                using (var cts = new CancellationTokenSource(SaveLoadTimeoutMs))
                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                using (var streamWriter = new StreamWriter(fileStream))
                using (var jsonWriter = new JsonTextWriter(streamWriter))
                {
                    var serializer = new JsonSerializer();
                    await Task.Run(() =>
                    {
                        serializer.Serialize(jsonWriter, GameState);
                    }, cts.Token);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not save Archipelago data: {0}", ex.Message);
            }

        }

        public async Task LoadGameStateAsync()
        {
            if (!IsConnected || !IsLoggedIn) return;

            var fileName = $"{GameName}_{CurrentSession.ConnectionInfo.Slot}_{Seed}.json";
            var filePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                $"AP_{GameName}",
                fileName);

            if (File.Exists(filePath))
            {
                try
                {
                    using (var cts = new CancellationTokenSource(SaveLoadTimeoutMs))
                    using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
                    using (var streamReader = new StreamReader(fileStream))
                    using (var jsonReader = new JsonTextReader(streamReader))
                    {
                        var serializer = new JsonSerializer();
                        GameState = await Task.Run(() =>
                        {
                            return serializer.Deserialize<GameState>(jsonReader);
                        }, cts.Token);
                    }
                    if (GameState == null) GameState = new GameState();
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("LoadGameState operation timed out.");
                    GameState = new GameState();
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Cannot load saved data. JSON file is in an unexpected format: {ex.Message}");
                    GameState = new GameState();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading game state: {ex.Message}");
                    GameState = new GameState();
                }
            }
            else
            {
                GameState = new GameState();
            }
        }

        public void Dispose()
        {
            if (IsConnected)
            {
                Disconnect();
            }
        }
    }
}

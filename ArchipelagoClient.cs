using Archipelago.Core.Models;
using Archipelago.Core.Util;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using Newtonsoft.Json;
using Serilog;
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
                Log.Logger.Information("Couldn't connect to Archipelago");
                Log.Logger.Information(ex.Message);
            }
        }

        private void Socket_SocketClosed(string reason)
        {
            Log.Logger.Information($"Connection Closed: {reason}");
            Disconnect();
        }

        public void Disconnect()
        {
            Log.Logger.Information($"Disconnecting...");
            CurrentSession.Socket.DisconnectAsync();
            CurrentSession.Socket.SocketClosed -= Socket_SocketClosed;
            CurrentSession.MessageLog.OnMessageReceived -= HandleMessageReceived;
            CurrentSession.Items.ItemReceived -= (e)=> ReceiveItems();
            CurrentSession = null;
            IsConnected = false;
            IsLoggedIn = false;
            Disconnected?.Invoke(this, new ConnectionChangedEventArgs(false));
            Log.Logger.Information($"Disconnected");
        }

        public async Task Login(string playerName, string password = null)
        {

            var loginResult = await CurrentSession.LoginAsync(GameName, playerName, ItemsHandlingFlags.AllItems, Version.Parse("5.0.0"), password: password, requestSlotData: true);
            Log.Logger.Information($"Login Result: {(loginResult.Successful ? "Success" : "Failed")}");
            if (loginResult.Successful)
            {
                Log.Logger.Information($"Connected as Player: {playerName} playing {GameName}");
            }
            else
            {
                Log.Logger.Information($"Login failed.");                
                return;
            }
            var currentSlot = CurrentSession.ConnectionInfo.Slot;
            var slotData = await CurrentSession.DataStorage.GetSlotDataAsync(currentSlot);
            var optionData = slotData["options"];
            if (optionData != null)
            {
                _options = JsonConvert.DeserializeObject<Dictionary<string, object>>(optionData.ToString());
            }
            Log.Logger.Debug($"Options: \n\t{JsonConvert.SerializeObject(optionData)}");
            IsLoggedIn = true;
            await LoadGameStateAsync();
            AppDomain.CurrentDomain.ProcessExit += async (sender, e) => await SaveGameStateAsync();


            CurrentSession.MessageLog.OnMessageReceived += HandleMessageReceived;
            Connected?.Invoke(this, new ConnectionChangedEventArgs(true));
            CurrentSession.Items.ItemReceived += (e)=> ReceiveItems();
            ReceiveItems();
            return;
        }

        private async void HandleMessageReceived(LogMessage message)
        {
            Log.Logger.Debug($"Message received");
           MessageReceived?.Invoke(this, new MessageReceivedEventArgs(message));
        }
        public async void SendGoalCompletion()
        {
            Log.Logger.Debug($"Sending Goal");
            if (IsConnected && IsLoggedIn)
            {
                var update = new StatusUpdatePacket();
                update.Status = ArchipelagoClientState.ClientGoal;
                CurrentSession.Socket.SendPacket(update);
            }
        }

        private void ReceiveItems()
        {
            Log.Logger.Debug($"Item Received");
            if (!IsConnected) return;

            var existingItems = GameState.ReceivedItems.ToDictionary(x => x.Id, x => x);
            var handledItems = new List<Item>();

            foreach (var newItem in CurrentSession.Items.AllItemsReceived)
            {
                var existingCount = (existingItems.TryGetValue(newItem.ItemId, out var existingItem) ? existingItem.Quantity : 0) + handledItems.Count(x => x.Id == newItem.ItemId);
                var totalReceivedCount = CurrentSession.Items.AllItemsReceived.Count(x => x.ItemId == newItem.ItemId);

                if (existingCount == 0 || existingCount < totalReceivedCount)
                {
                    var item = new Item
                    {
                        Id = newItem.ItemId,
                        Name = newItem.ItemName,
                        Quantity = 1
                    };

                    if (existingCount > 0)
                    {
                        Log.Logger.Debug($"Increasing received quantity");
                       GameState.ReceivedItems.First(x => x.Id == item.Id).Quantity++;
                    }
                    else
                    {
                        Log.Logger.Debug($"Adding to received items list");
                       GameState.ReceivedItems.Add(item);
                    }

                    ItemReceived?.Invoke(this, new ItemReceivedEventArgs() { Item = item });
                    handledItems.Add(item);
                }
                else
                {
                    Log.Logger.Debug($"Item already received");
                }
            }
        }

        public async void PopulateLocations(List<Location> locations)
        {
            if (!IsConnected || CurrentSession == null)
            {
                Log.Logger.Information("Ensure client is connected before populating locations!");
                return;
            }
            Locations = locations;
            Log.Logger.Debug($"Monitoring {locations.Count} locations");

           MonitorLocations(Locations);


        }
        public async Task MonitorLocations(List<Location> locations)
        {
            var locationBatches = locations
                .Select((location, index) => new { Location = location, Index = index })
                .GroupBy(x => x.Index / BATCH_SIZE)
                .Select(g => g.Select(x => x.Location).ToList())
                .ToList();
            Log.Logger.Debug($"Created {locationBatches.Count} batches");

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
                        Log.Logger.Verbose($"Checking location {location.Id}");
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
                Log.Logger.Information("Must be connected and logged in to send locations.");
                return;
            }
            Log.Logger.Debug($"Marking location {location.Id} as complete");

           await CurrentSession.Locations.CompleteLocationChecksAsync(new[] { (long)location.Id });
            GameState.CompletedLocations.Add(location);
        }

        public async Task SaveGameStateAsync()
        {
            if (!IsConnected || !IsLoggedIn) return;
            Log.Logger.Debug($"Saving game state");
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
                Log.Logger.Information("Could not save Archipelago data: {0}", ex.Message);
                Log.Logger.Debug($"{ex.StackTrace}", ex);
            }

        }

        public async Task LoadGameStateAsync()
        {
            if (!IsConnected || !IsLoggedIn) return;
            Log.Logger.Debug($"Loading game state");

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
                    Log.Logger.Information("LoadGameState operation timed out.");
                    GameState = new GameState();
                }
                catch (JsonException ex)
                {
                    Log.Logger.Information($"Cannot load saved data. JSON file is in an unexpected format: {ex.Message}");
                    GameState = new GameState();
                }
                catch (Exception ex)
                {
                    Log.Logger.Information($"Error loading game state: {ex.Message}");
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

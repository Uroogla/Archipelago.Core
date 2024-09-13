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
            LoadGameState();
            AppDomain.CurrentDomain.ProcessExit += (sender, e) => SaveGameState();


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

        private async void InitItemReceiver()
        {
            Console.WriteLine("Checking for offline items received");
            if (IsConnected)
            {
                var existingItems = GameState.ReceivedItems.Select(x => new Item() {Id = x.Id, IsProgression = x.IsProgression, Name = x.Name, Quantity = x.Quantity }).ToList();
                var newItems = CurrentSession.Items.AllItemsReceived;
                bool newItemFound = false;
                foreach (var item in newItems)
                {
                    if (existingItems.Any(x => x.Id == item.ItemId))
                    {
                        existingItems.Remove(existingItems.FirstOrDefault(x => x.Id == item.ItemId));
                    }
                    else
                    {
                        newItemFound = true;
                        break;
                    }
                }
                if (newItemFound)
                {
                    ReceiveItems();
                }
            }

            CurrentSession.Items.ItemReceived += ReceiveItems;

        }
        private async void ReceiveItems(ReceivedItemsHelper helper = null)
        {

            Console.WriteLine("Item received");
            var items = CurrentSession.Items.AllItemsReceived;
            List<ItemInfo> unhandled = new List<ItemInfo>();
            foreach (var thing in items)
            {
                //Have received an item of this type before
                if (GameState.ReceivedItems.Any(x => thing.ItemId == x.Id) || unhandled.Any(x => x.ItemId == thing.ItemId))
                {
                    //There is a new item of this type that hasnt been received yet
                    if (items.Count(x => x.ItemId == thing.ItemId) > (GameState.ReceivedItems.Count(ri => ri.Id == thing.ItemId) + unhandled.Count(x => x.ItemId == thing.ItemId)))
                    {
                        unhandled.Add(thing);
                    }
                }
                else
                {
                    //Havent received any of this item yet
                    unhandled.Add(thing);
                }
            }
            foreach (var item in unhandled)
            {
                var newItem = new Item() { Id = (int)item.ItemId, Quantity = 1, Name = item.ItemName };
                ItemReceived?.Invoke(this, new ItemReceivedEventArgs() { Item = newItem });
                GameState.ReceivedItems.Add(newItem);
            }
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
        private async Task MonitorLocations(List<Location> locations)
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

        public void SaveGameState()
        {
            if (!IsConnected || !IsLoggedIn) return;

            try
            {
                var fileName = $"{GameName}_{CurrentSession.ConnectionInfo.Slot}_{Seed}.json";
                var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                $"AP_{GameName}", fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                string content = JsonConvert.SerializeObject(GameState);
                File.WriteAllText(filePath, content);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not save Archipelago data: {0}", ex.Message);
            }

        }

        public void LoadGameState()
        {
            if (!IsConnected || !IsLoggedIn) return;

            var fileName = $"{GameName}_{CurrentSession.ConnectionInfo.Slot}_{Seed}.json";
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            $"AP_{GameName}", fileName);

            if (File.Exists(filePath))
            {
                string content = File.ReadAllText(filePath);
                try
                {
                    var obj = JsonConvert.DeserializeObject<GameState>(content);
                    GameState = obj;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Cannot load saved data, Json file is in an unexpected format.");
                }
            }
            else GameState = new GameState();

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

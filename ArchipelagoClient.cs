using Archipelago.Core.Models;
using Archipelago.Core.Util;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
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
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core
{
    public class ArchipelagoClient
    {
        public bool IsConnected { get; set; }
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
            }
            catch (Exception ex)
            {
                Console.WriteLine("Couldn't connect to Archipelago");
                Console.WriteLine(ex.Message);
            }
        }

        private void Socket_SocketClosed(string reason)
        {
            Disconnect();
        }

        public void Disconnect()
        {
            CurrentSession.Socket.SocketClosed -= Socket_SocketClosed;
            CurrentSession.MessageLog.OnMessageReceived -= HandleMessageReceived;
            CurrentSession = null;
            IsConnected = false;
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

            IsConnected = true;
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
            var update = new StatusUpdatePacket();
            update.Status = ArchipelagoClientState.ClientGoal;
            CurrentSession.Socket.SendPacket(update);
        }

        public async void InitItemReceiver()
        {
            Console.WriteLine("Checking for offline items received");
            if (IsConnected)
            {
                var existingItems = GameState.ReceivedItems;
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

            CurrentSession.Items.ItemReceived += (helper) =>
            {
                Console.WriteLine("Item received");
                ReceiveItems();
            };
        }
        public async void ReceiveItems()
        {
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
                var flags = item.Flags;
                var isProgression = flags.HasFlag(ItemFlags.Advancement);
                var newItem = new Item() { Id = (int)item.ItemId, Quantity = 1, Name = item.ItemName, IsProgression = isProgression };
                ItemReceived?.Invoke(this, new ItemReceivedEventArgs() { Item = newItem });
                GameState.ReceivedItems.Add(newItem);
            }
        }
        public async void PopulateLocations(List<Location> locations)
        {
            if (!IsConnected)
            {
                Console.WriteLine("Ensure client is connected before populating locations!");
                return;
            }
            Locations = locations;
            MonitorLocations(Locations);


        }
        private async void MonitorLocations(List<Location> locations)
        {
            foreach (var location in Locations)
            {
                if (location.CheckType == LocationCheckType.Bit)
                {
                    Task.Factory.StartNew(async () =>
                    {
                        await Helpers.MonitorAddressBit(location.Name, location.Address, location.AddressBit);
                        SendLocation(location);
                    });

                }
                else if (location.CheckType == LocationCheckType.Int)
                {
                    Task.Factory.StartNew(async () =>
                    {
                        await Helpers.MonitorAddress(location.Address, int.Parse(location.CheckValue), location.CompareType);
                        SendLocation(location);
                    });
                }
                else if (location.CheckType == LocationCheckType.UInt)
                {
                    Task.Factory.StartNew(async () =>
                    {
                        await Helpers.MonitorAddress(location.Address, 4, Convert.ToUInt32(location.CheckValue, 16), location.CompareType);
                        SendLocation(location);
                    });
                }
                else if (location.CheckType == LocationCheckType.Byte)
                {
                    Task.Factory.StartNew(async () =>
                    {
                        await Helpers.MonitorAddress(location.Address, byte.Parse(location.CheckValue), location.CompareType);
                        SendLocation(location);
                    });
                }
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

        private void SaveGameState()
        {
            if (IsConnected)
            {
                var fileName = $"{GameName}_{CurrentSession.ConnectionInfo.Slot}_{Seed}.json";
                var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                $"AP_{GameName}", fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                string content = JsonConvert.SerializeObject(GameState);
                File.WriteAllText(filePath, content);
            }
        }

        private void LoadGameState()
        {
            if (!IsConnected) { return; }

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
    }
}

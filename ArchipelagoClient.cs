using Archipelago.Core.Models;
using Archipelago.Core.Util;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.Json;
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

        private readonly SemaphoreSlim _saveSemaphore = new SemaphoreSlim(1, 1);

        private const int BATCH_SIZE = 25;
        private CancellationTokenSource _cancellationTokenSource { get; set; } = new CancellationTokenSource();
        public ArchipelagoClient(IGameClient gameClient)
        {
            Memory.CurrentProcId = gameClient.ProcId;
            AppDomain.CurrentDomain.ProcessExit += async (sender, e) => await SaveGameStateAsync();
            AppDomain.CurrentDomain.FirstChanceException += async (sender, e) => await SaveGameStateAsync();
            AppDomain.CurrentDomain.UnhandledException += async (sender, e) => await SaveGameStateAsync();
            AppDomain.CurrentDomain.DomainUnload += async (sender, e) => await SaveGameStateAsync();
        }
        public async Task Connect(string host, string gameName)
        {
            Disconnect();
            try
            {
                CurrentSession = ArchipelagoSessionFactory.CreateSession(host);
                var roomInfo = await CurrentSession.ConnectAsync();
                Seed = roomInfo.SeedName;
                GameName = gameName;

                CurrentSession.Socket.SocketClosed += Socket_SocketClosed;
                CurrentSession.MessageLog.OnMessageReceived += HandleMessageReceived;
                CurrentSession.Items.ItemReceived += async (e) => await ReceiveItems();
                IsConnected = true;
            }
            catch (Exception ex)
            {
                Log.Information("Couldn't connect to Archipelago");
                Log.Information(ex.Message);
            }
        }

        private void Socket_SocketClosed(string reason)
        {
            Log.Information($"Connection Closed: {reason}");
            Disconnect();
        }

        public void Disconnect()
        {
            if (CurrentSession != null)
            {
                Log.Information($"Disconnecting...");
                CurrentSession.Socket.DisconnectAsync();
                CurrentSession.Socket.SocketClosed -= Socket_SocketClosed;
                CurrentSession.MessageLog.OnMessageReceived -= HandleMessageReceived;
                CurrentSession.Items.ItemReceived -= async (e) => await ReceiveItems();
                CancelMonitors();
                CurrentSession = null;
            }
            IsConnected = false;
            IsLoggedIn = false;
            Disconnected?.Invoke(this, new ConnectionChangedEventArgs(false));
            Log.Information($"Disconnected");
        }

        public async Task Login(string playerName, string password = null)
        {
            var loginResult = await CurrentSession.LoginAsync(GameName, playerName, ItemsHandlingFlags.AllItems, Version.Parse("5.0.0"), password: password, requestSlotData: true);
            Log.Information($"Login Result: {(loginResult.Successful ? "Success" : "Failed")}");
            if (loginResult.Successful)
            {
                Log.Information($"Connected as Player: {playerName} playing {GameName}");
            }
            else
            {
                Log.Information($"Login failed.");
                return;
            }
            var currentSlot = CurrentSession.ConnectionInfo.Slot;
            var slotData = await CurrentSession.DataStorage.GetSlotDataAsync(currentSlot);
            Log.Information("Loading Options.");
            if (slotData.ContainsKey("options"))
            {
                var optionData = slotData["options"];
                if (optionData != null)
                {

                    _options = JsonSerializer.Deserialize<Dictionary<string, object>>(optionData.ToString());
                }
                Log.Debug($"Options: \n\t{JsonSerializer.Serialize(optionData)}");
            }
            else
            {
                Log.Information("No options found.");
            }
            IsLoggedIn = true;

            Connected?.Invoke(this, new ConnectionChangedEventArgs(true));

            await ReceiveItems();

            await SaveGameStateAsync();
            return;
        }
        public async void SendMessage(string message)
        {
            await CurrentSession.Socket.SendPacketAsync(new SayPacket() { Text = message });

        }
        private async void HandleMessageReceived(LogMessage message)
        {
            Log.Debug($"Message received");
            MessageReceived?.Invoke(this, new MessageReceivedEventArgs(message));
        }
        public async void SendGoalCompletion()
        {
            Log.Debug($"Sending Goal");
            if (IsConnected && IsLoggedIn)
            {
                try
                {
                    var update = new StatusUpdatePacket();
                    update.Status = ArchipelagoClientState.ClientGoal;
                    CurrentSession.Socket.SendPacket(update);
                }
                catch (Exception ex)
                {
                    Log.Error($"Could not send goal: {ex.Message}");
                }
            }
            else
            {
                Log.Error("Could not send goal: Not connected");
            }
        }
        public async void CancelMonitors()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
        }
        private async Task ReceiveItems()
        {
            Log.Debug($"Item Received");
            if (!IsConnected) return;

            if (GameState == null)
            {
                await LoadGameStateAsync();
            }
            var newItemInfo = CurrentSession.Items.PeekItem();
            while (newItemInfo != null)
            {
                var item = new Item
                {
                    Id = newItemInfo.ItemId,
                    Name = newItemInfo.ItemName,
                    Quantity = 1
                };

                var existingItem = GameState.ReceivedItems.FirstOrDefault(x => x.Id == item.Id);
                var totalReceivedCount = CurrentSession.Items.AllItemsReceived.Count(z => z.ItemId == item.Id);

                if (existingItem != null)
                {
                    var currentQuantity = GameState.ReceivedItems.Where(x => x.Id == item.Id).Sum(y => y.Quantity);
                    if (currentQuantity < totalReceivedCount)
                    {
                        Log.Debug($"Increasing received quantity");
                        existingItem.Quantity++;
                        ItemReceived?.Invoke(this, new ItemReceivedEventArgs() { Item = item });
                    }
                }
                else
                {
                    Log.Debug($"Adding to received items list");
                    // Item doesn't exist yet, add it
                    GameState.ReceivedItems.Add(item);
                    ItemReceived?.Invoke(this, new ItemReceivedEventArgs() { Item = item });
                }

                CurrentSession.Items.DequeueItem();
                newItemInfo = CurrentSession.Items.PeekItem();
            }
            await SaveGameStateAsync();
        }
        public async Task PopulateLocations(List<Location> locations)
        {
            if (!IsConnected || CurrentSession == null)
            {
                Log.Information("Ensure client is connected before populating locations!");
                return;
            }
            Locations = locations;
            Log.Debug($"Monitoring {locations.Count} locations");
            MonitorLocations(Locations);

            return;
        }
        public async Task MonitorLocations(List<Location> locations)
        {
            var locationBatches = locations
                .Select((location, index) => new { Location = location, Index = index })
                .GroupBy(x => x.Index / BATCH_SIZE)
                .Select(g => g.Select(x => x.Location).ToList())
                .ToList();
            Log.Debug($"Created {locationBatches.Count} batches");

            var tasks = locationBatches.Select(x => MonitorBatch(x, _cancellationTokenSource.Token));
            await Task.WhenAll(tasks);

        }
        private async Task MonitorBatch(List<Location> batch, CancellationToken token)
        {
            List<Location> completed = new List<Location>();

            while (!batch.All(x => completed.Any(y => y.Id == x.Id)))
            {
                if (token.IsCancellationRequested) return;
                foreach (var location in batch)
                {
                    var isCompleted = await Helpers.CheckLocation(location);
                    if (isCompleted)
                    {
                        completed.Add(location);
                        //  Log.Logger.Information(JsonConvert.SerializeObject(location));
                    }
                }
                if (completed.Any())
                {
                    foreach (var location in completed)
                    {
                        SendLocation(location);
                        Log.Information($"{location.Name} ({location.Id}) Completed");
                        batch.Remove(location);
                    }
                }
                completed.Clear();
                await Task.Delay(500);
            }
        }
        public async void SendLocation(Location location)
        {
            if (!(IsConnected))
            {
                Log.Information("Must be connected and logged in to send locations.");
                return;
            }
            Log.Debug($"Marking location {location.Id} as complete");

            await CurrentSession.Locations.CompleteLocationChecksAsync(new[] { (long)location.Id });
            GameState.CompletedLocations.Add(location);
        }

        public async Task SaveGameStateAsync()
        {

            Log.Debug($"Saving game state");

            if (!await _saveSemaphore.WaitAsync(TimeSpan.FromMilliseconds(100)))
            {
                Log.Debug("Save operation already in progress");
                return;
            }

            try
            {
                var fileName = $"{GameName}_{CurrentSession.ConnectionInfo.Slot}_{Seed}.json";
                var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                $"AP_{GameName}", fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                using (var cts = new CancellationTokenSource(SaveLoadTimeoutMs))
                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                    await Task.Run(() =>
                    {
                        JsonSerializer.Serialize(fileStream, GameState);
                    }, cts.Token);

            }
            catch (Exception ex)
            {
                Log.Information("Could not save Archipelago data: {0}", ex.Message);
                Log.Debug($"{ex.StackTrace}", ex);
            }
            finally
            {
                _saveSemaphore.Release();
            }
        }

        public async Task LoadGameStateAsync()
        {
            Log.Debug($"Loading game state");

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
                    using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
                        GameState = await Task.Run(() =>
                        {
                            return JsonSerializer.Deserialize<GameState>(fileStream);
                        }, cts.Token);

                    if (GameState == null) GameState = new GameState();
                }
                catch (OperationCanceledException)
                {
                    Log.Information("LoadGameState operation timed out.");
                    GameState = new GameState();
                }
                catch (JsonException ex)
                {
                    Log.Information($"Cannot load saved data. JSON file is in an unexpected format: {ex.Message}");
                    GameState = new GameState();
                }
                catch (Exception ex)
                {
                    Log.Information($"Error loading game state: {ex.Message}");
                    GameState = new GameState();
                }
            }
            else
            {
                GameState = new GameState();
            }
        }
        public DeathLinkService EnableDeathLink()
        {
            var service = CurrentSession.CreateDeathLinkService();
            service.EnableDeathLink();
            return service;
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

using Archipelago.Core.Models;
using Archipelago.Core.Util;
using Archipelago.Core.Util.Overlay;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Packets;
using Serilog;
using System.Text.Json;

namespace Archipelago.Core
{
    public class ArchipelagoClient : IDisposable
    {
        private const int SaveLoadTimeoutMs = 5000;
        public bool IsConnected { get; set; }
        public bool IsLoggedIn { get; set; }
        public event EventHandler<ItemReceivedEventArgs>? ItemReceived;
        public event EventHandler<ConnectionChangedEventArgs>? Disconnected;
        public event EventHandler<ConnectionChangedEventArgs>? Connected;
        public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
        public ArchipelagoSession CurrentSession { get; set; }
        private List<Location> Locations { get; set; } = [];
        private string GameName { get; set; } = "";
        private string Seed { get; set; } = "";
        private Dictionary<string, object> _options = [];
        public Dictionary<string, object> Options { get { return _options; } }
        public GameState GameState { get; set; }
        private IOverlayService? OverlayService { get; set; }

        private readonly SemaphoreSlim _fileOperationSemaphore = new(1, 1);
        private bool isOverlayEnabled = false;

        private const int BATCH_SIZE = 25;
        private readonly object _lockObject = new object();
        private CancellationTokenSource _cancellationTokenSource { get; set; } = new CancellationTokenSource();
        public ArchipelagoClient(IGameClient gameClient)
        {
            Memory.CurrentProcId = gameClient.ProcId;
            AppDomain.CurrentDomain.ProcessExit += async (sender, e) => await SaveGameStateAsync();
        }
        public void IntializeOverlayService(IOverlayService overlayService)
        {
            OverlayService = overlayService;
            OverlayService.AttachToWindow(Memory.GetCurrentProcess().MainWindowHandle);
            OverlayService.Show();
            isOverlayEnabled = true;
        }
        public async Task Connect(string host, string gameName, CancellationToken cancellationToken = default)
        {
            cancellationToken = CombineTokens(cancellationToken);
            Disconnect();
            try
            {
                CurrentSession = ArchipelagoSessionFactory.CreateSession(host);
                var roomInfo = await CurrentSession.ConnectAsync();
                Seed = roomInfo.SeedName;
                GameName = gameName;

                CurrentSession.Socket.SocketClosed += Socket_SocketClosed;
                CurrentSession.MessageLog.OnMessageReceived += HandleMessageReceived;
                CurrentSession.Items.ItemReceived += ItemReceivedHandler;
                IsConnected = true;
            }
            catch (Exception ex)
            {
                Log.Information("Couldn't connect to Archipelago");
                Log.Information(ex.Message);
            }
        }

        private async void ItemReceivedHandler(ReceivedItemsHelper helper)
        {
            await ReceiveItems(_cancellationTokenSource.Token);
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
                CurrentSession.Items.ItemReceived -= ItemReceivedHandler;
                CancelMonitors();
                GameState = null;
                CurrentSession = null;
            }
            IsConnected = false;
            IsLoggedIn = false;
            Disconnected?.Invoke(this, new ConnectionChangedEventArgs(false));
            Log.Information($"Disconnected");
        }

        public async Task Login(string playerName, string password = null, CancellationToken cancellationToken = default)
        {
            cancellationToken = CombineTokens(cancellationToken);
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
            if (slotData.TryGetValue("options", out object? optionData))
            {
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
            await LoadGameStateAsync(cancellationToken);
            await ReceiveItems(cancellationToken);

            return;
        }
        public async void SendMessage(string message, CancellationToken cancellationToken = default)
        {
            cancellationToken = CombineTokens(cancellationToken);
            await CurrentSession.Socket.SendPacketAsync(new SayPacket() { Text = message });

        }
        private void HandleMessageReceived(LogMessage message)
        {
            Log.Debug($"Message received");
            MessageReceived?.Invoke(this, new MessageReceivedEventArgs(message));
        }
        public void SendGoalCompletion()
        {
            Log.Debug($"Sending Goal");

            try
            {
                var update = new StatusUpdatePacket
                {
                    Status = ArchipelagoClientState.ClientGoal
                };
                CurrentSession.Socket.SendPacket(update);
            }
            catch (Exception ex)
            {
                Log.Error($"Could not send goal: {ex.Message}");
            }
        }
        public void CancelMonitors()
        {
            var previousToken = _cancellationTokenSource;
            _cancellationTokenSource = new CancellationTokenSource();
            previousToken.Cancel();
            previousToken.Dispose();
        }
        private async Task ReceiveItems(CancellationToken cancellationToken = default)
        {
            cancellationToken = CombineTokens(cancellationToken);

            await LoadGameStateAsync(cancellationToken);

            var newItemInfo = CurrentSession.Items.PeekItem();
            var itemsToAdd = new List<Item>();
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
                        Log.Debug($"Increasing received quantity for {item.Name} from {currentQuantity} to {totalReceivedCount}");
                        existingItem.Quantity = totalReceivedCount;
                        ItemReceived?.Invoke(this, new ItemReceivedEventArgs() { Item = item });
                    }
                }
                else if (totalReceivedCount > 0)
                {
                    Log.Debug($"Adding new item {item.Name} with quantity {totalReceivedCount}");
                    item.Quantity = totalReceivedCount;
                    itemsToAdd.Add(item);
                }

                CurrentSession.Items.DequeueItem();
                newItemInfo = CurrentSession.Items.PeekItem();
            }
            foreach (var item in itemsToAdd)
            {
                GameState.ReceivedItems.Add(item);
                ItemReceived?.Invoke(this, new ItemReceivedEventArgs() { Item = item });
            }
            if (!cancellationToken.IsCancellationRequested)
            {
                await SaveGameStateAsync(cancellationToken);
            }

        }
        [Obsolete("PopulateLocations is now deprecated, please use MonitorLocations instead")]
        public async Task PopulateLocations(List<Location> locations, CancellationToken cancellationToken = default)
        {
            cancellationToken = CombineTokens(cancellationToken);
            if (!IsConnected || CurrentSession == null)
            {
                Log.Information("Ensure client is connected before populating locations!");
                return;
            }
            Locations = locations;
            Log.Debug($"Monitoring {locations.Count} locations");
            await MonitorLocations(Locations, cancellationToken);

            return;
        }
        public async Task MonitorLocations(List<Location> locations, CancellationToken cancellationToken = default)
        {
            cancellationToken = CombineTokens(cancellationToken);
            var locationBatches = locations
                .Select((location, index) => new { Location = location, Index = index })
                .GroupBy(x => x.Index / BATCH_SIZE)
                .Select(g => g.Select(x => x.Location).ToList())
                .ToList();
            Log.Debug($"Created {locationBatches.Count} batches");

            var tasks = locationBatches.Select(x => MonitorBatch(x, _cancellationTokenSource.Token));
            await Task.WhenAll(tasks);

        }
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public void AddOverlayMessage(string message, TimeSpan? duration = null, CancellationToken cancellationToken = default)
        {
            if (isOverlayEnabled)
            {
                cancellationToken = CombineTokens(cancellationToken);
                if (!duration.HasValue) duration = TimeSpan.FromSeconds(5);
                OverlayService.AddTextPopup(message, new Util.Overlay.Color(0, 0, 0), duration.Value.TotalSeconds);
            }
        }
        private async Task MonitorBatch(List<Location> batch, CancellationToken token)
        {
            List<Location> completed = [];

            while (!batch.All(x => completed.Any(y => y.Id == x.Id)))
            {
                if (token.IsCancellationRequested) return;
                foreach (var location in batch)
                {
                    var isCompleted = Helpers.CheckLocation(location);
                    if (isCompleted)
                    {
                        completed.Add(location);
                        //  Log.Logger.Information(JsonConvert.SerializeObject(location));
                    }
                }
                if (completed.Count > 0)
                {
                    foreach (var location in completed)
                    {
                        SendLocation(location, token);
                        Log.Information($"{location.Name} ({location.Id}) Completed");
                        batch.Remove(location);
                    }
                }
                completed.Clear();
                await Task.Delay(500, token);
            }
        }
        public async void SendLocation(Location location, CancellationToken cancellationToken = default)
        {
            cancellationToken = CombineTokens(cancellationToken);
            if (CurrentSession == null)
            {
                Log.Information("Must be connected and logged in to send locations.");
                return;
            }
            Log.Debug($"Marking location {location.Id} as complete");

            await CurrentSession.Locations.CompleteLocationChecksAsync([(long)location.Id]);
            GameState.CompletedLocations.Add(location);
        }

        public async Task SaveGameStateAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken = CombineTokens(cancellationToken);
            if (CurrentSession == null) return;
            Log.Debug($"Saving game state");

            await _fileOperationSemaphore.WaitAsync(cancellationToken);

            try
            {
                var fileName = $"{GameName}_{CurrentSession.ConnectionInfo.Slot}_{Seed}.json";
                var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                $"AP_{GameName}", fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                using var cts = new CancellationTokenSource(SaveLoadTimeoutMs);
                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                await Task.Run(async () =>
                {
                    await JsonSerializer.SerializeAsync(fileStream, GameState);
                }, cts.Token);
            }
            catch (Exception ex)
            {
                Log.Information("Could not save Archipelago data: {0}", ex.Message);
                Log.Debug($"{ex.StackTrace}", ex);
            }
            finally
            {
                _fileOperationSemaphore.Release();
            }
        }

        public async Task LoadGameStateAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken = CombineTokens(cancellationToken);
            Log.Debug($"Loading game state");

            await _fileOperationSemaphore.WaitAsync(cancellationToken);

            try
            {
                var fileName = $"{GameName}_{CurrentSession.ConnectionInfo.Slot}_{Seed}.json";
                var filePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    $"AP_{GameName}",
                    fileName);

                if (File.Exists(filePath))
                {
                    try
                    {
                        using (var cts = new CancellationTokenSource(SaveLoadTimeoutMs))
                        using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
                            GameState = await Task.Run(async () =>
                            {
                                return await JsonSerializer.DeserializeAsync<GameState>(fileStream);
                            }, cts.Token);

                        GameState ??= new GameState();
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
                        Log.Error($"Error loading game state: {ex.Message}");
                        GameState = new GameState();
                    }
                }
                else
                {
                    Log.Warning($"Cannot load saved data. JSON file does not exist");
                    GameState = new GameState();
                }
            }
            finally
            {
                _fileOperationSemaphore.Release();
            }

            Log.Verbose($"Finished loading game state");
        }
        private CancellationToken CombineTokens(CancellationToken externalToken)
        {
            if (externalToken == default || externalToken == CancellationToken.None)
            {
                return _cancellationTokenSource.Token;
            }

            return CancellationTokenSource.CreateLinkedTokenSource(
                _cancellationTokenSource.Token,
                externalToken
            ).Token;
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
            OverlayService.Hide();
            OverlayService.Dispose();
            _cancellationTokenSource.Dispose();
            _fileOperationSemaphore.Dispose();

        }
    }
}

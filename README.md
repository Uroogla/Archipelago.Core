# Archipelago.Core Documentation

A comprehensive C# library for integrating games with the Archipelago multiworld randomizer system, providing memory reading/writing capabilities, location monitoring, item handling, and overlay services.

## Table of Contents
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Core Concepts](#core-concepts)
- [API Reference](#api-reference)
- [Advanced Features](#advanced-features)
- [Examples](#examples)
- [Troubleshooting](#troubleshooting)

## Installation

### Package Manager
```
Install-Package Archipelago.Core
Install-Package Archipelago.Core.MauiGUI  # Optional GUI components
```

### .NET CLI
```
dotnet add package Archipelago.Core
dotnet add package Archipelago.Core.MauiGUI  # Optional GUI components
```

## Quick Start

### Basic Setup

```csharp
using Archipelago.Core;
using Archipelago.Core.Models;

// Create a game client for your target process
var gameClient = new GenericGameClient("YourGame.exe");
gameClient.Connect();

// Initialize the Archipelago client
var archipelagoClient = new ArchipelagoClient(gameClient);

// Connect to Archipelago server
await archipelagoClient.Connect("archipelago.gg:38281", "YourGameName");

// Login with player credentials
await archipelagoClient.Login("PlayerName", "password");
```

### Event Handling

```csharp
// Handle received items
archipelagoClient.ItemReceived += (sender, args) => 
{
    Console.WriteLine($"Received item: {args.Item.Name}");
    // Add your game-specific item handling logic here
};

// Handle location completions
archipelagoClient.LocationCompleted += (sender, args) => 
{
    Console.WriteLine($"Location completed: {args.Location.Name}");
};

// Handle connection changes
archipelagoClient.Connected += (sender, args) => 
{
    Console.WriteLine("Connected to Archipelago!");
};

archipelagoClient.Disconnected += (sender, args) => 
{
    Console.WriteLine("Disconnected from Archipelago");
};
```

## Core Concepts

### Game Client
The `IGameClient` interface represents your connection to the target game process. Use `GenericGameClient` for most scenarios:

```csharp
var gameClient = new GenericGameClient("MyGame.exe");
```

### Memory Operations
The `Memory` class provides cross-platform memory reading and writing capabilities:

```csharp
// Read different data types
byte value = Memory.ReadByte(0x12345678);
int intValue = Memory.ReadInt(0x12345678);
float floatValue = Memory.ReadFloat(0x12345678);

// Write values
Memory.WriteByte(0x12345678, 255);
Memory.Write(0x12345678, 12345);

// Read/write with endianness control
int bigEndianValue = Memory.ReadInt(0x12345678, Endianness.Big);
```

### Locations
Locations represent checkable objectives in your game. Create locations using the `Location` class:

```csharp
var locations = new List<ILocation>
{
    new Location
    {
        Id = 1001,
        Name = "First Chest",
        Address = 0x12345678,
        CheckType = LocationCheckType.Bit,
        AddressBit = 0,
        Category = "Chests"
    },
    new Location
    {
        Id = 1002,
        Name = "Boss Defeated",
        Address = 0x87654321,
        CheckType = LocationCheckType.Byte,
        CheckValue = "1",
        CompareType = LocationCheckCompareType.Match,
        Category = "Bosses"
    }
};

// Start monitoring locations
await archipelagoClient.MonitorLocations(locations);
```

### Location Check Types

- **Bit**: Check if a specific bit is set
- **Byte/Short/Int/Long/UInt**: Check numeric values
- **FalseBit**: Check if a bit is NOT set
- **Nibble**: Check 4-bit values (upper or lower nibble)
- **AND/OR**: Composite locations with multiple conditions

### Composite Locations
Create complex location conditions using `CompositeLocation`:

```csharp
var compositeLocation = new CompositeLocation
{
    Id = 2001,
    Name = "Complex Achievement",
    CheckType = LocationCheckType.AND,
    Conditions = new List<ILocation>
    {
        new Location { /* condition 1 */ },
        new Location { /* condition 2 */ }
    }
};
```

## API Reference

### ArchipelagoClient

#### Properties
- `IsConnected`: Whether connected to Archipelago server
- `IsLoggedIn`: Whether logged in as a player
- `CurrentSession`: The active Archipelago session
- `Options`: Game-specific options from slot data
- `GameState`: Current game state (items, locations)
- `CustomValues`: Custom data storage

#### Methods

**Connection Methods**
```csharp
Task Connect(string host, string gameName, CancellationToken cancellationToken = default)
Task Login(string playerName, string password = null, CancellationToken cancellationToken = default)
void Disconnect()
```

**Location Methods**
```csharp
Task MonitorLocations(List<ILocation> locations, CancellationToken cancellationToken = default)
void SendLocation(ILocation location, CancellationToken cancellationToken = default)
```

**Item Methods**
```csharp
void ForceReloadAllItems()  // Clears received items and re-processes from server
```

**Communication Methods**
```csharp
void SendMessage(string message, CancellationToken cancellationToken = default)
void SendGoalCompletion()  // Notify server of goal completion
```

**Advanced Features**
```csharp
DeathLinkService EnableDeathLink()  // Enable death synchronization
void IntializeOverlayService(IOverlayService overlayService)  // Add overlay support
void AddOverlayMessage(string message, CancellationToken cancellationToken = default)
```

#### Events
```csharp
event EventHandler<ItemReceivedEventArgs> ItemReceived
event EventHandler<ConnectionChangedEventArgs> Connected
event EventHandler<ConnectionChangedEventArgs> Disconnected
event EventHandler<MessageReceivedEventArgs> MessageReceived
event EventHandler<LocationCompletedEventArgs> LocationCompleted
```

### Memory Class

#### Read Operations
```csharp
static byte ReadByte(ulong address)
static short ReadShort(ulong address, Endianness endianness = Endianness.Little)
static int ReadInt(ulong address, Endianness endianness = Endianness.Little)
static float ReadFloat(ulong address, Endianness endianness = Endianness.Little)
static string ReadString(ulong address, int length, Endianness endianness = Endianness.Little)
static bool ReadBit(ulong address, int bitNumber, Endianness endianness = Endianness.Little)

// Generic read method
static T Read<T>(ulong address, Endianness endianness) where T : struct

// Object mapping
static T ReadObject<T>(ulong baseAddress, Endianness endianness = Endianness.Little) where T : class, new()
```

#### Write Operations
```csharp
static bool WriteByte(ulong address, byte value)
static bool Write(ulong address, int value, Endianness endianness = Endianness.Little)
static bool WriteString(ulong address, string value, Endianness endianness = Endianness.Little)
static bool WriteBit(ulong address, int bitNumber, bool value, Endianness endianness = Endianness.Little)

// Object mapping
static bool WriteObject<T>(ulong baseAddress, T obj, Endianness endianness = Endianness.Little) where T : class
```

#### Utility Methods
```csharp
static ulong GetBaseAddress(string modName)
static Process GetCurrentProcess()
static Task MonitorAddressForAction<T>(ulong address, Action action, Func<T, bool> criteria)
```

### Object Mapping with Attributes

Use the `MemoryOffsetAttribute` to map C# classes to memory structures:

```csharp
[MemoryOffset(0x100)]  // Class-level offset
public class PlayerData
{
    [MemoryOffset(0x00)]
    public int Health { get; set; }
    
    [MemoryOffset(0x04)]
    public int Mana { get; set; }
    
    [MemoryOffset(0x08, StringLength = 32)]
    public string Name { get; set; }
    
    [MemoryOffset(0x28, CollectionLength = 10)]
    public List<int> Inventory { get; set; }
}

// Usage
var playerData = Memory.ReadObject<PlayerData>(baseAddress);
Memory.WriteObject(baseAddress, playerData);
```

## Advanced Features

### GPS Handler
Track player position and map changes:

```csharp
archipelagoClient.GPSHandler = new GPSHandler();
archipelagoClient.GPSHandler.PositionChanged += (sender, args) => 
{
    Console.WriteLine($"Position: {args.X}, {args.Y}");
};
```

### Overlay Service
Display messages over the game window:

```csharp
var overlayService = new WindowsOverlayService(new OverlayOptions
{
    FontSize = 16,
    XOffset = 50,
    YOffset = 50,
    FadeDuration = 5.0f
});

archipelagoClient.IntializeOverlayService(overlayService);
archipelagoClient.AddOverlayMessage("Item received!");
```

### Function Hooking
Hook game functions for advanced integration:

```csharp
var hook = new FunctionHook(functionAddress, context => 
{
    // Your hook logic here
    Console.WriteLine("Function called!");
    return true; // Execute original function
});

hook.Install();
```

### Death Link
Synchronize deaths between players:

```csharp
var deathLink = archipelagoClient.EnableDeathLink();
deathLink.OnDeathLinkReceived += (sender, args) => 
{
    // Handle death from another player
    KillPlayer();
};
```

### Custom Data Storage
Store and retrieve custom data:

```csharp
// Store data
archipelagoClient.CustomValues["MyKey"] = "MyValue";
await archipelagoClient.SaveGameStateAsync();

// Retrieve data
if (archipelagoClient.CustomValues.TryGetValue("MyKey", out var value))
{
    Console.WriteLine(value);
}
```

## Examples

### Complete Integration Example

```csharp
public class MyGameArchipelagoIntegration
{
    private ArchipelagoClient _client;
    private List<ILocation> _locations;
    
    public async Task Initialize()
    {
        // Setup game client
        var gameClient = new GenericGameClient("MyGame.exe");
        if (!gameClient.Connect())
        {
            throw new Exception("Could not connect to game");
        }
        
        // Initialize Archipelago client
        _client = new ArchipelagoClient(gameClient);
        
        // Setup event handlers
        _client.ItemReceived += OnItemReceived;
        _client.LocationCompleted += OnLocationCompleted;
        _client.Connected += OnConnected;
        
        // Connect and login
        await _client.Connect("archipelago.gg:38281", "My Game");
        await _client.Login("PlayerName");
    }
    
    private void OnItemReceived(object sender, ItemReceivedEventArgs e)
    {
        switch (e.Item.Name)
        {
            case "Sword":
                GivePlayerSword();
                break;
            case "Key":
                GivePlayerKey();
                break;
            default:
                Console.WriteLine($"Unknown item: {e.Item.Name}");
                break;
        }
    }
    
    private void OnLocationCompleted(object sender, LocationCompletedEventArgs e)
    {
        _client.AddOverlayMessage($"Found: {e.Location.Name}");
    }
    
    private async void OnConnected(object sender, ConnectionChangedEventArgs e)
    {
        // Load locations and start monitoring
        _locations = LoadLocationsFromFile();
        await _client.MonitorLocations(_locations);
        
        // Check game options
        if (_client.Options.TryGetValue("difficulty", out var difficulty))
        {
            SetGameDifficulty(difficulty.ToString());
        }
    }
    
    private void GivePlayerSword()
    {
        // Write to game memory to give player a sword
        Memory.WriteByte(0x12345678, 1); // Has sword flag
    }
}
```

### Location Definition Example

```csharp
private List<ILocation> CreateGameLocations()
{
    return new List<ILocation>
    {
        // Simple bit check
        new Location
        {
            Id = 1001,
            Name = "Treasure Chest 1",
            Address = 0x200000,
            CheckType = LocationCheckType.Bit,
            AddressBit = 0,
            Category = "Chests"
        },
        
        // Numeric value check
        new Location
        {
            Id = 1002,
            Name = "Boss 1 Defeated",
            Address = 0x200010,
            CheckType = LocationCheckType.Byte,
            CheckValue = "1",
            CompareType = LocationCheckCompareType.GreaterThan,
            Category = "Bosses"
        },
        
        // Range check
        new Location
        {
            Id = 1003,
            Name = "Level 10-15 Achievement",
            Address = 0x200020,
            CheckType = LocationCheckType.Int,
            CompareType = LocationCheckCompareType.Range,
            RangeStartValue = "10",
            RangeEndValue = "15",
            Category = "Achievements"
        },
        
        // Composite location (AND condition)
        new CompositeLocation
        {
            Id = 1004,
            Name = "Sword and Shield Collected",
            CheckType = LocationCheckType.AND,
            Category = "Equipment",
            Conditions = new List<ILocation>
            {
                new Location
                {
                    Id = 1005,
                    Name = "Has Sword",
                    Address = 0x200030,
                    CheckType = LocationCheckType.Bit,
                    AddressBit = 0
                },
                new Location
                {
                    Id = 1006,
                    Name = "Has Shield", 
                    Address = 0x200030,
                    CheckType = LocationCheckType.Bit,
                    AddressBit = 1
                }
            }
        }
    };
}
```

## Troubleshooting

### Common Issues

**Connection Problems**
- Ensure the Archipelago server is running and accessible
- Check firewall settings
- Verify the correct host and port

**Memory Reading Issues**
- Run as administrator for memory access permissions
- Ensure the target process is running
- Check that addresses are correct for your game version

**Location Not Triggering**
- Verify memory addresses using a memory scanner
- Check that the condition logic matches your game's behavior
- Use logging to debug location check values

**Items Not Processing**
- Check that your item handling code is working correctly
- Verify game state is saving/loading properly

### Performance Tips

- Use batched location monitoring for better performance
- Implement conditional location checking with `EnableLocationsCondition`
- Avoid frequent memory writes in tight loops
- Use overlay messages sparingly to prevent spam

### Debugging

Monitor memory addresses:
```csharp
var task = Memory.MonitorAddressForAction<int>(address, 
    () => Console.WriteLine("Value changed!"),
    value => value > 0);
```

## License

This library is provided under the MIT License. See LICENSE file for details.

## Contributing

Contributions are welcome! Please submit issues and pull requests on the project repository.

# Guide to Using Archipelago NuGet Packages for Game Emulator Integration
# Table of Contents

Introduction
Available Packages
Installation
Basic Usage
Connecting to an AP Server
Location Tracking
Advanced Features

## Introduction
This guide covers the usage of Archipelago NuGet packages for integrating game emulators with the Archipelago Multiworld Randomizer. These packages allow developers to create new "APWorlds," enabling the implementation of new games for Archipelago.
## Available Packages

 - Archipelago.Core: Contains most of the core functionality
 - Archipelago.PCSX2: Implementation for PCSX2 emulator
 - Archipelago.ePSXe: Implementation for ePSXe emulator
 - Archipelago.Xenia: Implementation for Xenia emulator
 - Archipelago.BizHawk: Implementation for BizHawk emulator

## Installation

Install the Archipelago.Core NuGet package
Install the specific client package for your emulator (e.g., Archipelago.PCSX2, Archipelago.ePSXe)

## Basic Usage
### Creating a Game Client
```
var gameClient = new ePSXeClient();
gameClient.Connect();
```  
### Creating an Archipelago Client
```
var archipelagoClient = new ArchipelagoClient(gameClient);
```  
### Using Memory Functions
```var money = Memory.ReadInt(0x00000000);
Memory.WriteString(0x00000000, "Hello World");
```  

### Connecting to an AP Server
```
archipelagoClient.Connect("archipelago.gg:12345", "GameName");
archipelagoClient.Login("Player1", "Password");
```  
### Location Tracking
To set up location tracking:

Create a collection of Location objects
Call PopulateLocations after connecting but before logging in:

``` 
archipelagoClient.PopulateLocations(myLocations);
 ```  

## Location Object Properties

 - ulong address: The address that changes when the location is completed
 - int addressbit: Which bit of this address is related to this specific location (only used when LocationCheckType == Bit)
 - string name: The name of the location
 - int id: The id of the location in the apworld
 - LocationCheckType CheckType: The data type of the location check (supports Bit, Int, Uint, Byte)
 - string CheckValue: The value to compare to determine if the location check is met
 - LocationCheckCompareType CompareType: The comparison type for the location check (supports Match, GreaterThan, LessThan, Range)

## Advanced Features

The client will trigger the ItemReceived event when an item is received.
Ensure location tracking setup is done after connecting the Archipelago client but before logging in the user.

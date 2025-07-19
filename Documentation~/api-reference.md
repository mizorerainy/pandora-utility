# API Reference - Pandora Unity Utility Package

This document provides comprehensive documentation for all public APIs in the Pandora Unity Utility Package.

## Table of Contents

- [Core Namespace](#core-namespace)
- [AetherLink Network System](#aetherlink-network-system)
- [Data Structures](#data-structures)
- [Events](#events)
- [Enumerations](#enumerations)
- [Usage Examples](#usage-examples)

---

## Core Namespace

All networking functionality is contained within:
```csharp
using PandoraUtility.Network;
```
---

## AetherLink Network System

The `AetherLink` class is the core component for network communication, inheriting from `MonoBehaviour` and designed as a singleton.

### Class Declaration
```csharp
public partial class AetherLink : MonoBehaviour
```
### Static Properties

#### Instance
```csharp
public static AetherLink Instance { get; }
```
Gets the singleton instance of AetherLink. Automatically created if it doesn't exist.

### Instance Properties

#### IsRunning
```csharp
public bool IsRunning { get; }
```
Returns `true` if the AetherLink system is currently running and processing network operations.

#### IsConnected
```csharp
public bool IsConnected { get; }
```
Returns `true` if there is an active network connection established.

#### CurrentMode
```csharp
public Mode CurrentMode { get; }
```
Gets the current operational mode of the AetherLink system (Master or Slave).

#### IsMaster
```csharp
public bool IsMaster { get; }
```
Returns `true` if the current instance is operating as the master/server.

#### IsSlave
```csharp
public bool IsSlave { get; }
```
Returns `true` if the current instance is operating as the slave/client.

#### RemoteEndPoint
```csharp
public IPEndPoint RemoteEndPoint { get; }
```
Gets the IP endpoint of the remote connection, if connected.

#### Statistics
```csharp
public NetworkStats Statistics { get; }
```
Gets the current network statistics including data transfer rates and connection information.

### Core Methods

#### Initialize
```csharp
public void Initialize(Settings settings)
```
Initializes the AetherLink system with the provided settings.

**Parameters:**
- `settings`: Configuration object containing network parameters

**Example:**
```csharp
var settings = Settings.Default;
// Configure settings as needed
AetherLink.Instance.Initialize(settings);
```
#### StartLink
```csharp
public void StartLink()
```
Starts the network link based on the current settings. Must be called after `Initialize()`.

#### StopLink
```csharp
public void StopLink()
```
Stops the network link and closes all connections.

#### SendData
```csharp
public void SendData(ushort header, params object[] data)
```
Sends data over the network connection.

**Parameters:**
- `header`: Unique identifier for the message type
- `data`: Variable number of objects to serialize and send

**Example:**
```csharp
// Send player position
AetherLink.Instance.SendData(1001, transform.position, playerName);
```
### Type Registration

#### RegisterSerializableType
```csharp
public void RegisterSerializableType<T>(ushort typeId, bool override = false)
```
Registers a custom type for network serialization.

**Parameters:**
- `typeId`: Unique identifier for the type
- `override`: Whether to override existing registration

**Example:**
```csharp
AetherLink.Instance.RegisterSerializableType<PlayerData>(2001);
```
### Utility Methods

#### ResetStatistics
```csharp
public void ResetStatistics()
```
Resets all network statistics counters.

#### CleanupNetworkResources
```csharp
public void CleanupNetworkResources()
```
Manually cleanup network resources. Usually called automatically.

#### DisposeResources
```csharp
public void DisposeResources()
```
Dispose all managed and unmanaged resources.

### Unity Lifecycle

The following methods are automatically called by Unity:

- `OnEnable()`: Called when the component becomes enabled
- `OnDisable()`: Called when the component becomes disabled
- `OnDestroy()`: Called when the object is destroyed
- `OnApplicationPause(bool pauseStatus)`: Called when application is paused/resumed

---

## Data Structures

### Settings Class

Configuration object for AetherLink initialization.
```csharp
public class Settings
{
    public static Settings Default { get; }
}
```
**Usage:**
```csharp
var settings = Settings.Default;
AetherLink.Instance.Initialize(settings);
```
### PacketResponse Class

Represents a received network packet.
```csharp
public class PacketResponse
{
    public ushort Header { get; }
    public byte[] Data { get; }
    public IPEndPoint RemoteEndPoint { get; }
    
    public PacketResponse(ushort header, byte[] data, IPEndPoint remoteEndPoint)
    public object[] ReadObjects()
}
```
**Properties:**
- `Header`: Message type identifier
- `Data`: Raw packet data
- `RemoteEndPoint`: Source of the packet

**Methods:**
- `ReadObjects()`: Deserializes the packet data into objects

**Example:**
```csharp
public void OnPacketReceived(PacketResponse packet)
{
    if (packet.Header == 1001)
    {
        object[] data = packet.ReadObjects();
        Vector3 position = (Vector3)data[0];
        string playerName = (string)data[1];
    }
}
```
### NetworkStats Class

Contains network performance statistics.
```csharp
public class NetworkStats
{
    public void Reset()
}
```
**Methods:**
- `Reset()`: Resets all statistics counters

---

## Events

### UnityPacketEvent

UnityEvent for handling received packets.
```csharp
public class UnityPacketEvent : UnityEvent<PacketResponse>
```
### UnityIPEndPointEvent

UnityEvent for handling IP endpoint events.
```csharp
public class UnityIPEndPointEvent : UnityEvent<IPEndPoint>
```
---

## Enumerations

### Mode

Defines the operational mode of AetherLink.
```csharp
public enum Mode
{
    // Values not specified in summary - typically Master/Server and Slave/Client modes
}
```
---

## Usage Examples

### Basic Setup

```csharp
using UnityEngine;
using PandoraUtility.Network;

public class NetworkManager : MonoBehaviour
{
    void Start()
    {
        // Initialize with default settings
        var settings = Settings.Default;
        AetherLink.Instance.Initialize(settings);
        
        // Start the network link
        AetherLink.Instance.StartLink();
    }
    
    void Update()
    {
        if (AetherLink.Instance.IsConnected)
        {
            // Send periodic updates
            AetherLink.Instance.SendData(1000, Time.time);
        }
    }
}
```
### Custom Data Types

```csharp
[System.Serializable]
public class PlayerData
{
    public string name;
    public Vector3 position;
    public int health;
}

void Start()
{
    // Register custom type
    AetherLink.Instance.RegisterSerializableType<PlayerData>(2000);
    
    // Send custom data
    var playerData = new PlayerData
    {
        name = "Player1",
        position = transform.position,
        health = 100
    };
    
    AetherLink.Instance.SendData(2000, playerData);
}
```


### Event Handling

```csharp
public UnityPacketEvent onPacketReceived;

void Start()
{
    // Subscribe to packet events
    onPacketReceived.AddListener(HandlePacket);
}

void HandlePacket(PacketResponse packet)
{
    switch (packet.Header)
    {
        case 1000:
            // Handle time sync
            break;
        case 2000:
            // Handle player data
            object[] data = packet.ReadObjects();
            break;
    }
}
```


### Connection Status Monitoring

```csharp
void Update()
{
    if (AetherLink.Instance.IsRunning)
    {
        if (AetherLink.Instance.IsConnected)
        {
            // Handle connected state
            UpdateNetworkStats();
        }
        else
        {
            // Handle disconnected state
            AttemptReconnection();
        }
    }
}

void UpdateNetworkStats()
{
    NetworkStats stats = AetherLink.Instance.Statistics;
    // Display or log statistics
}
```


---

## Best Practices

### Initialization
- Always call `Initialize()` before `StartLink()`
- Use `Settings.Default` as a starting point for configuration
- Register custom types before starting the link

### Data Transmission
- Use meaningful header IDs for different message types
- Keep data packets small for better performance
- Register custom types with unique type IDs

### Error Handling
- Check `IsRunning` before performing network operations
- Monitor `IsConnected` for connection state changes
- Handle network disconnections gracefully

### Performance
- Use `ResetStatistics()` to clear counters when needed
- Call `CleanupNetworkResources()` if experiencing memory issues
- Monitor network statistics for performance optimization

---

## Thread Safety

AetherLink is designed to be used from the Unity main thread. Network operations are handled internally with appropriate threading, but public API calls should be made from the main thread only.

---

## Troubleshooting

### Common Issues

**AetherLink.Instance returns null**
- Ensure there's a GameObject with AetherLink component in the scene
- The singleton is created automatically when first accessed

**SendData throws exceptions**
- Verify `Initialize()` was called successfully
- Check that `StartLink()` was called and `IsRunning` returns true
- Ensure all data types are registered if using custom types

**Connection fails**
- Verify network settings in the Settings object
- Check firewall and network permissions
- Ensure both endpoints are using compatible configurations

---

*This API reference is based on AetherLink system analysis. For implementation-specific details, refer to the source code and inline documentation.*

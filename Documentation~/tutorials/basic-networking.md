# Basic Networking Tutorial

This tutorial will guide you through the fundamentals of networking with the AetherLink system, from initial setup to establishing your first network connection.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Understanding AetherLink Concepts](#understanding-aetherlink-concepts)
- [Setting Up Your First Network](#setting-up-your-first-network)
- [Master-Slave Architecture](#master-slave-architecture)
- [Sending and Receiving Data](#sending-and-receiving-data)
- [Connection Management](#connection-management)
- [Testing Your Network](#testing-your-network)
- [Common Patterns](#common-patterns)

---

## Prerequisites

Before starting this tutorial, ensure you have:

- Unity 2021.3 or later
- Pandora Unity Utility Package installed
- Basic understanding of Unity MonoBehaviour lifecycle
- Familiarity with C# programming

## Understanding AetherLink Concepts

### Core Components

**AetherLink**: The main networking component that handles all network communication. It operates as a singleton and manages connections between devices.

**Master-Slave Architecture**: AetherLink uses a simple two-device model where one device acts as the Master (server) and another as the Slave (client).

**Packet-Based Communication**: Data is sent as packets with headers (message IDs) and serialized payload data.

### Key Concepts

- **Header**: A unique 16-bit identifier for each message type
- **Serialization**: Converting objects into byte arrays for network transmission
- **EndPoint**: Network address information (IP address and port)
- **Statistics**: Network performance metrics and connection information

---

## Setting Up Your First Network

### Step 1: Create the Network Manager

Create a new script that will manage your networking:
```csharp
using UnityEngine;
using PandoraUtility.Network;

public class BasicNetworkManager : MonoBehaviour
{
    [Header("Network Configuration")]
    [SerializeField] private bool isMasterDevice = true;

    void Start()
    {
        InitializeNetwork();
    }
    
    void InitializeNetwork()
    {
        // Get or create AetherLink instance
        var aetherLink = AetherLink.Instance;
        
        // Initialize with default settings
        var settings = Settings.Default;
        aetherLink.Initialize(settings);
        
        // Start the network link
        aetherLink.StartLink();
        
        Debug.Log($"Network initialized as {(isMasterDevice ? "Master" : "Slave")}");
    }
    
    void OnDestroy()
    {
        // Clean shutdown
        if (AetherLink.Instance != null)
        {
            AetherLink.Instance.StopLink();
        }
    }
}
```
### Step 2: Setup Network GameObject

1. Create an empty GameObject in your scene
2. Name it "NetworkManager"
3. Attach the `BasicNetworkManager` script
4. Configure the `isMasterDevice` setting:
    - Set to `true` for the device that will act as server
    - Set to `false` for the device that will act as client

---

## Master-Slave Architecture

### Understanding the Roles

**Master (Server)**:
- Accepts incoming connections
- Usually the device that starts first
- Can initiate communication
- Manages the connection state

**Slave (Client)**:
- Connects to the Master
- Responds to Master's requests
- Can also send data to Master

### Checking Connection Status
```csharp
public class ConnectionMonitor : MonoBehaviour
{
    void Update()
    {
        var aetherLink = AetherLink.Instance;

        if (aetherLink.IsRunning)
        {
            if (aetherLink.IsConnected)
            {
                // Display connection info
                string role = aetherLink.IsMaster ? "Master" : "Slave";
                Debug.Log($"Connected as {role} to {aetherLink.RemoteEndPoint}");
            }
            else
            {
                Debug.Log("Network running but not connected");
            }
        }
    }
}
```
---

## Sending and Receiving Data

### Basic Data Transmission
```csharp
public class DataTransmitter : MonoBehaviour
{
    void Start()
    {
        // Send simple data
        SendPlayerInfo();

        // Send multiple objects
        SendGameState();
    }
    
    public struct PlayerInfo { public int playerId; public int playerLevel; }
    public struct GameStateData { public Vector3 position; public float health; public bool isAlive; }

    void SendPlayerInfo()
    {
        if (AetherLink.Instance.IsConnected)
        {
            // Header 1001 = Player Info
            AetherLink.Instance.SendData(1001, new PlayerInfo { playerId = 1, playerLevel = 5 });
        }
    }
    
    void SendGameState()
    {
        if (AetherLink.Instance.IsConnected)
        {
            // Header 1002 = Game State
            var state = new GameStateData 
            { 
                position = transform.position, 
                health = 100f, 
                isAlive = true 
            };
            AetherLink.Instance.SendData(1002, state);
        }
    }
}
```
### Receiving Data with Events
```csharp
public class DataReceiver : MonoBehaviour
{
    [Header("Events")]
    public UnityPacketEvent onPacketReceived;

    void Start()
    {
        // Subscribe to packet events
        onPacketReceived.AddListener(HandleIncomingPacket);
    }
    
    void HandleIncomingPacket(PacketResponse packet)
    {
        switch (packet.Header)
        {
            case 1001:
                HandlePlayerInfo(packet);
                break;
            case 1002:
                HandleGameState(packet);
                break;
            default:
                Debug.LogWarning($"Unknown packet header: {packet.Header}");
                break;
        }
    }
    
    void HandlePlayerInfo(PacketResponse packet)
    {
        var info = packet.ReadAs<PlayerInfo>();
        Debug.Log($"Received player info for ID: {info.playerId}, Level {info.playerLevel}");
    }
    
    void HandleGameState(PacketResponse packet)
    {
        var state = packet.ReadAs<GameStateData>();
        Debug.Log($"Game state: Pos={state.position}, Health={state.health}, Alive={state.isAlive}");
    }
}
```
---

## Connection Management

### Monitoring Connection State
```csharp
public class ConnectionManager : MonoBehaviour
{
    private bool wasConnected = false;

    void Update()
    {
        bool isConnected = AetherLink.Instance.IsConnected;
        
        // Detect connection state changes
        if (isConnected != wasConnected)
        {
            if (isConnected)
            {
                OnConnectionEstablished();
            }
            else
            {
                OnConnectionLost();
            }
            wasConnected = isConnected;
        }
    }
    
    void OnConnectionEstablished()
    {
        Debug.Log("Connection established!");
        
        // Enable network-dependent features
        EnableNetworkFeatures();
    }
    
    void OnConnectionLost()
    {
        Debug.Log("Connection lost!");
        
        // Disable network features and show offline mode
        DisableNetworkFeatures();
    }
    
    void EnableNetworkFeatures()
    {
        // Enable multiplayer UI, start sending updates, etc.
    }
    
    void DisableNetworkFeatures()
    {
        // Disable multiplayer features, show reconnection UI
    }
}
```
### Network Statistics
```csharp
public class NetworkStatsDisplay : MonoBehaviour
{
    void Update()
    {
        if (AetherLink.Instance.IsConnected)
        {
            NetworkStats stats = AetherLink.Instance.Statistics;
            DisplayStats(stats);
        }
    }

    void DisplayStats(NetworkStats stats)
    {
        // Display network performance information
        // Implementation depends on NetworkStats properties
    }
}
```
---

## Testing Your Network

### Local Testing Setup

1. **Single Device Testing**:
    - Build your project as a standalone application
    - Run the build with Master settings
    - Run Unity editor with Slave settings
    - They should connect automatically on the same machine

2. **Two Device Testing**:
    - Set one device as Master, another as Slave
    - Ensure both devices are on the same local network
    - Check firewall settings allow the connection

### Debugging Connection Issues
```csharp
public class NetworkDebugger : MonoBehaviour
{
    void Update()
    {
        var aetherLink = AetherLink.Instance;

        // Log current state
        if (Input.GetKeyDown(KeyCode.D))
        {
            LogNetworkState(aetherLink);
        }
        
        // Reset statistics
        if (Input.GetKeyDown(KeyCode.R))
        {
            aetherLink.ResetStatistics();
            Debug.Log("Network statistics reset");
        }
    }
    
    void LogNetworkState(AetherLink aetherLink)
    {
        Debug.Log($"IsRunning: {aetherLink.IsRunning}");
        Debug.Log($"IsConnected: {aetherLink.IsConnected}");
        Debug.Log($"Current Mode: {aetherLink.CurrentMode}");
        Debug.Log($"Remote EndPoint: {aetherLink.RemoteEndPoint}");
    }
}
```
---

## Common Patterns

### Periodic Updates
```csharp
public class PeriodicNetworkUpdate : MonoBehaviour
{
    [SerializeField] private float updateInterval = 0.1f;
    private float lastUpdateTime;

    void Update()
    {
        if (AetherLink.Instance.IsConnected && 
            Time.time - lastUpdateTime >= updateInterval)
        {
            SendPeriodicUpdate();
            lastUpdateTime = Time.time;
        }
    }
    
    public struct PeriodicUpdate { public Vector3 position; public Quaternion rotation; }

    void SendPeriodicUpdate()
    {
        // Send regular updates like position, animation state, etc.
        AetherLink.Instance.SendData(2001, new PeriodicUpdate { position = transform.position, rotation = transform.rotation });
    }
}
```
### Request-Response Pattern
```csharp
public class RequestResponseExample : MonoBehaviour
{
    void Start()
    {
        if (AetherLink.Instance.IsMaster)
        {
            // Master can request data from slave
            RequestSlaveData();
        }
    }

    public struct DataRequest { public int requestCode; }
    public struct PlayerDataResponse { public int playerId; public float health; public float energy; }

    void RequestSlaveData()
    {
        // Header 3001 = Data Request
        AetherLink.Instance.SendData(3001, new DataRequest { requestCode = 1 });
    }
    
    void HandleDataRequest(PacketResponse packet)
    {
        if (packet.Header == 3001)
        {
            var request = packet.ReadAs<DataRequest>();
            
            if (request.requestCode == 1)
            {
                // Respond with requested data
                // Header 3002 = Data Response
                AetherLink.Instance.SendData(3002, new PlayerDataResponse { playerId = 100, health = 100f, energy = 75.5f });
            }
        }
    }
}
```
### Error Handling
```csharp
public class NetworkErrorHandler : MonoBehaviour
{
    void SendDataSafely<T>(ushort header, T data) where T : unmanaged
    {
        try
        {
            if (AetherLink.Instance.IsConnected)
            {
                AetherLink.Instance.SendData(header, data);
            }
            else
            {
                Debug.LogWarning("Cannot send data: Not connected");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Network send error: {ex.Message}");
        }
    }
}
```
---

## Next Steps

After completing this basic tutorial, you're ready to explore:

1. **[AetherLink Setup Tutorial](aetherlink-setup.md)** - Advanced configuration options
2. **[Examples Scenarios](../examples/advanced-scenarios.md)** - Working with extensive custom data streams
3. **[Advanced Scenarios](../examples/advanced-scenarios.md)** - Complex networking patterns

## Troubleshooting

**No connection established**:
- Verify both devices are on the same network
- Check firewall settings
- Ensure one device is Master and the other is Slave

**Data not being received**:
- Check that packet event handlers are properly subscribed
- Verify header IDs match between sender and receiver
- Confirm connection is established before sending

**Performance issues**:
- Reduce update frequency for periodic messages
- Use appropriate data types (avoid sending large objects frequently)
- Monitor network statistics to identify bottlenecks

---

*This tutorial covered the basics of AetherLink networking. Practice with these examples and experiment with different data types and communication patterns to build more complex networked applications.*

# Simple Connection Tutorial

This tutorial walks you through creating your first AetherLink connection between two Unity applications. You'll learn the basics of setting up Master and Slave devices, establishing connections, and sending simple data.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Understanding Master-Slave Architecture](#understanding-master-slave-architecture)
- [Setting Up the Master Device](#setting-up-the-master-device)
- [Setting Up the Slave Device](#setting-up-the-slave-device)
- [Testing the Connection](#testing-the-connection)
- [Sending Your First Data](#sending-your-first-data)
- [Receiving Data](#receiving-data)
- [Common Issues](#common-issues)
- [Next Steps](#next-steps)

---

## Prerequisites

Before starting this tutorial, ensure you have:

- Unity 2021.3 or later
- Pandora Unity Utility Package installed
- Two Unity instances (or one Unity Editor + one build)
- Both devices connected to the same Local Area Network (LAN)

## Understanding Master-Slave Architecture

AetherLink uses a Master-Slave connection model:

- **Master**: Waits for and accepts incoming connections
- **Slave**: Searches for and connects to a Master device
- **Connection**: Once established, both devices can send and receive data equally

### Key Points

1. Only one Master and one Slave per connection
2. The Master must be started first
3. Both devices must be on the same network
4. Connection is automatic once both are running

---

## Setting Up the Master Device

### Step 1: Create the Master Script

Create a new script called `SimpleConnectionMaster.cs`:
```csharp
using UnityEngine;
using PandoraUtility.Network;

public class SimpleConnectionMaster : MonoBehaviour
{
    [Header("Connection Settings")]
    [SerializeField] private bool autoStart = true;

    [Header("Events")]
    public UnityIPEndPointEvent onConnected = new UnityIPEndPointEvent();
    public UnityPacketEvent onPacketReceived = new UnityPacketEvent();
    public UnityEvent onDisconnected = new UnityEvent();
    
    void Start()
    {
        if (autoStart)
        {
            SetupMasterConnection();
        }
    }
    
    void SetupMasterConnection()
    {
        // Get default settings and configure for Master mode
        var settings = Settings.Default;
        settings.LinkMode = Mode.Master;
        
        // Initialize AetherLink with Master settings
        AetherLink.Instance.Initialize(settings);
        
        // Subscribe to connection events
        onConnected.AddListener(OnDeviceConnected);
        onPacketReceived.AddListener(OnDataReceived);
        onDisconnected.AddListener(OnDeviceDisconnected);
        
        // Start the network link
        AetherLink.Instance.StartLink();
        
        Debug.Log("Master device started - waiting for Slave connection...");
    }
    
    void OnDeviceConnected(IPEndPoint remoteEndpoint)
    {
        Debug.Log($"Slave connected from: {remoteEndpoint}");
    }
    
    // Define our network data structures
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct JoinRequest
    {
        public int PlayerId;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct ServerStatus
    {
        public bool IsReady;
    }

    void OnDataReceived(PacketResponse packet)
    {
        Debug.Log($"Master received data with header: {packet.Header}");
        
        // Handle different types of data based on header
        switch (packet.Header)
        {
            case 1001:
                HandleJoinRequest(packet);
                break;
            default:
                Debug.Log($"Unknown packet header: {packet.Header}");
                break;
        }
    }
    
    void HandleJoinRequest(PacketResponse packet)
    {
        var request = packet.ReadAs<JoinRequest>();
        Debug.Log($"Join request from Player ID over LAN: {request.PlayerId}");
        
        // Send response back to Slave
        var status = new ServerStatus { IsReady = true };
        AetherLink.Instance.SendData(1002, status);
    }
    
    void OnDeviceDisconnected()
    {
        Debug.Log("Slave disconnected");
    }
    
    // Public method to manually start connection
    public void StartMasterConnection()
    {
        SetupMasterConnection();
    }
}
```
### Step 2: Create the Master Scene

1. Create a new scene called `MasterScene`
2. Create an empty GameObject called `ConnectionManager`
3. Attach the `SimpleConnectionMaster` script to it
4. Configure the script in the Inspector:
   - Set `Auto Start` to true
   - Wire up the Unity Events if you want visual feedback

---

## Setting Up the Slave Device

### Step 1: Create the Slave Script

Create a new script called `SimpleConnectionSlave.cs`:
```csharp
using UnityEngine;
using PandoraUtility.Network;
using System.Collections;

public class SimpleConnectionSlave : MonoBehaviour
{
    [Header("Connection Settings")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private float greetingDelay = 2f;

    [Header("Events")]
    public UnityIPEndPointEvent onConnected = new UnityIPEndPointEvent();
    public UnityPacketEvent onPacketReceived = new UnityPacketEvent();
    public UnityEvent onDisconnected = new UnityEvent();
    
    void Start()
    {
        if (autoStart)
        {
            SetupSlaveConnection();
        }
    }
    
    void SetupSlaveConnection()
    {
        // Get default settings and configure for Slave mode
        var settings = Settings.Default;
        settings.LinkMode = Mode.Slave;
        
        // Initialize AetherLink with Slave settings
        AetherLink.Instance.Initialize(settings);
        
        // Subscribe to connection events
        onConnected.AddListener(OnConnectedToMaster);
        onPacketReceived.AddListener(OnDataReceived);
        onDisconnected.AddListener(OnDisconnectedFromMaster);
        
        // Start the network link
        AetherLink.Instance.StartLink();
        
        Debug.Log("Slave device started - searching for Master...");
    }
    
    // Define our network data structures
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct JoinRequest
    {
        public int PlayerId;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct ServerStatus
    {
        public bool IsReady;
    }

    void OnConnectedToMaster(IPEndPoint masterEndpoint)
    {
        Debug.Log($"Connected to Master at: {masterEndpoint}");
        
        // Send join request after a short delay
        StartCoroutine(SendJoinRequestAfterDelay());
    }
    
    IEnumerator SendJoinRequestAfterDelay()
    {
        yield return new WaitForSeconds(greetingDelay);
        
        if (AetherLink.Instance.IsConnected)
        {
            SendJoinRequest();
        }
    }
    
    void SendJoinRequest()
    {
        var joinRequest = new JoinRequest { PlayerId = Random.Range(1000, 9999) };
        AetherLink.Instance.SendData(1001, joinRequest);
        Debug.Log("Sent join request to Master");
    }
    
    void OnDataReceived(PacketResponse packet)
    {
        Debug.Log($"Slave received data with header: {packet.Header}");
        
        // Handle different types of data based on header
        switch (packet.Header)
        {
            case 1002:
                HandleMasterResponse(packet);
                break;
            default:
                Debug.Log($"Unknown packet header: {packet.Header}");
                break;
        }
    }
    
    void HandleMasterResponse(PacketResponse packet)
    {
        var status = packet.ReadAs<ServerStatus>();
        Debug.Log($"Server Ready Status: {status.IsReady}");
    }
    
    void OnDisconnectedFromMaster()
    {
        Debug.Log("Disconnected from Master");
    }
    
    // Public method to manually start connection
    public void StartSlaveConnection()
    {
        SetupSlaveConnection();
    }
}
```
### Step 2: Create the Slave Scene

1. Create a new scene called `SlaveScene`
2. Create an empty GameObject called `ConnectionManager`
3. Attach the `SimpleConnectionSlave` script to it
4. Configure the script in the Inspector similar to the Master

---

## Testing the Connection

### Method 1: Two Unity Editor Instances

1. **Start the Master**:
   - Open the `MasterScene` in Unity Editor
   - Press Play
   - Watch the Console for "Master device started" message

2. **Start the Slave**:
   - Open a second Unity Editor instance
   - Open the `SlaveScene`
   - Press Play
   - Watch for connection messages in both consoles

### Method 2: Editor + Build

1. **Build the Master**:
   - Set `MasterScene` as the first scene in Build Settings
   - Build and run the application
   - The Master will start automatically

2. **Run Slave in Editor**:
   - Open `SlaveScene` in Unity Editor
   - Press Play
   - The Slave should connect to the built Master

### Expected Output

When successful, you should see:

**Master Console:**
```

Master device started - waiting for Slave connection...
Slave connected from: 192.168.1.100:54321
Join request from Player ID over LAN: 4572
```
**Slave Console:**
```

Slave device started - searching for Master...
Connected to Master at: 192.168.1.101:7777
Sent join request to Master
Server Ready Status: True
```
---

## Sending Your First Data

Now let's expand the example to send different types of data:

### Enhanced Master Script

Add this method to your `SimpleConnectionMaster` script:
```csharp
[Header("Data Sending")]
[SerializeField] private KeyCode sendDataKey = KeyCode.Space;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public struct GameData
{
    public float Time;
    public Vector3 Position;
    public int Score;
}

void Update()
{
    if (Input.GetKeyDown(sendDataKey) && AetherLink.Instance.IsConnected)
    {
        SendSampleData();
    }
}

void SendSampleData()
{
    // Build our Unmanaged Struct
    var matchData = new GameData
    {
        Time = Time.time,
        Position = new Vector3(
            Random.Range(-10f, 10f),
            Random.Range(0f, 5f),
            Random.Range(-10f, 10f)
        ),
        Score = Random.Range(0, 1000)
    };

    AetherLink.Instance.SendData(2001, matchData);
    Debug.Log($"Master sent: Time={matchData.Time}, Pos={matchData.Position}, Score={matchData.Score}");
}
```
### Enhanced Slave Script

Add this method to your `SimpleConnectionSlave` script:
```csharp
void OnDataReceived(PacketResponse packet)
{
    Debug.Log($"Slave received data with header: {packet.Header}");

    switch (packet.Header)
    {
        case 1002:
            HandleMasterResponse(packet);
            break;
        case 2001:
            HandleSampleData(packet);
            break;
        default:
            Debug.Log($"Unknown packet header: {packet.Header}");
            break;
    }
}

void HandleSampleData(PacketResponse packet)
{
    var data = packet.ReadAs<SimpleConnectionMaster.GameData>();

    Debug.Log($"Sample data - Time: {data.Time}, Position: {data.Position}, Score: {data.Score}");
}
```
---

## Receiving Data

### Data Types Supported

AetherLink automatically handles zero-allocation serialization for any `unmanaged` struct.

- **Primitives**: bool, byte, int, float, double (use primitives directly inside a struct)
- **Unity Types**: Vector2, Vector3, Quaternion (all unmanaged in Unity)
- **Custom Types**: Any struct with `[StructLayout(LayoutKind.Sequential)]`

### Reading Data Example
```csharp
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public struct ComplexData
{
    public Vector3 position;
    public float health;
}

void HandleComplexData(PacketResponse packet)
{
    // Cast to expected type effortlessly without allocating memory
    var data = packet.ReadAs<ComplexData>();
    
    Debug.Log($"Player at {data.position} with {data.health} health");
}
```
---

## Common Issues

### Connection Not Established

**Symptoms:**
- Master shows "waiting for connection" but Slave never connects
- No error messages

**Solutions:**
1. Ensure both devices are on the same network
2. Check firewall settings
3. Verify both applications are running
4. Make sure only one Master and one Slave are running

### Data Not Received

**Symptoms:**
- Connection established but no data packets received
- SendData called but OnDataReceived not triggered

**Solutions:**
1. Verify Unity Events are properly connected
2. Check packet header consistency
3. Ensure connection is active before sending data
4. Add debug logs to confirm SendData is being called

### Struct Size Errors

**Symptoms:**
- `ReadAs<T>` returning wrong or garbled numbers
- Marshal exception when parsing the struct

**Solutions:**
1. Always add `[StructLayout(LayoutKind.Sequential)]` to your structs.
2. Match the exact same struct definition on both Master and Slave devices.
3. Don't use reference classes (like `string` or `List`). Always stick to blittable types like arrays created with `fixed` bounds, or custom string representations like `FixedString32Bytes`.

---

## Next Steps

Now that you have a basic connection working:

1. **Explore Advanced Features**:
   - Custom serializable types
   - Packet filtering and routing
   - Connection monitoring and statistics

2. **Build Real Applications**:
   - Multiplayer games
   - Data synchronization tools
   - Remote control applications

3. **Learn Best Practices**:
   - Review the [AetherLink Setup Tutorial](aetherlink-setup.md)
   - Check out [Advanced Usage Examples](../examples/)
   - Read the [API Reference](../api-reference.md)

4. **Performance Optimization**:
   - Implement data batching
   - Use appropriate update rates
   - Monitor network statistics

---

## Complete Example Project

Here's a minimal complete example you can copy and test immediately:

### TestConnection.cs (For both Master and Slave)
```csharp
using UnityEngine;
using PandoraUtility.Network;

public class TestConnection : MonoBehaviour
{
    [Header("Device Settings")]
    [SerializeField] private bool isMaster = true;

    void Start()
    {
        SetupConnection();
    }
    
    void SetupConnection()
    {
        var settings = Settings.Default;
        settings.LinkMode = isMaster ? Mode.Master : Mode.Slave;
        
        AetherLink.Instance.Initialize(settings);
        AetherLink.Instance.OnConnectedInspector.AddListener(OnConnected);
        AetherLink.Instance.OnPacketReceivedInspector.AddListener(OnDataReceived);
        AetherLink.Instance.OnDisconnectedInspector.AddListener(OnDisconnected);
        AetherLink.Instance.StartLink();
        
        Debug.Log($"Started as {(isMaster ? "Master" : "Slave")}");
    }
    
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct SimpleMessage
    {
        public int Id;
        public bool Flag;
    }

    void OnConnected(IPEndPoint endpoint)
    {
        Debug.Log($"Connected to {endpoint}");
        
        // Send test message
        var msg = new SimpleMessage { Id = isMaster ? 1 : 2, Flag = true };
        AetherLink.Instance.SendData(1000, msg);
    }
    
    void OnDataReceived(PacketResponse packet)
    {
        if (packet.Header == 1000)
        {
            var data = packet.ReadAs<SimpleMessage>();
            Debug.Log($"Received ID: {data.Id}, Flag: {data.Flag}");
        }
    }
    
    void OnDisconnected()
    {
        Debug.Log("Disconnected");
    }
}
```
Simply attach this script to a GameObject, set one instance as Master and another as Slave, and run both to see a working connection with data exchange.

---

*This tutorial provided the foundation for AetherLink connections. Continue with the advanced tutorials to build more sophisticated networked applications.*

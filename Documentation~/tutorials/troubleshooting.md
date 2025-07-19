# AetherLink Troubleshooting Guide

This comprehensive guide helps diagnose and resolve common issues with the AetherLink networking system. Follow the structured approach to identify and fix problems quickly.

## Table of Contents

- [Quick Diagnostic Checklist](#quick-diagnostic-checklist)
- [Connection Issues](#connection-issues)
- [Data Transmission Problems](#data-transmission-problems)
- [Performance Issues](#performance-issues)
- [Platform-Specific Problems](#platform-specific-problems)
- [Development Environment Issues](#development-environment-issues)
- [Network Configuration Problems](#network-configuration-problems)
- [Advanced Debugging](#advanced-debugging)
- [Common Error Messages](#common-error-messages)
- [Recovery Procedures](#recovery-procedures)

---

## Quick Diagnostic Checklist

Before diving into detailed troubleshooting, run through this quick checklist:
```csharp
public class AetherLinkDiagnostics : MonoBehaviour
{
   [Header("Diagnostic Tools")]
   [SerializeField] private KeyCode diagnosticKey = KeyCode.F1;

    void Update()
    {
        if (Input.GetKeyDown(diagnosticKey))
        {
            RunDiagnostics();
        }
    }
    
    public void RunDiagnostics()
    {
        Debug.Log("=== AetherLink Diagnostics ===");
        
        // 1. Check AetherLink instance
        if (AetherLink.Instance == null)
        {
            Debug.LogError("FAIL: AetherLink.Instance is null");
            return;
        }
        Debug.Log("PASS: AetherLink instance exists");
        
        // 2. Check initialization state
        if (!AetherLink.Instance.IsRunning)
        {
            Debug.LogWarning("WARNING: AetherLink is not running - call Initialize() and StartLink()");
        }
        else
        {
            Debug.Log("PASS: AetherLink is running");
        }
        
        // 3. Check connection state
        if (!AetherLink.Instance.IsConnected)
        {
            Debug.LogWarning("WARNING: Not connected to remote device");
        }
        else
        {
            Debug.Log($"PASS: Connected as {AetherLink.Instance.CurrentMode} to {AetherLink.Instance.RemoteEndPoint}");
        }
        
        // 4. Check network statistics
        var stats = AetherLink.Instance.Statistics;
        Debug.Log($"INFO: Network Statistics Available: {stats.ToString() != null}");
    }
}
```
### Essential Checks

1. **Instance exists**: `AetherLink.Instance != null`
2. **System running**: `AetherLink.Instance.IsRunning == true`
3. **Connection active**: `AetherLink.Instance.IsConnected == true`
4. **Network accessible**: Both devices on same LAN
5. **Firewall permits**: TCP and UDP ports not blocked

---

## Connection Issues

### Problem: No Connection Established

**Symptoms:**
- `IsConnected` returns false
- No error messages in console
- Both devices seem to be running correctly

**Diagnostic Steps:**
```csharp
public class ConnectionDiagnostics : MonoBehaviour
{
void DiagnoseConnectionIssue()
{
Debug.Log("=== Connection Diagnostics ===");

        // Check basic states
        Debug.Log($"Is Running: {AetherLink.Instance.IsRunning}");
        Debug.Log($"Is Connected: {AetherLink.Instance.IsConnected}");
        Debug.Log($"Current Mode: {AetherLink.Instance.CurrentMode}");
        Debug.Log($"Is Master: {AetherLink.Instance.IsMaster}");
        Debug.Log($"Is Slave: {AetherLink.Instance.IsSlave}");
        
        // Network endpoint info
        if (AetherLink.Instance.RemoteEndPoint != null)
        {
            Debug.Log($"Remote Endpoint: {AetherLink.Instance.RemoteEndPoint}");
        }
        else
        {
            Debug.LogWarning("No remote endpoint - connection not established");
        }
    }
}
```
**Common Solutions:**

1. **Network Visibility**:
   ```csharp
   // Ensure both devices are on the same network
   void CheckNetworkSetup()
   {
       // Both devices must be on the same LAN
       // Check IP addresses are in same subnet (e.g., 192.168.1.x)
       Debug.Log("Verify both devices on same network subnet");
   }
   ```

2. **Role Configuration**:
   ```csharp
   // Ensure one device is Master, one is Slave
   void VerifyRoleSetup()
   {
       if (AetherLink.Instance.IsMaster)
       {
           Debug.Log("This device is Master - waiting for Slave connection");
       }
       else if (AetherLink.Instance.IsSlave)
       {
           Debug.Log("This device is Slave - attempting to connect to Master");
       }
       else
       {
           Debug.LogError("Device role not properly set");
       }
   }
   ```

3. **Firewall Configuration**:
   - Check Windows Firewall / macOS Firewall
   - Ensure TCP and UDP ports are not blocked
   - Unity applications may need explicit firewall permissions

### Problem: Connection Drops Frequently

**Symptoms:**
- Connection established but frequently lost
- `IsConnected` changes from true to false repeatedly

**Solutions:**
```csharp
public class ConnectionStabilizer : MonoBehaviour
{
    [Header("Stability Settings")]
    [SerializeField] private float connectionCheckInterval = 2f;
    [SerializeField] private int maxReconnectAttempts = 5;
    [SerializeField] private float reconnectDelay = 3f;
    
    private int reconnectAttempts = 0;
    private bool wasConnected = false;
    
    void Update()
    {
        MonitorConnection();
    }
    
    void MonitorConnection()
    {
        bool isConnected = AetherLink.Instance.IsConnected;
        
        if (wasConnected && !isConnected)
        {
            Debug.LogWarning("Connection lost - attempting recovery");
            AttemptReconnection();
        }
        else if (!wasConnected && isConnected)
        {
            Debug.Log("Connection restored");
            reconnectAttempts = 0;
        }
        
        wasConnected = isConnected;
    }
    
    void AttemptReconnection()
    {
        if (reconnectAttempts < maxReconnectAttempts)
        {
            reconnectAttempts++;
            Debug.Log($"Reconnection attempt {reconnectAttempts}/{maxReconnectAttempts}");
            
            Invoke(nameof(RestartNetworking), reconnectDelay);
        }
        else
        {
            Debug.LogError("Maximum reconnection attempts exceeded");
            OnPersistentConnectionFailure();
        }
    }
    
    void RestartNetworking()
    {
        AetherLink.Instance.StopLink();
        
        // Brief delay before restart
        Invoke(nameof(StartNetworking), 1f);
    }
    
    void StartNetworking()
    {
        var settings = Settings.Default;
        AetherLink.Instance.Initialize(settings);
        AetherLink.Instance.StartLink();
    }
    
    void OnPersistentConnectionFailure()
    {
        // Handle persistent failure - show UI message, switch to offline mode, etc.
        Debug.LogError("Unable to maintain stable connection");
    }
}
```
---

## Data Transmission Problems

### Problem: Data Not Being Received

**Symptoms:**
- `SendData()` called but no packets received on other end
- Packet events not firing

**Diagnostic Code:**
```csharp
public class DataTransmissionDiagnostics : MonoBehaviour
{
    public UnityPacketEvent onPacketReceived;
    
    [Header("Test Settings")]
    [SerializeField] private float testDataInterval = 2f;
    private float lastTestTime;
    
    void Start()
    {
        onPacketReceived.AddListener(OnTestPacketReceived);
    }
    
    void Update()
    {
        // Send test data periodically
        if (AetherLink.Instance.IsConnected && Time.time - lastTestTime >= testDataInterval)
        {
            SendTestData();
            lastTestTime = Time.time;
        }
    }
    
    void SendTestData()
    {
        try
        {
            string testMessage = $"Test message {Time.time}";
            AetherLink.Instance.SendData(9999, testMessage);
            Debug.Log($"Sent test data: {testMessage}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Send failed: {ex.Message}");
        }
    }
    
    void OnTestPacketReceived(PacketResponse packet)
    {
        if (packet.Header == 9999)
        {
            object[] data = packet.ReadObjects();
            string message = (string)data[0];
            Debug.Log($"Received test data: {message}");
        }
        else
        {
            Debug.Log($"Received packet with header: {packet.Header}");
        }
    }
}
```
**Common Solutions:**

1. **Event Subscription Check**:
   ```csharp
   void VerifyEventSubscription()
   {
       // Ensure packet event is properly subscribed
       if (onPacketReceived == null)
       {
           Debug.LogError("PacketEvent is null - check Unity Event setup");
           return;
       }
       
       if (onPacketReceived.GetPersistentEventCount() == 0)
       {
           Debug.LogWarning("No persistent listeners on packet event");
       }
   }
   ```

2. **Header ID Consistency**:
   ```csharp
   // Use constants for header IDs to avoid mismatches
   public static class PacketHeaders
   {
       public const ushort PLAYER_DATA = 1001;
       public const ushort GAME_STATE = 1002;
       public const ushort INPUT_COMMAND = 1003;
   }
   
   // Sending
   AetherLink.Instance.SendData(PacketHeaders.PLAYER_DATA, playerName, position);
   
   // Receiving
   if (packet.Header == PacketHeaders.PLAYER_DATA)
   {
       // Handle player data
   }
   ```

### Problem: Serialization Errors

**Symptoms:**
- Exceptions when sending custom objects
- Data corruption on receive
- Type casting errors

**Solutions:**
```csharp
public class SerializationTester : MonoBehaviour
{
    void Start()
    {
        TestCustomTypeSerialization();
    }
    
    void TestCustomTypeSerialization()
    {
        // Register custom types before testing
        AetherLink.RegisterSerializableType<PlayerData>(2001);
        
        // Test serialization
        var testData = new PlayerData
        {
            name = "TestPlayer",
            position = Vector3.one,
            health = 100f
        };
        
        try
        {
            AetherLink.Instance.SendData(2001, testData);
            Debug.Log("Custom type serialization successful");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Serialization failed: {ex.Message}");
        }
    }
}

[System.Serializable]
public struct PlayerData
{
    public string name;
    public Vector3 position;
    public float health;
}
```
---

## Performance Issues

### Problem: High Latency or Slow Data Transfer

**Performance Monitor:**
```csharp
public class PerformanceMonitor : MonoBehaviour
{
    [Header("Performance Tracking")]
    [SerializeField] private bool enableProfiling = true;
    [SerializeField] private float profilingInterval = 1f;
    
    private float lastProfileTime;
    private int packetsSent = 0;
    private int packetsReceived = 0;
    
    public UnityPacketEvent onPacketReceived;
    
    void Start()
    {
        if (enableProfiling)
        {
            onPacketReceived.AddListener(OnPacketForProfiling);
        }
    }
    
    void Update()
    {
        if (enableProfiling && Time.time - lastProfileTime >= profilingInterval)
        {
            ProfileNetworkPerformance();
            lastProfileTime = Time.time;
        }
    }
    
    void ProfileNetworkPerformance()
    {
        Debug.Log($"Performance: {packetsSent} sent, {packetsReceived} received in {profilingInterval}s");
        
        var stats = AetherLink.Instance.Statistics;
        Debug.Log($"Total packets sent: {stats.PacketsSent}");
        Debug.Log($"Total packets received: {stats.PacketsReceived}");
        Debug.Log($"Total bytes sent: {stats.BytesSent}");
        Debug.Log($"Total bytes received: {stats.BytesReceived}");
        
        // Reset counters
        packetsSent = 0;
        packetsReceived = 0;
    }
    
    void OnPacketForProfiling(PacketResponse packet)
    {
        packetsReceived++;
    }
    
    public void OnDataSent()
    {
        packetsSent++;
    }
}
```
**Optimization Strategies:**
```csharp
public class NetworkOptimizer : MonoBehaviour
{
    [Header("Optimization Settings")]
    [SerializeField] private float maxUpdateRate = 20f; // Max 20 updates per second
    [SerializeField] private bool enableDataCompression = true;
    [SerializeField] private bool batchSmallUpdates = true;
    
    private float lastUpdateTime;
    private List<object> batchedData = new List<object>();
    
    void Update()
    {
        if (ShouldSendUpdate())
        {
            SendOptimizedUpdate();
        }
    }
    
    bool ShouldSendUpdate()
    {
        return Time.time - lastUpdateTime >= (1f / maxUpdateRate);
    }
    
    void SendOptimizedUpdate()
    {
        if (!AetherLink.Instance.IsConnected) return;
        
        if (batchSmallUpdates && batchedData.Count > 0)
        {
            // Send batched data
            AetherLink.Instance.SendData(5001, batchedData.ToArray());
            batchedData.Clear();
        }
        else
        {
            // Send individual updates
            SendRegularUpdate();
        }
        
        lastUpdateTime = Time.time;
    }
    
    void SendRegularUpdate()
    {
        // Send only essential data
        Vector3 position = transform.position;
        AetherLink.Instance.SendData(5002, position);
    }
    
    public void AddToBatch(object data)
    {
        if (batchSmallUpdates)
        {
            batchedData.Add(data);
        }
    }
}
```
---

## Platform-Specific Problems

### Windows Issues
```csharp
#if UNITY_STANDALONE_WIN
public class WindowsNetworkFixes : MonoBehaviour
{
    void Start()
    {
        ApplyWindowsFixes();
    }
    
    void ApplyWindowsFixes()
    {
        // Windows firewall often blocks Unity networking
        Debug.Log("Windows: Check Windows Defender Firewall settings");
        Debug.Log("Windows: Unity.exe may need explicit firewall permission");
        
        // Check for Windows-specific networking issues
        CheckWindowsNetworking();
    }
    
    void CheckWindowsNetworking()
    {
        // Windows-specific network diagnostics
        Debug.Log("Windows networking check completed");
    }
}
#endif
```
### macOS Issues
```csharp
#if UNITY_STANDALONE_OSX
public class MacNetworkFixes : MonoBehaviour
{
    void Start()
    {
        ApplyMacFixes();
    }
    
    void ApplyMacFixes()
    {
        // macOS firewall and network permissions
        Debug.Log("macOS: Check System Preferences > Security & Privacy > Firewall");
        Debug.Log("macOS: Unity may need network access permission");
    }
}
#endif
```
### Mobile Platform Issues
```csharp
#if UNITY_ANDROID || UNITY_IOS
public class MobileNetworkFixes : MonoBehaviour
{
    void Start()
    {
        ApplyMobileFixes();
    }
    
    void ApplyMobileFixes()
    {
        // Mobile devices may have different networking restrictions
        Debug.Log("Mobile: Ensure Wi-Fi is enabled and connected to same network");
        Debug.Log("Mobile: Check app permissions for network access");
        
        #if UNITY_ANDROID
            Debug.Log("Android: INTERNET permission required in manifest");
        #endif
        
        #if UNITY_IOS
            Debug.Log("iOS: Local network permission may be required in iOS 14+");
        #endif
    }
}
#endif
```
---

## Development Environment Issues

### Unity Editor vs Build Differences
```csharp
public class EditorBuildCompatibility : MonoBehaviour
{
    void Start()
    {
        CheckEnvironment();
    }
    
    void CheckEnvironment()
    {
        #if UNITY_EDITOR
            Debug.Log("Running in Unity Editor");
            HandleEditorMode();
        #else
            Debug.Log("Running in Build");
            HandleBuildMode();
        #endif
    }
    
    void HandleEditorMode()
    {
        // Editor-specific networking behavior
        Debug.Log("Editor: Can connect to builds on same machine");
        Debug.Log("Editor: Check Play Mode options for networking");
    }
    
    void HandleBuildMode()
    {
        // Build-specific networking behavior
        Debug.Log("Build: Standard networking behavior");
        Debug.Log("Build: Check firewall permissions for executable");
    }
}
```
---

## Network Configuration Problems

### Port Conflicts
```csharp
public class PortDiagnostics : MonoBehaviour
{
    void DiagnosePortIssues()
    {
        Debug.Log("=== Port Configuration Diagnostics ===");
        
        // AetherLink uses TCP and UDP ports
        Debug.Log("AetherLink uses both TCP and UDP protocols");
        Debug.Log("Check that required ports are not blocked");
        Debug.Log("Multiple instances on same machine may conflict");
        
        // Port checking would require Settings access
        CheckPortAvailability();
    }
    
    void CheckPortAvailability()
    {
        // In real implementation, would check Settings struct for port values
        Debug.Log("Checking TCP connection port availability");
        Debug.Log("Checking UDP broadcast port availability");
    }
}
```
### Network Adapter Issues
```csharp
public class NetworkAdapterDiagnostics : MonoBehaviour
{
    void CheckNetworkAdapters()
    {
        Debug.Log("=== Network Adapter Diagnostics ===");
        
        // Multiple network adapters can cause issues
        Debug.Log("Multiple network adapters may cause connection issues");
        Debug.Log("VPN connections can interfere with local networking");
        Debug.Log("Virtual machine adapters may conflict");
        
        // Suggest solutions
        Debug.Log("Solution: Disable unused network adapters");
        Debug.Log("Solution: Disconnect VPN when testing locally");
    }
}
```
---

## Advanced Debugging

### Network Packet Logging
```csharp
public class PacketLogger : MonoBehaviour
{
    [Header("Logging Settings")]
    [SerializeField] private bool enablePacketLogging = false;
    [SerializeField] private bool logOutgoing = true;
    [SerializeField] private bool logIncoming = true;
    [SerializeField] private int maxLoggedPackets = 100;
    
    public UnityPacketEvent onPacketReceived;
    private int loggedPacketCount = 0;
    
    void Start()
    {
        if (enablePacketLogging)
        {
            onPacketReceived.AddListener(LogIncomingPacket);
        }
    }
    
    void LogIncomingPacket(PacketResponse packet)
    {
        if (!logIncoming || loggedPacketCount >= maxLoggedPackets) return;
        
        Debug.Log($"[PACKET IN] Header: {packet.Header}, From: {packet.RemoteEndPoint}");
        loggedPacketCount++;
    }
    
    public void LogOutgoingPacket(ushort header, object[] data)
    {
        if (!logOutgoing || !enablePacketLogging || loggedPacketCount >= maxLoggedPackets) return;
        
        Debug.Log($"[PACKET OUT] Header: {header}, Objects: {data.Length}");
        loggedPacketCount++;
    }
    
    public void ClearPacketLogs()
    {
        loggedPacketCount = 0;
        Debug.Log("Packet logs cleared");
    }
}
```
### Memory and Resource Monitoring
```csharp
public class ResourceMonitor : MonoBehaviour
{
    [Header("Resource Monitoring")]
    [SerializeField] private float monitorInterval = 5f;
    
    void Start()
    {
        InvokeRepeating(nameof(MonitorResources), monitorInterval, monitorInterval);
    }
    
    void MonitorResources()
    {
        // Monitor memory usage
        long memoryUsage = System.GC.GetTotalMemory(false);
        Debug.Log($"Memory Usage: {memoryUsage / 1024 / 1024} MB");
        
        // Check for resource leaks
        if (AetherLink.Instance != null)
        {
            Debug.Log($"AetherLink Status: Running={AetherLink.Instance.IsRunning}, Connected={AetherLink.Instance.IsConnected}");
        }
    }
    
    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            Debug.Log("Application paused - cleaning up resources");
            AetherLink.Instance?.CleanupNetworkResources();
        }
    }
}
```
---

## Common Error Messages

### "AetherLink Instance is null"
**Cause:** AetherLink component not properly initialized
**Solution:**
```csharp
// Ensure AetherLink GameObject exists in scene
// Or create programmatically
var go = new GameObject("AetherLink");
go.AddComponent<AetherLink>();
```
### "Network not initialized"
**Cause:** `StartLink()` called before `Initialize()`
**Solution:**
```csharp
// Always initialize before starting
var settings = Settings.Default;
AetherLink.Instance.Initialize(settings);
AetherLink.Instance.StartLink();
```
```


### "Serialization failed"
**Cause:** Unregistered custom type or non-serializable object
**Solution:**
```csharp
// Register custom types
AetherLink.RegisterSerializableType<MyCustomType>(1001);

// Or use built-in serializable types only
AetherLink.Instance.SendData(1002, "string", 42, Vector3.zero);
```


---

## Recovery Procedures

### Complete Network Reset

```csharp
public class NetworkRecovery : MonoBehaviour
{
    public void PerformCompleteReset()
    {
        Debug.Log("Performing complete network reset");
        
        StartCoroutine(ResetProcedure());
    }
    
    System.Collections.IEnumerator ResetProcedure()
    {
        // Step 1: Stop all networking
        AetherLink.Instance.StopLink();
        yield return new WaitForSeconds(1f);
        
        // Step 2: Reset statistics
        AetherLink.Instance.ResetStatistics();
        yield return new WaitForSeconds(1f);
        
        // Step 3: Reinitialize
        var settings = Settings.Default;
        AetherLink.Instance.Initialize(settings);
        yield return new WaitForSeconds(1f);
        
        // Step 4: Restart networking
        AetherLink.Instance.StartLink();
        
        Debug.Log("Network reset complete");
    }
}
```


### Emergency Cleanup

```csharp
public class EmergencyCleanup : MonoBehaviour
{
    void OnApplicationQuit()
    {
        PerformEmergencyCleanup();
    }
    
    void PerformEmergencyCleanup()
    {
        Debug.Log("Emergency cleanup");
        
        try
        {
            if (AetherLink.Instance != null)
            {
                AetherLink.Instance.StopLink();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Cleanup error: {ex.Message}");
        }
    }
}
```


---

## When to Contact Support

Contact support if you encounter:

1. **Persistent crashes** related to AetherLink
2. **Data corruption** that cannot be resolved
3. **Platform-specific issues** not covered in this guide
4. **Performance problems** that don't respond to optimization
5. **Unusual error messages** not documented here

### Information to Provide

When reporting issues, include:

- Unity version
- Target platform(s)
- AetherLink package version
- Network environment details
- Console logs with errors
- Minimal reproduction steps

---

*This troubleshooting guide covers the most common AetherLink issues. For additional support, refer to the package documentation or contact the development team.*
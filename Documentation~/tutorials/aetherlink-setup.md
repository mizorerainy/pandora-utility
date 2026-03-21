# AetherLink Setup Tutorial

This comprehensive guide covers advanced configuration and setup options for the AetherLink networking system, including custom settings, optimization, and troubleshooting.

## Table of Contents

- [Understanding AetherLink Configuration](#understanding-aetherlink-configuration)
- [Settings Structure](#settings-structure)
- [Basic Configuration](#basic-configuration)
- [Advanced Configuration](#advanced-configuration)
- [Network Optimization](#network-optimization)
- [Custom Serialization](#custom-serialization)
- [Editor Integration](#editor-integration)
- [Production Deployment](#production-deployment)
- [Troubleshooting Setup Issues](#troubleshooting-setup-issues)

---

## Understanding AetherLink Configuration

AetherLink is designed for quick 1-to-1 Local Area Network connections using a Master-Slave architecture. The system automatically handles:

- Network discovery on local networks
- Connection establishment between two devices
- Data serialization and packet management
- Connection monitoring and statistics

### Key Principles

- **Singleton Pattern**: AetherLink.Instance provides global access
- **Settings-Driven**: All configuration through the Settings struct
- **Event-Driven**: Communication through Unity Events and packet callbacks
- **Automatic Management**: Minimal manual network resource handling required

---

## Settings Structure

The AetherLink system uses a `Settings` struct for configuration:
```csharp
// Settings is a struct with predefined defaults
var settings = Settings.Default;
```
### Default Configuration

The `Settings.Default` provides a working configuration suitable for most LAN scenarios. It includes:

- Standard network ports
- Appropriate timeouts
- Default buffer sizes
- LAN discovery settings

---

## Basic Configuration

### Simple Setup with Defaults
```csharp
using UnityEngine;
using PandoraUtility.Network;

public class SimpleAetherLinkSetup : MonoBehaviour
{
    void Start()
    {
        // Use default settings for quick setup
        var settings = Settings.Default;
        
        // Initialize the system
        AetherLink.Instance.Initialize(settings);
        
        // Start networking
        AetherLink.Instance.StartLink();
        
        Debug.Log("AetherLink started with default settings");
    }
}
```
### Role-Based Configuration
```csharp
public class RoleBasedNetworkSetup : MonoBehaviour
{
    [Header("Device Configuration")]
    [SerializeField] private bool isMasterDevice = true;
    [SerializeField] private bool autoStart = true;
    
    void Start()
    {
        if (autoStart)
        {
            SetupNetworking();
        }
    }
    
    void SetupNetworking()
    {
        var settings = Settings.Default;
        
        // Configure based on device role
        if (isMasterDevice)
        {
            Debug.Log("Initializing as Master device");
        }
        else
        {
            Debug.Log("Initializing as Slave device");
        }
        
        AetherLink.Instance.Initialize(settings);
        AetherLink.Instance.StartLink();
    }
    
    public void ToggleRole()
    {
        isMasterDevice = !isMasterDevice;
        RestartNetworking();
    }
    
    void RestartNetworking()
    {
        AetherLink.Instance.StopLink();
        SetupNetworking();
    }
}
```
---

## Advanced Configuration

### Custom Settings Configuration
```csharp
public class AdvancedAetherLinkSetup : MonoBehaviour
{
    [Header("Network Settings")]
    [SerializeField] private int customPort = 7777;
    [SerializeField] private float connectionTimeout = 30f;
    [SerializeField] private int maxRetryAttempts = 5;
    
    void Start()
    {
        SetupAdvancedNetworking();
    }
    
    void SetupAdvancedNetworking()
    {
        // Start with default settings
        var settings = Settings.Default;
        
        // Note: Actual Settings struct properties would need to be
        // documented based on the real implementation
        // This is a conceptual example of how custom settings might work
        
        Debug.Log($"Configuring AetherLink with custom settings");
        Debug.Log($"Port: {customPort}, Timeout: {connectionTimeout}s");
        
        AetherLink.Instance.Initialize(settings);
        AetherLink.Instance.StartLink();
    }
}
```
### Environment-Specific Setup
```csharp
public class EnvironmentAwareSetup : MonoBehaviour
{
    void Start()
    {
        var settings = GetSettingsForEnvironment();
        AetherLink.Instance.Initialize(settings);
        AetherLink.Instance.StartLink();
    }
    
    Settings GetSettingsForEnvironment()
    {
        var settings = Settings.Default;
        
        #if UNITY_EDITOR
            Debug.Log("Using development settings");
            // Development-specific configuration
        #elif UNITY_STANDALONE
            Debug.Log("Using standalone settings");
            // Production standalone configuration
        #elif UNITY_ANDROID || UNITY_IOS
            Debug.Log("Using mobile settings");
            // Mobile-specific configuration
        #endif
        
        return settings;
    }
}
```
---

## Network Optimization

### Performance Monitoring
```csharp
public class NetworkPerformanceMonitor : MonoBehaviour
{
    [Header("Monitoring")]
    [SerializeField] private float statsUpdateInterval = 1f;
    [SerializeField] private bool showDebugInfo = true;
    
    private float lastStatsUpdate;
    
    void Update()
    {
        if (showDebugInfo && Time.time - lastStatsUpdate >= statsUpdateInterval)
        {
            UpdateNetworkStats();
            lastStatsUpdate = Time.time;
        }
    }
    
    void UpdateNetworkStats()
    {
        if (AetherLink.Instance.IsConnected)
        {
            var stats = AetherLink.Instance.Statistics;
            DisplayNetworkStats(stats);
        }
    }
    
    void DisplayNetworkStats(NetworkStats stats)
    {
        // Display relevant network statistics
        Debug.Log($"Network Status: Connected to {AetherLink.Instance.RemoteEndPoint}");
        Debug.Log($"Role: {(AetherLink.Instance.IsMaster ? "Master" : "Slave")}");
        
        // Additional stats display based on NetworkStats properties
    }
    
    public void ResetNetworkStats()
    {
        AetherLink.Instance.ResetStatistics();
        Debug.Log("Network statistics reset");
    }
}
```
### Connection Quality Management
```csharp
public class ConnectionQualityManager : MonoBehaviour
{
    [Header("Quality Settings")]
    [SerializeField] private float connectionCheckInterval = 5f;
    [SerializeField] private int maxReconnectAttempts = 3;
    
    private int reconnectAttempts = 0;
    private float lastConnectionCheck;
    
    void Update()
    {
        if (Time.time - lastConnectionCheck >= connectionCheckInterval)
        {
            CheckConnectionQuality();
            lastConnectionCheck = Time.time;
        }
    }
    
    void CheckConnectionQuality()
    {
        if (AetherLink.Instance.IsRunning && !AetherLink.Instance.IsConnected)
        {
            HandleConnectionLoss();
        }
        else if (AetherLink.Instance.IsConnected)
        {
            // Reset reconnect attempts on successful connection
            reconnectAttempts = 0;
        }
    }
    
    void HandleConnectionLoss()
    {
        if (reconnectAttempts < maxReconnectAttempts)
        {
            Debug.Log($"Attempting reconnection ({reconnectAttempts + 1}/{maxReconnectAttempts})");
            AttemptReconnection();
            reconnectAttempts++;
        }
        else
        {
            Debug.LogWarning("Maximum reconnection attempts reached");
            OnConnectionFailed();
        }
    }
    
    void AttemptReconnection()
    {
        // Restart the network link
        AetherLink.Instance.StopLink();
        
        // Brief delay before restart
        Invoke(nameof(RestartNetworking), 2f);
    }
    
    void RestartNetworking()
    {
        AetherLink.Instance.StartLink();
    }
    
    void OnConnectionFailed()
    {
        // Handle persistent connection failure
        Debug.LogError("Unable to establish stable connection");
        // Show UI message to user, switch to offline mode, etc.
    }
}
```
---

## Custom Serialization

### Registering Custom Types
```csharp
public class CustomSerializationSetup : MonoBehaviour
{
    void Start()
    {
        // Initialize AetherLink first
        var settings = Settings.Default;
        AetherLink.Instance.Initialize(settings);
        
        // Register custom serializable types
        RegisterCustomTypes();
        
        // Start networking
        AetherLink.Instance.StartLink();
    }
    
    void RegisterCustomTypes()
    {
        // Register custom data structures with unique type IDs
        // Note: AetherLink's zero-allocation design now works natively with unmanaged structs.
        // You do not need to register primitive wrapper structures manually:
        // AetherLink.Instance.SendData(1001, new PlayerData { ... });
        Debug.Log("Custom serialization types are handled natively.");
    }
}

[System.Serializable]
public struct PlayerData // Must be unmanaged (no objects/strings/arrays)
{
    public int playerId;
    public Vector3 position;
    public Quaternion rotation;
    public float health;
    public int score;
}

[System.Serializable]
public struct GameState
{
    public float gameTime;
    public int currentLevel;
    public bool isPaused;
    public int checkpointFlags; // Using flags instead of arrays
}

[System.Serializable]
public struct InputCommand
{
    public int commandType;
    public Vector2 inputVector;
    public int buttonFlags; // Using bitmasks instead of boolean arrays
    public float timestamp;
}
```
### Advanced Data Handling
```csharp
public class AdvancedDataHandler : MonoBehaviour
{
    public UnityPacketEvent onPacketReceived;
    
    void Start()
    {
        onPacketReceived.AddListener(HandleAdvancedPackets);
    }
    
    void HandleAdvancedPackets(PacketResponse packet)
    {
        switch (packet.Header)
        {
            case 1001:
                HandlePlayerData(packet);
                break;
            case 1002:
                HandleGameState(packet);
                break;
            case 1003:
                HandleInputCommand(packet);
                break;
        }
    }
    
    void HandlePlayerData(PacketResponse packet)
    {
        PlayerData playerData = packet.ReadAs<PlayerData>();
        
        Debug.Log($"Player: {playerData.playerId} at {playerData.position}");
        // Update local player representation
    }
    
    void HandleGameState(PacketResponse packet)
    {
        GameState gameState = packet.ReadAs<GameState>();
        
        Debug.Log($"Game Time: {gameState.gameTime}, Level: {gameState.currentLevel}");
        // Update game state
    }
    
    void HandleInputCommand(PacketResponse packet)
    {
        InputCommand input = packet.ReadAs<InputCommand>();
        
        // Process remote input
        ProcessRemoteInput(input);
    }
    
    void ProcessRemoteInput(InputCommand input)
    {
        // Apply remote player input to local simulation
    }
}
```
---

## Editor Integration

### Inspector Configuration
```csharp
[System.Serializable]
public class AetherLinkConfiguration
{
    [Header("Basic Settings")]
    public bool autoStartOnPlay = true;
    public bool isMasterDevice = true;
    
    [Header("Advanced Settings")]
    public bool enableDebugLogging = true;
    public bool showNetworkStats = false;
    public float statsUpdateRate = 1f;
    
    [Header("Connection Settings")]
    public float connectionTimeout = 30f;
    public int maxRetryAttempts = 3;
    public float retryDelay = 2f;
}

public class ConfigurableAetherLink : MonoBehaviour
{
    [SerializeField] private AetherLinkConfiguration config;
    
    void Start()
    {
        if (config.autoStartOnPlay)
        {
            InitializeWithConfig();
        }
    }
    
    public void InitializeWithConfig()
    {
        var settings = Settings.Default;
        // Apply configuration to settings
        
        AetherLink.Instance.Initialize(settings);
        AetherLink.Instance.StartLink();
        
        if (config.enableDebugLogging)
        {
            Debug.Log($"AetherLink initialized with custom configuration");
            Debug.Log($"Master Device: {config.isMasterDevice}");
        }
    }
}
```
### Runtime Controls
```csharp
public class AetherLinkRuntimeControls : MonoBehaviour
{
    [Header("Runtime Controls")]
    [SerializeField] private KeyCode startKey = KeyCode.S;
    [SerializeField] private KeyCode stopKey = KeyCode.X;
    [SerializeField] private KeyCode statsKey = KeyCode.I;
    [SerializeField] private KeyCode resetKey = KeyCode.R;
    
    void Update()
    {
        HandleKeyboardControls();
    }
    
    void HandleKeyboardControls()
    {
        if (Input.GetKeyDown(startKey))
        {
            StartNetworking();
        }
        
        if (Input.GetKeyDown(stopKey))
        {
            StopNetworking();
        }
        
        if (Input.GetKeyDown(statsKey))
        {
            ShowNetworkInfo();
        }
        
        if (Input.GetKeyDown(resetKey))
        {
            ResetNetworking();
        }
    }
    
    void StartNetworking()
    {
        if (!AetherLink.Instance.IsRunning)
        {
            var settings = Settings.Default;
            AetherLink.Instance.Initialize(settings);
            AetherLink.Instance.StartLink();
            Debug.Log("Network started manually");
        }
    }
    
    void StopNetworking()
    {
        if (AetherLink.Instance.IsRunning)
        {
            AetherLink.Instance.StopLink();
            Debug.Log("Network stopped manually");
        }
    }
    
    void ShowNetworkInfo()
    {
        var aetherLink = AetherLink.Instance;
        Debug.Log($"=== AetherLink Status ===");
        Debug.Log($"Running: {aetherLink.IsRunning}");
        Debug.Log($"Connected: {aetherLink.IsConnected}");
        Debug.Log($"Mode: {aetherLink.CurrentMode}");
        Debug.Log($"Remote: {aetherLink.RemoteEndPoint}");
    }
    
    void ResetNetworking()
    {
        AetherLink.Instance.StopLink();
        AetherLink.Instance.ResetStatistics();
        
        // Restart after brief delay
        Invoke(nameof(StartNetworking), 1f);
        Debug.Log("Network reset");
    }
}
```
---

## Production Deployment

### Build Configuration
```csharp
public class ProductionAetherLinkSetup : MonoBehaviour
{
    void Start()
    {
        SetupForProduction();
    }
    
    void SetupForProduction()
    {
        var settings = GetProductionSettings();
        
        // Disable debug features in production
        #if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            // Production-only settings
            Debug.Log("AetherLink: Production mode");
        #endif
        
        AetherLink.Instance.Initialize(settings);
        AetherLink.Instance.StartLink();
    }
    
    Settings GetProductionSettings()
    {
        var settings = Settings.Default;
        
        // Configure for production environment
        // - Optimized timeouts
        // - Reduced logging
        // - Performance-focused settings
        
        return settings;
    }
}
```
### Platform-Specific Settings
```csharp
public class PlatformSpecificSetup : MonoBehaviour
{
    void Start()
    {
        var settings = GetPlatformSettings();
        AetherLink.Instance.Initialize(settings);
        AetherLink.Instance.StartLink();
    }
    
    Settings GetPlatformSettings()
    {
        var settings = Settings.Default;
        
        #if UNITY_STANDALONE_WIN
            // Windows-specific networking settings
        #elif UNITY_STANDALONE_OSX
            // macOS-specific networking settings  
        #elif UNITY_STANDALONE_LINUX
            // Linux-specific networking settings
        #elif UNITY_ANDROID
            // Android-specific networking settings
        #elif UNITY_IOS
            // iOS-specific networking settings
        #endif
        
        return settings;
    }
}
```
---

## Troubleshooting Setup Issues

### Common Setup Problems

**AetherLink Instance is null**
```csharp
void SafeAetherLinkAccess()
{
    if (AetherLink.Instance == null)
    {
        Debug.LogError("AetherLink Instance is null - check GameObject setup");
        return;
    }
    
    // Safe to use AetherLink.Instance
}
```
**Settings not applying correctly**
```csharp
void ValidateSettings()
{
    var settings = Settings.Default;
    
    // Verify settings before initialization
    Debug.Log("Validating AetherLink settings...");
    
    try
    {
        AetherLink.Instance.Initialize(settings);
        Debug.Log("Settings validated successfully");
    }
    catch (System.Exception ex)
    {
        Debug.LogError($"Settings validation failed: {ex.Message}");
    }
}
```
### Debugging Connection Issues
```csharp
public class ConnectionDebugger : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(MonitorConnection());
    }
    
    System.Collections.IEnumerator MonitorConnection()
    {
        while (true)
        {
            LogConnectionState();
            yield return new WaitForSeconds(2f);
        }
    }
    
    void LogConnectionState()
    {
        var aetherLink = AetherLink.Instance;
        
        if (!aetherLink.IsRunning)
        {
            Debug.LogWarning("AetherLink is not running");
        }
        else if (!aetherLink.IsConnected)
        {
            Debug.LogWarning("AetherLink running but not connected");
        }
        else
        {
            Debug.Log($"Connected as {aetherLink.CurrentMode} to {aetherLink.RemoteEndPoint}");
        }
    }
}
```
### Resource Cleanup
```csharp
public class AetherLinkCleanup : MonoBehaviour
{
    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            // Clean up on pause
            CleanupResources();
        }
        else
        {
            // Reinitialize on resume
            ReinitializeNetwork();
        }
    }
    
    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            CleanupResources();
        }
    }
    
    void OnDestroy()
    {
        CleanupResources();
    }
    
    void CleanupResources()
    {
        if (AetherLink.Instance != null)
        {
            AetherLink.Instance.CleanupNetworkResources();
            AetherLink.Instance.StopLink();
        }
    }
    
    void ReinitializeNetwork()
    {
        var settings = Settings.Default;
        AetherLink.Instance.Initialize(settings);
        AetherLink.Instance.StartLink();
    }
}
```
---

## Next Steps

After completing this setup tutorial:

1. **Test your configuration** in different network environments
2. **Implement custom data types** for your specific application needs
3. **Monitor performance** and optimize settings as needed
4. **Review [Basic Networking Tutorial](basic-networking.md)** for usage patterns
5. **Explore [Examples](../examples/)** for advanced implementation scenarios

---

## Best Practices Summary

1. **Always use Settings.Default** as your starting point
2. **Initialize before starting** the network link
3. **Register custom types early** in the initialization process
4. **Monitor connection state** and handle disconnections gracefully
5. **Clean up resources** properly when stopping or destroying components
6. **Test on target platforms** to validate platform-specific behavior
7. **Use appropriate timeouts** for your network environment
8. **Implement reconnection logic** for robust applications

---

*This tutorial covered comprehensive AetherLink setup and configuration. For specific implementation examples, refer to the examples documentation and API reference.*
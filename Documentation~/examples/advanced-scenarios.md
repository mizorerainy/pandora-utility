# Advanced Scenarios Tutorial

This tutorial covers sophisticated AetherLink usage patterns for complex applications. Learn about async streams, managed handlers, advanced connection management, and building robust networked systems.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Async Stream API](#async-stream-api)
- [Managed Event Handlers](#managed-event-handlers)
- [Advanced Connection Management](#advanced-connection-management)
- [Request-Response Patterns](#request-response-patterns)
- [State Synchronization](#state-synchronization)
- [Error Recovery and Resilience](#error-recovery-and-resilience)
- [Performance Optimization](#performance-optimization)
- [Complex Application Architectures](#complex-application-architectures)
- [Real-World Examples](#real-world-examples)

---

## Prerequisites

Before diving into advanced scenarios, ensure you have:

- Completed the [AetherLink Setup Tutorial](../tutorials/aetherlink-setup.md)
- Understanding of [Basic Networking](../tutorials/basic-networking.md)
- UniTask package installed (for async stream features)
- C# async/await knowledge
- Familiarity with cancellation tokens

### UniTask Integration

AetherLink's advanced features require the UniTask package:
```
json
// In manifest.json or Package Manager
"com.cysharp.unitask": "2.3.3"
```
---

## Async Stream API

The async stream API provides reactive, composable event handling:

### Basic Stream Consumption
```csharp
using Cysharp.Threading.Tasks;
using System.Threading;

// Define zero-allocation payload structs
public struct HandshakeData { public bool isReady; }
public struct PlayerDataUpdate { public int playerId; public float health; }
public struct GameStateUpdate { public int currentLevel; }

public class AsyncStreamExample : MonoBehaviour
{
    private CancellationTokenSource cancellationSource;

    async void Start()
    {
        cancellationSource = new CancellationTokenSource();
        
        // Start consuming connection events
        ConsumeConnectionEvents(cancellationSource.Token).Forget();
        ConsumeDataEvents(cancellationSource.Token).Forget();
        
        // Setup and start AetherLink
        SetupNetworking();
    }
    
    void OnDestroy()
    {
        cancellationSource?.Cancel();
        cancellationSource?.Dispose();
    }
    
    async UniTaskVoid ConsumeConnectionEvents(CancellationToken token)
    {
        try
        {
            await foreach (var endpoint in AetherLink.Instance.OnConnected().WithCancellation(token))
            {
                Debug.Log($"Connected to: {endpoint}");
                await OnDeviceConnected(endpoint);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation token is triggered
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Connection stream error: {ex.Message}");
        }
    }
    
    async UniTaskVoid ConsumeDataEvents(CancellationToken token)
    {
        try
        {
            await foreach (var packet in AetherLink.Instance.OnDataReceived().WithCancellation(token))
            {
                await ProcessPacket(packet);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation token is triggered
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Data stream error: {ex.Message}");
        }
    }
    
    async UniTask OnDeviceConnected(IPEndPoint endpoint)
    {
        // Perform async initialization after connection
        await UniTask.Delay(100); // Brief delay
        
        // Send initial handshake using unmanaged struct
        AetherLink.Instance.SendData(5001, new HandshakeData { isReady = true });
    }
    
    async UniTask ProcessPacket(PacketResponse packet)
    {
        // Process packets asynchronously
        switch (packet.Header)
        {
            case 5001:
                await HandleHandshake(packet);
                break;
            case 5002:
                await HandleDataUpdate(packet);
                break;
        }
    }
    
    async UniTask HandleHandshake(PacketResponse packet)
    {
        await UniTask.Delay(50); // Simulate processing time
        Debug.Log("Handshake received - connection established");
    }
    
    async UniTask HandleDataUpdate(PacketResponse packet)
    {
        // Simulate async processing
        await UniTask.Yield();
        
        // Read directly as an unmanaged struct with zero allocation
        PlayerDataUpdate data = packet.ReadAs<PlayerDataUpdate>();
        // Process data...
    }
}
```
### Stream Filtering and Composition
```csharp
using Cysharp.Threading.Tasks.Linq;

public class StreamFilteringExample : MonoBehaviour
{
async void Start()
{
var cancellationToken = this.GetCancellationTokenOnDestroy();

        // Filter specific packet types
        await AetherLink.Instance.OnDataReceived()
            .Where(packet => packet.Header >= 6000 && packet.Header < 7000)
            .ForEachAsync(HandleGameplayPacket, cancellationToken);
    }
    
    void HandleGameplayPacket(PacketResponse packet)
    {
        Debug.Log($"Gameplay packet received: {packet.Header}");
    }
}

public class StreamTransformExample : MonoBehaviour
{
async void Start()
{
var cancellationToken = this.GetCancellationTokenOnDestroy();

        // Transform and batch packets
        await AetherLink.Instance.OnDataReceived()
            .Where(p => p.Header == 6001)
            .Select(p => ExtractPlayerPosition(p))
            .Buffer(5) // Batch 5 positions
            .ForEachAsync(ProcessPositionBatch, cancellationToken);
    }
    
    Vector3 ExtractPlayerPosition(PacketResponse packet)
    {
        // Vector3 is an unmanaged struct, so it can be deserialized seamlessly
        return packet.ReadAs<Vector3>();
    }
    
    void ProcessPositionBatch(Vector3[] positions)
    {
        Debug.Log($"Processing batch of {positions.Length} positions");
        // Update UI, interpolate movement, etc.
    }
}
```
---

## Managed Event Handlers

Managed handlers provide automatic error handling and lifetime management:

### Safe Handler Registration
```csharp
public class ManagedHandlerExample : MonoBehaviour
{
void Start()
{
var token = this.GetCancellationTokenOnDestroy();

        // Register safe, managed handlers
        AetherLink.Instance.RegisterConnectionHandler(OnConnectionEstablished, token);
        AetherLink.Instance.RegisterDisconnectionHandler(OnConnectionLost, token);
        AetherLink.Instance.RegisterDataHandler(OnAnyDataReceived, token);
        
        // Register handler for specific packet type
        AetherLink.Instance.RegisterDataHandler(7001, OnPlayerDataReceived, token);
        AetherLink.Instance.RegisterDataHandler(7002, OnGameStateReceived, token);
    }
    
    void OnConnectionEstablished(IPEndPoint endpoint)
    {
        Debug.Log($"Safely connected to: {endpoint}");
        
        // Handler errors are automatically caught and logged
        if (Random.value < 0.1f) // Simulate occasional error
        {
            throw new System.Exception("Simulated connection handler error");
        }
        
        StartGameplaySession();
    }
    
    void OnConnectionLost()
    {
        Debug.Log("Connection lost - cleanup in progress");
        EndGameplaySession();
    }
    
    void OnAnyDataReceived(PacketResponse packet)
    {
        // This handler receives ALL packets
        UpdateNetworkActivity();
    }
    
    void OnPlayerDataReceived(PacketResponse packet)
    {
        // This handler only receives packets with header 7001
        PlayerDataUpdate data = packet.ReadAs<PlayerDataUpdate>();
        ProcessPlayerData(data);
    }
    
    void OnGameStateReceived(PacketResponse packet)
    {
        // This handler only receives packets with header 7002
        GameStateUpdate data = packet.ReadAs<GameStateUpdate>();
        UpdateGameState(data);
    }
    
    void StartGameplaySession()
    {
        Debug.Log("Starting gameplay session");
    }
    
    void EndGameplaySession()
    {
        Debug.Log("Ending gameplay session");
    }
    
    void UpdateNetworkActivity()
    {
        // Update network activity indicator
    }
    
    void ProcessPlayerData(PlayerDataUpdate data)
    {
        // Process player-specific data
    }
    
    void UpdateGameState(GameStateUpdate data)
    {
        // Update global game state
    }
}
```
---

## Advanced Connection Management

### Intelligent Reconnection System

```csharp
public class AdvancedConnectionManager : MonoBehaviour
{
    [Header("Reconnection Settings")]
    [SerializeField] private int maxReconnectAttempts = 10;
    [SerializeField] private float baseReconnectDelay = 1f;
    [SerializeField] private float maxReconnectDelay = 30f;
    [SerializeField] private bool exponentialBackoff = true;
    
    private int reconnectAttempts = 0;
    private bool isReconnecting = false;
    private CancellationTokenSource reconnectCancellation;
    
    async void Start()
    {
        var token = this.GetCancellationTokenOnDestroy();
        
        // Monitor connection state
        MonitorConnectionState(token).Forget();
        
        // Initial connection
        await EstablishConnection();
    }
    
    async UniTaskVoid MonitorConnectionState(CancellationToken token)
    {
        try
        {
            // Monitor disconnection events
            await foreach (var _ in AetherLink.Instance.OnDisconnected().WithCancellation(token))
            {
                Debug.LogWarning("Connection lost - initiating recovery");
                await HandleDisconnection();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when destroyed
        }
    }
    
    async UniTask EstablishConnection()
    {
        var settings = Settings.Default;
        settings.LinkMode = Mode.Slave; // Example as slave
        
        AetherLink.Instance.Initialize(settings);
        AetherLink.Instance.StartLink();
        
        Debug.Log("Attempting initial connection...");
    }
    
    async UniTask HandleDisconnection()
    {
        if (isReconnecting) return;
        
        isReconnecting = true;
        reconnectCancellation?.Cancel();
        reconnectCancellation = new CancellationTokenSource();
        
        try
        {
            bool reconnected = await AttemptReconnection(reconnectCancellation.Token);
            
            if (reconnected)
            {
                reconnectAttempts = 0;
                Debug.Log("Reconnection successful");
            }
            else
            {
                Debug.LogError("Failed to reconnect after maximum attempts");
                OnPermanentConnectionFailure();
            }
        }
        finally
        {
            isReconnecting = false;
        }
    }
    
    async UniTask<bool> AttemptReconnection(CancellationToken token)
    {
        for (int attempt = 0; attempt < maxReconnectAttempts; attempt++)
        {
            float delay = CalculateReconnectDelay(attempt);
            Debug.Log($"Reconnection attempt {attempt + 1}/{maxReconnectAttempts} in {delay:F1} seconds");
            
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: token);
                
                // Restart networking
                AetherLink.Instance.StopLink();
                await UniTask.Delay(500, cancellationToken: token);
                
                var settings = Settings.Default;
                settings.LinkMode = Mode.Slave;
                AetherLink.Instance.Initialize(settings);
                AetherLink.Instance.StartLink();
                
                // Wait for connection with timeout
                bool connected = await WaitForConnection(10f, token);
                if (connected)
                {
                    return true;
                }
                
                reconnectAttempts++;
            }
            catch (OperationCanceledException)
            {
                Debug.Log("Reconnection cancelled");
                return false;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Reconnection attempt failed: {ex.Message}");
            }
        }
        
        return false;
    }
    
    float CalculateReconnectDelay(int attempt)
    {
        if (!exponentialBackoff)
        {
            return baseReconnectDelay;
        }
        
        float delay = baseReconnectDelay * Mathf.Pow(2, attempt);
        return Mathf.Min(delay, maxReconnectDelay);
    }
    
    async UniTask<bool> WaitForConnection(float timeoutSeconds, CancellationToken token)
    {
        var timeoutToken = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeoutToken.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        
        try
        {
            await AetherLink.Instance.OnConnected().FirstAsync(timeoutToken.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
    
    void OnPermanentConnectionFailure()
    {
        Debug.LogError("Permanent connection failure - switching to offline mode");
        // Handle permanent failure (UI notification, offline mode, etc.)
    }
    
    void OnDestroy()
    {
        reconnectCancellation?.Cancel();
        reconnectCancellation?.Dispose();
    }
}
```

---

## Request-Response Patterns

### Async Request-Response System

```csharp
public struct RpcRequestHeader { public int requestId; public ushort requestType; }
public struct PlayerStatsRequest { public int requestId; public ushort requestType; public int playerId; }
public struct PlayerStatsResponse { public int requestId; public int level; public int score; }

public class RequestResponseSystem : MonoBehaviour
{
    private readonly Dictionary<int, TaskCompletionSource<PacketResponse>> pendingRequests = 
        new Dictionary<int, TaskCompletionSource<PacketResponse>>();
    private int requestIdCounter = 1;
    
    async void Start()
    {
        var token = this.GetCancellationTokenOnDestroy();
        
        // Handle responses
        AetherLink.Instance.RegisterDataHandler(8000, HandleResponse, token);
        
        // Example usage
        await TestRequestResponse();
    }
    
    async UniTask TestRequestResponse()
    {
        try
        {
            // Send request and wait for response
            var request = new PlayerStatsRequest { playerId = 123 };
            var response = await SendRequestAsync(8001, request);
            
            var stats = response.ReadAs<PlayerStatsResponse>();
            Debug.Log($"Response received: Level {stats.level}, Score {stats.score}");
        }
        catch (TimeoutException)
        {
            Debug.LogError("Request timed out");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Request failed: {ex.Message}");
        }
    }
    
    async UniTask<PacketResponse> SendRequestAsync<T>(ushort requestType, T requestData) where T : unmanaged
    {
        int requestId = requestIdCounter++;
        var tcs = new TaskCompletionSource<PacketResponse>();
        
        // Store request for response matching
        pendingRequests[requestId] = tcs;
        
        // requestData should already contain the RequestId and RequestType.
        // Send the payload unmanaged struct directly
        AetherLink.Instance.SendData(7999, requestData); // 7999 = request header
        
        // Wait for response with timeout
        var timeoutToken = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            return await tcs.Task.AsUniTask().AttachExternalCancellation(timeoutToken.Token);
        }
        catch (OperationCanceledException)
        {
            pendingRequests.Remove(requestId);
            throw new TimeoutException("Request timed out");
        }
        finally
        {
            timeoutToken.Dispose();
        }
    }
    
    void HandleResponse(PacketResponse packet)
    {
        // First, read the packet as a basic header to extract RequestId
        var header = packet.ReadAs<RpcRequestHeader>();
        
        if (pendingRequests.TryGetValue(header.requestId, out var tcs))
        {
            pendingRequests.Remove(header.requestId);
            tcs.SetResult(packet);
        }
    }
    
    // Server-side request handler (would be on the other device)
    void HandleRequest(PacketResponse packet)
    {
        var header = packet.ReadAs<RpcRequestHeader>();
        
        // Process request based on type
        switch (header.requestType)
        {
            case 8001: // GetPlayerStats
                var req = packet.ReadAs<PlayerStatsRequest>();
                
                // Return stats via unmanaged struct
                var res = new PlayerStatsResponse { 
                    requestId = req.requestId, 
                    level = 25, 
                    score = 1500 
                };
                
                AetherLink.Instance.SendData(8000, res);
                break;
        }
    }
}
```

---

## State Synchronization

### Advanced State Sync System

```csharp
using Unity.Collections;
using System.Runtime.InteropServices;

[System.Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct EnvironmentState
{
    public float gameTime;
    public int currentLevel;
    public bool isPaused;
}

[System.Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct PlayerState
{
    public int playerId;
    public Vector3 position;
    public float health;
    public int score;
    public bool isActive;
}

public class StateSynchronizationManager : MonoBehaviour
{
    [Header("Sync Settings")]
    [SerializeField] private float syncInterval = 0.1f; // 10 FPS
    
    private EnvironmentState currentEnvState;
    private Dictionary<int, PlayerState> currentPlayerStates = new Dictionary<int, PlayerState>();
    
    private float lastSyncTime;
    
    async void Start()
    {
        var token = this.GetCancellationTokenOnDestroy();
        
        // Handle incoming state updates
        AetherLink.Instance.RegisterDataHandler(9001, OnEnvStateReceived, token);
        AetherLink.Instance.RegisterDataHandler(9002, OnPlayerStateReceived, token);
        
        // Start sync loop if master
        if (AetherLink.Instance.IsMaster)
        {
            SyncLoop(token).Forget();
        }
    }
    
    async UniTaskVoid SyncLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await UniTask.WaitUntil(() => AetherLink.Instance.IsConnected, cancellationToken: token);
            
            if (Time.time - lastSyncTime >= syncInterval)
            {
                UpdateAndSendState();
                lastSyncTime = Time.time;
            }
            
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
    }
    
    void UpdateAndSendState()
    {
        // 1. Send Environment State
        currentEnvState.gameTime = Time.time;
        currentEnvState.currentLevel = GameManager.Instance.CurrentLevel;
        currentEnvState.isPaused = GameManager.Instance.IsPaused;
        
        AetherLink.Instance.SendData(9001, currentEnvState);
        
        // 2. Send Player States individually to maintain zero-allocation
        var players = FindObjectsOfType<PlayerController>();
        foreach (var player in players)
        {
            var pState = new PlayerState
            {
                playerId = player.PlayerId,
                position = player.transform.position,
                health = player.Health,
                score = player.Score,
                isActive = player.IsActive
            };
            
            AetherLink.Instance.SendData(9002, pState);
        }
    }
    
    void OnEnvStateReceived(PacketResponse packet)
    {
        // Zero-allocation deserialization
        var state = packet.ReadAs<EnvironmentState>();
        GameManager.Instance.SetGameTime(state.gameTime);
        GameManager.Instance.SetCurrentLevel(state.currentLevel);
        GameManager.Instance.SetPauseState(state.isPaused);
    }
    
    void OnPlayerStateReceived(PacketResponse packet)
    {
        // Zero-allocation deserialization
        var pState = packet.ReadAs<PlayerState>();
        currentPlayerStates[pState.playerId] = pState;
        
        // Apply state to local player representations...
        ApplyPlayerState(pState);
    }
    
    void ApplyPlayerState(PlayerState state)
    {
        var player = GetPlayerById(state.playerId);
        if (player != null)
        {
            // Update components...
            player.transform.position = state.position;
            player.SetHealth(state.health);
        }
    }
    
    PlayerController GetPlayerById(int id) { return null; /* Implementation details omitted */ }
}
```

---

## Error Recovery and Resilience

### Robust Error Handling System

```csharp
public class ErrorRecoveryManager : MonoBehaviour
{
    [Header("Error Recovery Settings")]
    [SerializeField] private int maxConsecutiveErrors = 5;
    [SerializeField] private float errorResetTime = 30f;
    [SerializeField] private bool enableAutomaticRecovery = true;
    
    private int consecutiveErrors = 0;
    private float lastErrorTime = 0f;
    private readonly Queue<string> recentErrors = new Queue<string>();
    
    async void Start()
    {
        var token = this.GetCancellationTokenOnDestroy();
        
        // Monitor for various error conditions
        MonitorNetworkHealth(token).Forget();
        SetupErrorHandlers();
    }
    
    void SetupErrorHandlers()
    {
        // Wrap existing handlers with error recovery
        var token = this.GetCancellationTokenOnDestroy();
        AetherLink.Instance.RegisterDataHandler(packet => 
        {
            try
            {
                HandleDataWithRecovery(packet);
            }
            catch (System.Exception ex)
            {
                HandleError($"Data processing error: {ex.Message}", ex);
            }
        }, token);
    }
    
    async UniTaskVoid MonitorNetworkHealth(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await UniTask.Delay(5000, cancellationToken: token);
                
                // Reset error count if enough time has passed
                if (Time.time - lastErrorTime > errorResetTime)
                {
                    consecutiveErrors = 0;
                }
                
                // Check for network health issues
                if (AetherLink.Instance.IsRunning && !AetherLink.Instance.IsConnected)
                {
                    await CheckConnectionHealth();
                }
                
                // Monitor statistics for anomalies
                CheckNetworkStatistics();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (System.Exception ex)
            {
                HandleError($"Health monitoring error: {ex.Message}", ex);
            }
        }
    }
    
    async UniTask CheckConnectionHealth()
    {
        var stats = AetherLink.Instance.Statistics;
        
        // Check for concerning patterns
        if (stats.CorruptedPackets > 10)
        {
            HandleError("High packet corruption detected");
            if (enableAutomaticRecovery)
            {
                await AttemptRecovery("PacketCorruption");
            }
        }
        
        if (stats.DisconnectionCount > 5)
        {
            HandleError("Frequent disconnections detected");
            if (enableAutomaticRecovery)
            {
                await AttemptRecovery("FrequentDisconnections");
            }
        }
    }
    
    void CheckNetworkStatistics()
    {
        var stats = AetherLink.Instance.Statistics;
        
        // Log statistics for monitoring
        Debug.Log($"Network Stats - Sent: {stats.PacketsSent}, Received: {stats.PacketsReceived}, " +
                  $"Corrupted: {stats.CorruptedPackets}, Disconnections: {stats.DisconnectionCount}");
        
        // Check for anomalies
        float errorRate = (float)stats.CorruptedPackets / Mathf.Max(stats.PacketsReceived, 1);
        if (errorRate > 0.1f) // 10% error rate
        {
            HandleError($"High error rate detected: {errorRate:P}");
        }
    }
    
    void HandleDataWithRecovery(PacketResponse packet)
    {
        // Validate packet integrity
        if (packet.Data == null || packet.Data.Length == 0)
        {
            throw new System.ArgumentException("Invalid packet data");
        }
        
        // Route to appropriate handler and safely parse
        try 
        {
            switch (packet.Header)
            {
                case 9001: // EnvironmentState
                    var envState = packet.ReadAs<EnvironmentState>();
                    HandleGameData(envState);
                    break;
                case 9002: // PlayerState
                    var pState = packet.ReadAs<PlayerState>();
                    HandlePlayerData(pState);
                    break;
                default:
                    Debug.LogWarning($"Unknown packet header: {packet.Header}");
                    break;
            }
            
            // Reset error count on successful processing
            consecutiveErrors = 0;
        }
        catch (System.Exception ex)
        {
            throw new System.ArgumentException($"Data read error: {ex.Message}");
        }
    }
    
    void HandleGameData(EnvironmentState state)
    {
        // Process game data safely
        try
        {
            UpdateGameState(state.gameTime, state.currentLevel, state.isPaused);
        }
        catch (System.Exception ex)
        {
            throw new System.ArgumentException($"Game data error: {ex.Message}");
        }
    }
    
    void HandlePlayerData(PlayerState state)
    {
        // Process with validation...
    }
    
    void HandleError(string message, System.Exception exception = null)
    {
        consecutiveErrors++;
        lastErrorTime = Time.time;
        
        // Store recent errors
        recentErrors.Enqueue($"{Time.time}: {message}");
        if (recentErrors.Count > 10)
        {
            recentErrors.Dequeue();
        }
        
        Debug.LogError($"Network Error ({consecutiveErrors}): {message}");
        if (exception != null)
        {
            Debug.LogException(exception);
        }
        
        // Check if we've exceeded error threshold
        if (consecutiveErrors >= maxConsecutiveErrors)
        {
            OnCriticalErrorThresholdReached();
        }
    }
    
    void OnCriticalErrorThresholdReached()
    {
        Debug.LogError("Critical error threshold reached - initiating emergency recovery");
        
        if (enableAutomaticRecovery)
        {
            EmergencyRecovery().Forget();
        }
        else
        {
            NotifyUser("Critical network errors detected. Manual intervention required.");
        }
    }
    
    async UniTaskVoid EmergencyRecovery()
    {
        try
        {
            Debug.Log("Starting emergency recovery procedure");
            
            // Stop current networking
            AetherLink.Instance.StopLink();
            await UniTask.Delay(2000);
            
            // Reset statistics
            AetherLink.Instance.ResetStatistics();
            
            // Clear error state
            consecutiveErrors = 0;
            recentErrors.Clear();
            
            // Restart networking
            var settings = Settings.Default;
            AetherLink.Instance.Initialize(settings);
            AetherLink.Instance.StartLink();
            
            Debug.Log("Emergency recovery completed");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Emergency recovery failed: {ex.Message}");
            NotifyUser("Automatic recovery failed. Please restart the application.");
        }
    }
    
    async UniTask AttemptRecovery(string recoveryType)
    {
        Debug.Log($"Attempting {recoveryType} recovery");
        
        switch (recoveryType)
        {
            case "PacketCorruption":
                await RecoverFromPacketCorruption();
                break;
            case "FrequentDisconnections":
                await RecoverFromFrequentDisconnections();
                break;
        }
    }
    
    async UniTask RecoverFromPacketCorruption()
    {
        // Reset network buffers and statistics
        AetherLink.Instance.ResetStatistics();
        await UniTask.Delay(1000);
        Debug.Log("Packet corruption recovery completed");
    }
    
    async UniTask RecoverFromFrequentDisconnections()
    {
        // Implement connection stability improvements
        Debug.Log("Implementing connection stability measures");
        await UniTask.Delay(2000);
    }
    
    void NotifyUser(string message)
    {
        // Show UI notification to user
        Debug.LogError($"USER NOTIFICATION: {message}");
        // In real implementation, show UI dialog or notification
    }
    
    void UpdateGameState(float gameTime, int level, bool isPaused)
    {
        // Update game state safely
        Debug.Log($"Game state updated: Time={gameTime}, Level={level}, Paused={isPaused}");
    }
    
    // Public method to get error summary
    public string GetErrorSummary()
    {
        var summary = new System.Text.StringBuilder();
        summary.AppendLine($"Consecutive Errors: {consecutiveErrors}");
        summary.AppendLine($"Last Error Time: {lastErrorTime}");
        summary.AppendLine("Recent Errors:");
        
        foreach (string error in recentErrors)
        {
            summary.AppendLine($"  {error}");
        }
        
        return summary.ToString();
    }
}
```

---

## Performance Optimization

### High-Performance Data Pipeline

```csharp
public class PerformanceOptimizedNetworking : MonoBehaviour
{
    [Header("Performance Settings")]
    [SerializeField] private int maxPacketsPerFrame = 10;
    [SerializeField] private float packetProcessingTimeLimit = 2f; // milliseconds
    [SerializeField] private bool enablePacketBatching = true;
    [SerializeField] private int batchSize = 5;
    
    private readonly Queue<PacketResponse> packetQueue = new Queue<PacketResponse>();
    private readonly List<PacketResponse> processingBatch = new List<PacketResponse>();
    private Stopwatch frameTimer = new Stopwatch();
    
    async void Start()
    {
        var token = this.GetCancellationTokenOnDestroy();
        
        // Setup high-performance packet processing
        SetupOptimizedHandlers(token);
        
        // Start processing loop
        ProcessPacketQueue(token).Forget();
    }
    
    void SetupOptimizedHandlers(CancellationToken token)
    {
        // Queue packets instead of processing immediately
        AetherLink.Instance.RegisterDataHandler(packet => 
        {
            QueuePacketForProcessing(packet);
        }, token);
    }
    
    void QueuePacketForProcessing(PacketResponse packet)
    {
        lock (packetQueue)
        {
            packetQueue.Enqueue(packet);
            
            // Prevent queue overflow
            if (packetQueue.Count > 1000)
            {
                Debug.LogWarning("Packet queue overflow - dropping oldest packets");
                for (int i = 0; i < 100; i++)
                {
                    if (packetQueue.Count > 0)
                        packetQueue.Dequeue();
                }
            }
        }
    }
    
    async UniTaskVoid ProcessPacketQueue(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            frameTimer.Restart();
            int packetsProcessed = 0;
            
            // Process packets within time and count limits
            while (packetsProcessed < maxPacketsPerFrame && 
                   frameTimer.Elapsed.TotalMilliseconds < packetProcessingTimeLimit)
            {
                PacketResponse packet = null;
                
                lock (packetQueue)
                {
                    if (packetQueue.Count > 0)
                        packet = packetQueue.Dequeue();
                }
                
                if (packet == null)
                    break;
                
                try
                {
                    ProcessPacketOptimized(packet);
                    packetsProcessed++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Packet processing error: {ex.Message}");
                }
            }
            
            // Yield control back to Unity
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
    }
    
    void ProcessPacketOptimized(PacketResponse packet)
    {
        // Fast packet routing based on header ranges
        switch (packet.Header)
        {
            case >= 11000 and < 12000:
                ProcessHighFrequencyUpdate(packet);
                break;
            case >= 12000 and < 13000:
                ProcessGameplayEvent(packet);
                break;
            case >= 13000 and < 14000:
                ProcessSystemMessage(packet);
                break;
            default:
                ProcessGenericPacket(packet);
                break;
        }
    }
    
    void ProcessHighFrequencyUpdate(PacketResponse packet)
    {
        // Optimized processing for frequent updates (positions, etc.)
        object[] data = packet.ReadObjects();
        
        // Use object pooling for frequent allocations
        var update = UpdateObjectPool.Get();
        try
        {
            update.ParseFromData(data);
            ApplyUpdate(update);
        }
        finally
        {
            UpdateObjectPool.Return(update);
        }
    }
    
    void ProcessGameplayEvent(PacketResponse packet)
    {
        // Standard processing for gameplay events
        object[] data = packet.ReadObjects();
        HandleGameplayEvent(packet.Header, data);
    }
    
    void ProcessSystemMessage(PacketResponse packet)
    {
        // Lower priority system messages
        object[] data = packet.ReadObjects();
        HandleSystemMessage(data);
    }
    
    void ProcessGenericPacket(PacketResponse packet)
    {
        // Fallback for unknown packet types
        Debug.LogWarning($"Unknown packet type: {packet.Header}");
    }
    
    void ApplyUpdate(PooledUpdate update)
    {
        // Apply update efficiently
    }
    
    void HandleGameplayEvent(ushort header, object[] data)
    {
        // Handle gameplay events
    }
    
    void HandleSystemMessage(object[] data)
    {
        // Handle system messages
    }
}

// Example object pool for frequent allocations
public class UpdateObjectPool
{
    private static readonly Stack<PooledUpdate> pool = new Stack<PooledUpdate>();
    
    public static PooledUpdate Get()
    {
        if (pool.Count > 0)
            return pool.Pop();
        return new PooledUpdate();
    }
    
    public static void Return(PooledUpdate update)
    {
        update.Reset();
        pool.Push(update);
    }
}

public class PooledUpdate
{
    public Vector3 position;
    public float rotation;
    public int playerId;
    
    public void ParseFromData(object[] data)
    {
        playerId = (int)data[0];
        position = (Vector3)data[1];
        rotation = (float)data[2];
    }
    
    public void Reset()
    {
        position = Vector3.zero;
        rotation = 0f;
        playerId = 0;
    }
}
```

---

## Complex Application Architectures

### Modular Network System

```csharp
// Network Module Interface
public interface INetworkModule
{
    string ModuleName { get; }
    ushort[] HandledPacketHeaders { get; }
    UniTask InitializeAsync(CancellationToken token);
    void HandlePacket(PacketResponse packet);
    UniTask ShutdownAsync();
}

// Module Manager
public class NetworkModuleManager : MonoBehaviour
{
    private readonly List<INetworkModule> modules = new List<INetworkModule>();
    private readonly Dictionary<ushort, INetworkModule> headerToModule = 
        new Dictionary<ushort, INetworkModule>();
    
    async void Start()
    {
        var token = this.GetCancellationTokenOnDestroy();
        
        // Register modules
        RegisterModule(new PlayerSyncModule());
        RegisterModule(new ChatModule());
        RegisterModule(new GameStateModule());
        RegisterModule(new InventoryModule());
        
        // Initialize all modules
        await InitializeModules(token);
        
        // Setup packet routing
        AetherLink.Instance.RegisterDataHandler(RoutePacket, token);
    }
    
    void RegisterModule(INetworkModule module)
    {
        modules.Add(module);
        
        foreach (ushort header in module.HandledPacketHeaders)
        {
            headerToModule[header] = module;
        }
        
        Debug.Log($"Registered network module: {module.ModuleName}");
    }
    
    async UniTask InitializeModules(CancellationToken token)
    {
        var initTasks = modules.Select(module => module.InitializeAsync(token));
        await UniTask.WhenAll(initTasks);
        
        Debug.Log("All network modules initialized");
    }
    
    void RoutePacket(PacketResponse packet)
    {
        if (headerToModule.TryGetValue(packet.Header, out INetworkModule module))
        {
            try
            {
                module.HandlePacket(packet);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Module {module.ModuleName} error: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"No module registered for packet header: {packet.Header}");
        }
    }
    
    async void OnDestroy()
    {
        var shutdownTasks = modules.Select(module => module.ShutdownAsync());
        await UniTask.WhenAll(shutdownTasks);
    }
}

// Example Module Implementation
public class PlayerSyncModule : INetworkModule
{
    public string ModuleName => "Player Synchronization";
    
    public ushort[] HandledPacketHeaders => new ushort[] 
    { 
        20001, // Player position
        20002, // Player state
        20003  // Player action
    };
    
    private readonly Dictionary<int, PlayerSyncData> playerData = 
        new Dictionary<int, PlayerSyncData>();
    
    public async UniTask InitializeAsync(CancellationToken token)
    {
        Debug.Log("Initializing Player Sync Module");
        await UniTask.Delay(100, cancellationToken: token);
        // Initialize player tracking, etc.
    }
    
    public void HandlePacket(PacketResponse packet)
    {
        switch (packet.Header)
        {
            case 20001:
                HandlePlayerPosition(packet);
                break;
            case 20002:
                HandlePlayerState(packet);
                break;
            case 20003:
                HandlePlayerAction(packet);
                break;
        }
    }
    
    public struct PlayerPositionUpdate { public int playerId; public Vector3 position; }

    void HandlePlayerPosition(PacketResponse packet)
    {
        var data = packet.ReadAs<PlayerPositionUpdate>();
        
        if (playerData.TryGetValue(data.playerId, out var syncData))
        {
            syncData.UpdatePosition(data.position);
        }
        else
        {
            playerData[data.playerId] = new PlayerSyncData { Position = data.position };
        }
    }
    
    void HandlePlayerState(PacketResponse packet)
    {
        // Handle player state updates
    }
    
    void HandlePlayerAction(PacketResponse packet)
    {
        // Handle player actions
    }
    
    public async UniTask ShutdownAsync()
    {
        Debug.Log("Shutting down Player Sync Module");
        playerData.Clear();
        await UniTask.Yield();
    }
}

public class PlayerSyncData
{
    public Vector3 Position { get; set; }
    public float LastUpdateTime { get; set; }
    
    public void UpdatePosition(Vector3 newPosition)
    {
        Position = newPosition;
        LastUpdateTime = Time.time;
    }
}
```

---

## Real-World Examples

### Multiplayer Game Session Manager

```csharp
public class MultiplayerSessionManager : MonoBehaviour
{
    [Header("Session Settings")]
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private float sessionTimeout = 300f; // 5 minutes
    [SerializeField] private bool enableSpectatorMode = true;
    
    private readonly Dictionary<int, PlayerSession> activePlayers = 
        new Dictionary<int, PlayerSession>();
    private SessionState currentSessionState = SessionState.WaitingForPlayers;
    private float sessionStartTime;
    private CancellationTokenSource sessionCancellation;
    
    public enum SessionState
    {
        WaitingForPlayers,
        InProgress,
        Paused,
        Ended
    }
    
    async void Start()
    {
        sessionCancellation = new CancellationTokenSource();
        var token = sessionCancellation.Token;
        
        // Setup session management
        SetupSessionHandlers(token);
        
        // Start session management loop
        ManageSession(token).Forget();
    }
    
    void SetupSessionHandlers(CancellationToken token)
    {
        // Connection events
        AetherLink.Instance.RegisterConnectionHandler(OnPlayerConnected, token);
        AetherLink.Instance.RegisterDisconnectionHandler(OnPlayerDisconnected, token);
        
        // Session-specific packets
        AetherLink.Instance.RegisterDataHandler(30001, OnPlayerJoinRequest, token);
        AetherLink.Instance.RegisterDataHandler(30002, OnPlayerReadyUpdate, token);
        AetherLink.Instance.RegisterDataHandler(30003, OnGameAction, token);
    }
    
    async UniTaskVoid ManageSession(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            switch (currentSessionState)
            {
                case SessionState.WaitingForPlayers:
                    await HandleWaitingForPlayers(token);
                    break;
                    
                case SessionState.InProgress:
                    await HandleInProgressSession(token);
                    break;
                    
                case SessionState.Paused:
                    await HandlePausedSession(token);
                    break;
                    
                case SessionState.Ended:
                    await HandleEndedSession(token);
                    break;
            }
            
            await UniTask.Delay(1000, cancellationToken: token); // 1 second intervals
        }
    }
    
    async UniTask HandleWaitingForPlayers(CancellationToken token)
    {
        // Check if we have enough players to start
        int readyPlayers = activePlayers.Values.Count(p => p.IsReady);
        
        if (readyPlayers >= 2 && readyPlayers == activePlayers.Count)
        {
            await StartGameSession();
        }
        
        // Send lobby updates
        BroadcastLobbyState();
    }
    
    async UniTask HandleInProgressSession(CancellationToken token)
    {
        float elapsed = Time.time - sessionStartTime;
        
        // Check for session timeout
        if (elapsed > sessionTimeout)
        {
            await EndGameSession("Session timeout");
            return;
        }
        
        // Check for disconnections
        if (activePlayers.Count == 0)
        {
            await EndGameSession("All players disconnected");
            return;
        }
        
        // Send game state updates
        BroadcastGameState();
    }
    
    async UniTask HandlePausedSession(CancellationToken token)
    {
        // Wait for resume conditions
        if (activePlayers.Values.All(p => p.IsReady))
        {
            currentSessionState = SessionState.InProgress;
            BroadcastSessionStateChange();
        }
    }
    
    async UniTask HandleEndedSession(CancellationToken token)
    {
        // Cleanup and prepare for new session
        await UniTask.Delay(10000, cancellationToken: token); // Wait 10 seconds
        
        activePlayers.Clear();
        currentSessionState = SessionState.WaitingForPlayers;
        BroadcastSessionStateChange();
    }
    
    void OnPlayerConnected(IPEndPoint endpoint)
    {
        Debug.Log($"Player connected from: {endpoint}");
        // Wait for join request
    }
    
    void OnPlayerDisconnected()
    {
        Debug.Log("Player disconnected");
        
        // Find and remove disconnected player
        // This is simplified - in practice, you'd need to identify which player
        var playersToRemove = activePlayers.Where(kvp => !IsPlayerStillConnected(kvp.Value))
                                          .Select(kvp => kvp.Key)
                                          .ToList();
        
        foreach (int playerId in playersToRemove)
        {
            activePlayers.Remove(playerId);
            BroadcastPlayerDisconnected(playerId);
        }
        
        // Handle session state based on remaining players
        if (currentSessionState == SessionState.InProgress && activePlayers.Count < 2)
        {
            PauseSession("Insufficient players");
        }
    }
    
    // Define unmanaged structs for communication
    public struct PlayerJoinRequest { public int requestedPlayerId; }
    public struct JoinResponseData { public int playerId; public bool success; }
    public struct PlayerReadyUpdateData { public int playerId; public bool isReady; }
    public struct PlayerGameAction { public int playerId; public int actionType; public int actionData; }

    void OnPlayerJoinRequest(PacketResponse packet)
    {
        var req = packet.ReadAs<PlayerJoinRequest>();
        string playerName = "Player_" + req.requestedPlayerId;
        
        if (activePlayers.Count >= maxPlayers)
        {
            SendJoinResponse(req.requestedPlayerId, false, "Session full");
            return;
        }
        
        if (currentSessionState != SessionState.WaitingForPlayers)
        {
            if (enableSpectatorMode)
            {
                SendJoinResponse(req.requestedPlayerId, true, "Joined as spectator");
            }
            else
            {
                SendJoinResponse(req.requestedPlayerId, false, "Session in progress");
            }
            return;
        }
        
        // Add player to session
        var playerSession = new PlayerSession
        {
            PlayerId = req.requestedPlayerId,
            PlayerName = playerName,
            JoinTime = Time.time,
            IsReady = false,
            IsSpectator = false
        };
        
        activePlayers[req.requestedPlayerId] = playerSession;
        SendJoinResponse(req.requestedPlayerId, true, "Welcome to the session!");
        BroadcastPlayerJoined(playerSession);
        
        Debug.Log($"Player {playerName} (ID: {req.requestedPlayerId}) joined the session");
    }
    
    void OnPlayerReadyUpdate(PacketResponse packet)
    {
        var data = packet.ReadAs<PlayerReadyUpdateData>();
        
        if (activePlayers.TryGetValue(data.playerId, out var playerSession))
        {
            playerSession.IsReady = data.isReady;
            BroadcastPlayerReadyUpdate(data.playerId, data.isReady);
            
            Debug.Log($"Player {playerSession.PlayerName} ready status: {data.isReady}");
        }
    }
    
    void OnGameAction(PacketResponse packet)
    {
        if (currentSessionState != SessionState.InProgress)
            return;
        
        var action = packet.ReadAs<PlayerGameAction>();
        
        // Process and broadcast game action
        ProcessGameAction(action.playerId, action.actionType, action.actionData);
        BroadcastGameAction(action);
    }
    
    async UniTask StartGameSession()
    {
        Debug.Log("Starting game session");
        
        currentSessionState = SessionState.InProgress;
        sessionStartTime = Time.time;
        
        // Initialize game state
        await InitializeGameState();
        
        BroadcastSessionStateChange();
        BroadcastGameStart();
    }
    
    async UniTask EndGameSession(string reason)
    {
        Debug.Log($"Ending game session: {reason}");
        currentSessionState = SessionState.Ended;
        
        var sessionResults = CalculateSessionResults();
        
        BroadcastGameEnd(sessionResults);
        BroadcastSessionStateChange();
    }
    
    void PauseSession(string reason)
    {
        Debug.Log($"Pausing session: {reason}");
        
        currentSessionState = SessionState.Paused;
        BroadcastSessionStateChange();
    }
    
    void SendJoinResponse(int playerId, bool success, string message)
    {
        AetherLink.Instance.SendData(30101, new JoinResponseData { playerId = playerId, success = success });
    }
    
    void BroadcastLobbyState()
    {
        AetherLink.Instance.SendData(30201, currentSessionState);
    }
    
    void BroadcastGameState()
    {
        float elapsedTime = Time.time - sessionStartTime;
        AetherLink.Instance.SendData(30202, elapsedTime);
    }
    
    void BroadcastPlayerJoined(PlayerSession player)
    {
        AetherLink.Instance.SendData(30203, player.PlayerId);
    }
    
    void BroadcastPlayerDisconnected(int playerId)
    {
        AetherLink.Instance.SendData(30204, playerId);
    }
    
    void BroadcastPlayerReadyUpdate(int playerId, bool isReady)
    {
        AetherLink.Instance.SendData(30205, new PlayerReadyUpdateData { playerId = playerId, isReady = isReady });
    }
    
    void BroadcastSessionStateChange()
    {
        AetherLink.Instance.SendData(30206, currentSessionState);
    }
    
    void BroadcastGameStart()
    {
        AetherLink.Instance.SendData(30207, sessionStartTime);
    }
    
    void BroadcastGameEnd(SessionResults results)
    {
        AetherLink.Instance.SendData(30208, results);
    }
    
    void BroadcastGameAction(PlayerGameAction action)
    {
        AetherLink.Instance.SendData(30210, action);
    }
    
    void ProcessGameAction(int playerId, int actionType, int actionData)
    {
        // Process game action logic
        Debug.Log($"Processing action {actionType} from player {playerId}");
    }
    
    async UniTask InitializeGameState()
    {
        // Initialize game-specific state
        await UniTask.Delay(100);
    }
    
    SessionResults CalculateSessionResults()
    {
        return new SessionResults
        {
            Duration = Time.time - sessionStartTime,
            PlayerCount = activePlayers.Count
        };
    }
    
    bool IsPlayerStillConnected(PlayerSession player)
    {
        // In practice, implement actual connection checking
        return AetherLink.Instance.IsConnected;
    }
    
    void OnDestroy()
    {
        sessionCancellation?.Cancel();
        sessionCancellation?.Dispose();
    }
}

[System.Serializable]
public class PlayerSession
{
    public int PlayerId;
    public string PlayerName;
    public float JoinTime;
    public bool IsReady;
    public bool IsSpectator;
    public int Score;
}

[System.Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct SessionResults
{
    public float Duration;
    public int PlayerCount;
}
```

This comprehensive advanced scenarios tutorial demonstrates sophisticated AetherLink usage patterns including async streams, managed handlers, advanced connection management, and building robust networked applications. The examples show real-world patterns that can be adapted for complex multiplayer games and networked applications.


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

- Completed the [Simple Connection Tutorial](simple-connection.md)
- Understanding of [Data Serialization](data-serialization.md)
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
        
        // Send initial handshake
        AetherLink.Instance.SendData(5001, "Ready for synchronization");
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
        object[] data = packet.ReadObjects();
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
        object[] data = packet.ReadObjects();
        return (Vector3)data[0];
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
        object[] data = packet.ReadObjects();
        ProcessPlayerData(data);
    }
    
    void OnGameStateReceived(PacketResponse packet)
    {
        // This handler only receives packets with header 7002
        object[] data = packet.ReadObjects();
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
    
    void ProcessPlayerData(object[] data)
    {
        // Process player-specific data
    }
    
    void UpdateGameState(object[] data)
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
            var response = await SendRequestAsync(8001, "GetPlayerStats", "Player123");
            
            object[] responseData = response.ReadObjects();
            Debug.Log($"Response received: {responseData[0]}");
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
    
    async UniTask<PacketResponse> SendRequestAsync(ushort requestType, params object[] requestData)
    {
        int requestId = requestIdCounter++;
        var tcs = new TaskCompletionSource<PacketResponse>();
        
        // Store request for response matching
        pendingRequests[requestId] = tcs;
        
        // Send request with ID
        object[] dataWithId = new object[requestData.Length + 2];
        dataWithId[0] = requestId;
        dataWithId[1] = requestType;
        Array.Copy(requestData, 0, dataWithId, 2, requestData.Length);
        
        AetherLink.Instance.SendData(7999, dataWithId); // 7999 = request header
        
        // Wait for response with timeout
        var timeoutToken = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            var response = await tcs.Task.AsUniTask().AttachExternalCancellation(timeoutToken.Token);
            return response;
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
        object[] data = packet.ReadObjects();
        if (data.Length < 1) return;
        
        int requestId = (int)data[0];
        if (pendingRequests.TryGetValue(requestId, out var tcs))
        {
            pendingRequests.Remove(requestId);
            tcs.SetResult(packet);
        }
    }
    
    // Server-side request handler (would be on the other device)
    void HandleRequest(PacketResponse packet)
    {
        object[] data = packet.ReadObjects();
        if (data.Length < 2) return;
        
        int requestId = (int)data[0];
        ushort requestType = (ushort)data[1];
        
        // Process request based on type
        string response = ProcessRequest(requestType, data.Skip(2).ToArray());
        
        // Send response
        AetherLink.Instance.SendData(8000, requestId, response);
    }
    
    string ProcessRequest(ushort requestType, object[] requestData)
    {
        switch (requestType)
        {
            case 8001: // GetPlayerStats
                string playerId = (string)requestData[0];
                return $"Stats for {playerId}: Level 25, Score 1500";
            
            default:
                return "Unknown request type";
        }
    }
}
```

---

## State Synchronization

### Advanced State Sync System

```csharp
[System.Serializable]
public class SyncedGameState : IAetherSerializable
{
    public float gameTime;
    public Vector3[] playerPositions;
    public Dictionary<int, PlayerState> playerStates;
    public int currentLevel;
    public bool isPaused;
    
    public SyncedGameState()
    {
        playerStates = new Dictionary<int, PlayerState>();
    }
    
    public void Serialize(BinaryWriter writer)
    {
        writer.Write(gameTime);
        writer.Write(currentLevel);
        writer.Write(isPaused);
        
        // Player positions
        writer.Write(playerPositions?.Length ?? 0);
        if (playerPositions != null)
        {
            foreach (var pos in playerPositions)
            {
                writer.Write(pos.x);
                writer.Write(pos.y);
                writer.Write(pos.z);
            }
        }
        
        // Player states
        writer.Write(playerStates.Count);
        foreach (var kvp in playerStates)
        {
            writer.Write(kvp.Key);
            kvp.Value.Serialize(writer);
        }
    }
    
    public void Deserialize(BinaryReader reader)
    {
        gameTime = reader.ReadSingle();
        currentLevel = reader.ReadInt32();
        isPaused = reader.ReadBoolean();
        
        // Player positions
        int posCount = reader.ReadInt32();
        playerPositions = new Vector3[posCount];
        for (int i = 0; i < posCount; i++)
        {
            playerPositions[i] = new Vector3(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle()
            );
        }
        
        // Player states
        playerStates.Clear();
        int stateCount = reader.ReadInt32();
        for (int i = 0; i < stateCount; i++)
        {
            int playerId = reader.ReadInt32();
            var state = new PlayerState();
            state.Deserialize(reader);
            playerStates[playerId] = state;
        }
    }
}

[System.Serializable]
public struct PlayerState : IAetherSerializable
{
    public float health;
    public int score;
    public bool isActive;
    public string currentWeapon;
    
    public void Serialize(BinaryWriter writer)
    {
        writer.Write(health);
        writer.Write(score);
        writer.Write(isActive);
        writer.Write(currentWeapon ?? string.Empty);
    }
    
    public void Deserialize(BinaryReader reader)
    {
        health = reader.ReadSingle();
        score = reader.ReadInt32();
        isActive = reader.ReadBoolean();
        currentWeapon = reader.ReadString();
    }
}

public class StateSynchronizationManager : MonoBehaviour
{
    [Header("Sync Settings")]
    [SerializeField] private float syncInterval = 0.1f; // 10 FPS
    [SerializeField] private bool isDeltaCompression = true;
    
    private SyncedGameState currentState;
    private SyncedGameState previousState;
    private float lastSyncTime;
    
    async void Start()
    {
        currentState = new SyncedGameState();
        previousState = new SyncedGameState();
        
        var token = this.GetCancellationTokenOnDestroy();
        
        // Handle incoming state updates
        AetherLink.Instance.RegisterDataHandler(9001, OnStateReceived, token);
        
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
                UpdateCurrentState();
                
                if (isDeltaCompression)
                {
                    SendDeltaState();
                }
                else
                {
                    SendFullState();
                }
                
                lastSyncTime = Time.time;
            }
            
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
    }
    
    void UpdateCurrentState()
    {
        // Update state from game objects
        currentState.gameTime = Time.time;
        currentState.currentLevel = GameManager.Instance.CurrentLevel;
        currentState.isPaused = GameManager.Instance.IsPaused;
        
        // Update player positions
        var players = FindObjectsOfType<PlayerController>();
        currentState.playerPositions = players.Select(p => p.transform.position).ToArray();
        
        // Update player states
        currentState.playerStates.Clear();
        foreach (var player in players)
        {
            currentState.playerStates[player.PlayerId] = new PlayerState
            {
                health = player.Health,
                score = player.Score,
                isActive = player.IsActive,
                currentWeapon = player.CurrentWeapon
            };
        }
    }
    
    void SendFullState()
    {
        AetherLink.Instance.SendData(9001, currentState);
        previousState = CloneState(currentState);
    }
    
    void SendDeltaState()
    {
        // Only send if there are meaningful changes
        if (HasSignificantChanges())
        {
            AetherLink.Instance.SendData(9001, currentState);
            previousState = CloneState(currentState);
        }
    }
    
    bool HasSignificantChanges()
    {
        // Check for significant changes
        if (Mathf.Abs(currentState.gameTime - previousState.gameTime) > 0.01f) return true;
        if (currentState.currentLevel != previousState.currentLevel) return true;
        if (currentState.isPaused != previousState.isPaused) return true;
        
        // Check position changes
        if (currentState.playerPositions.Length != previousState.playerPositions.Length) return true;
        
        for (int i = 0; i < currentState.playerPositions.Length; i++)
        {
            if (Vector3.Distance(currentState.playerPositions[i], previousState.playerPositions[i]) > 0.01f)
                return true;
        }
        
        return false;
    }
    
    void OnStateReceived(PacketResponse packet)
    {
        object[] data = packet.ReadObjects();
        var receivedState = (SyncedGameState)data[0];
        
        ApplyState(receivedState);
    }
    
    void ApplyState(SyncedGameState state)
    {
        // Apply received state to game objects
        GameManager.Instance.SetGameTime(state.gameTime);
        GameManager.Instance.SetCurrentLevel(state.currentLevel);
        GameManager.Instance.SetPauseState(state.isPaused);
        
        // Update player positions with interpolation
        var players = FindObjectsOfType<PlayerController>();
        for (int i = 0; i < Mathf.Min(players.Length, state.playerPositions.Length); i++)
        {
            StartCoroutine(InterpolatePosition(players[i], state.playerPositions[i]));
        }
        
        // Update player states
        foreach (var player in players)
        {
            if (state.playerStates.TryGetValue(player.PlayerId, out var playerState))
            {
                player.SetHealth(playerState.health);
                player.SetScore(playerState.score);
                player.SetActive(playerState.isActive);
                player.SetWeapon(playerState.currentWeapon);
            }
        }
    }
    
    IEnumerator InterpolatePosition(PlayerController player, Vector3 targetPosition)
    {
        Vector3 startPosition = player.transform.position;
        float duration = syncInterval;
        float elapsed = 0;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            player.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }
        
        player.transform.position = targetPosition;
    }
    
    SyncedGameState CloneState(SyncedGameState original)
    {
        // Deep clone implementation
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        using (var reader = new BinaryReader(stream))
        {
            original.Serialize(writer);
            stream.Position = 0;
            
            var clone = new SyncedGameState();
            clone.Deserialize(reader);
            return clone;
        }
    }
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
        
        // Process packet with validation
        object[] data = packet.ReadObjects();
        if (data.Length == 0)
        {
            throw new System.InvalidOperationException("No data objects in packet");
        }
        
        // Route to appropriate handler
        switch (packet.Header)
        {
            case 10001:
                HandleGameData(data);
                break;
            case 10002:
                HandlePlayerData(data);
                break;
            default:
                Debug.LogWarning($"Unknown packet header: {packet.Header}");
                break;
        }
        
        // Reset error count on successful processing
        consecutiveErrors = 0;
    }
    
    void HandleGameData(object[] data)
    {
        // Validate data structure
        if (data.Length < 3)
        {
            throw new System.ArgumentException("Insufficient game data");
        }
        
        // Process game data with type checking
        try
        {
            float gameTime = (float)data[0];
            int level = (int)data[1];
            bool isPaused = (bool)data[2];
            
            UpdateGameState(gameTime, level, isPaused);
        }
        catch (System.InvalidCastException ex)
        {
            throw new System.ArgumentException($"Invalid data types in game data: {ex.Message}");
        }
    }
    
    void HandlePlayerData(object[] data)
    {
        // Similar validation and processing for player data
        if (data.Length < 2)
        {
            throw new System.ArgumentException("Insufficient player data");
        }
        
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
    
    void HandlePlayerPosition(PacketResponse packet)
    {
        object[] data = packet.ReadObjects();
        int playerId = (int)data[0];
        Vector3 position = (Vector3)data[1];
        
        if (playerData.TryGetValue(playerId, out var syncData))
        {
            syncData.UpdatePosition(position);
        }
        else
        {
            playerData[playerId] = new PlayerSyncData { Position = position };
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
    
    void OnPlayerJoinRequest(PacketResponse packet)
    {
        object[] data = packet.ReadObjects();
        string playerName = (string)data[0];
        int requestedPlayerId = (int)data[1];
        
        if (activePlayers.Count >= maxPlayers)
        {
            SendJoinResponse(requestedPlayerId, false, "Session full");
            return;
        }
        
        if (currentSessionState != SessionState.WaitingForPlayers)
        {
            if (enableSpectatorMode)
            {
                SendJoinResponse(requestedPlayerId, true, "Joined as spectator");
            }
            else
            {
                SendJoinResponse(requestedPlayerId, false, "Session in progress");
            }
            return;
        }
        
        // Add player to session
        var playerSession = new PlayerSession
        {
            PlayerId = requestedPlayerId,
            PlayerName = playerName,
            JoinTime = Time.time,
            IsReady = false,
            IsSpectator = false
        };
        
        activePlayers[requestedPlayerId] = playerSession;
        SendJoinResponse(requestedPlayerId, true, "Welcome to the session!");
        BroadcastPlayerJoined(playerSession);
        
        Debug.Log($"Player {playerName} (ID: {requestedPlayerId}) joined the session");
    }
    
    void OnPlayerReadyUpdate(PacketResponse packet)
    {
        object[] data = packet.ReadObjects();
        int playerId = (int)data[0];
        bool isReady = (bool)data[1];
        
        if (activePlayers.TryGetValue(playerId, out var playerSession))
        {
            playerSession.IsReady = isReady;
            BroadcastPlayerReadyUpdate(playerId, isReady);
            
            Debug.Log($"Player {playerSession.PlayerName} ready status: {isReady}");
        }
    }
    
    void OnGameAction(PacketResponse packet)
    {
        if (currentSessionState != SessionState.InProgress)
            return;
        
        object[] data = packet.ReadObjects();
        int playerId = (int)data[0];
        string actionType = (string)data[1];
        object actionData = data[2];
        
        // Process and broadcast game action
        ProcessGameAction(playerId, actionType, actionData);
        BroadcastGameAction(playerId, actionType, actionData);
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
        
        // Calculate final scores, stats, etc.
        var sessionResults = CalculateSessionResults();
        
        BroadcastGameEnd(reason, sessionResults);
        BroadcastSessionStateChange();
    }
    
    void PauseSession(string reason)
    {
        Debug.Log($"Pausing session: {reason}");
        
        currentSessionState = SessionState.Paused;
        BroadcastSessionPaused(reason);
        BroadcastSessionStateChange();
    }
    
    void SendJoinResponse(int playerId, bool success, string message)
    {
        AetherLink.Instance.SendData(30101, playerId, success, message);
    }
    
    void BroadcastLobbyState()
    {
        var lobbyData = activePlayers.Values.Select(p => new
        {
            p.PlayerId,
            p.PlayerName,
            p.IsReady,
            p.IsSpectator
        }).ToArray();
        
        AetherLink.Instance.SendData(30201, currentSessionState, lobbyData);
    }
    
    void BroadcastGameState()
    {
        float elapsedTime = Time.time - sessionStartTime;
        float remainingTime = sessionTimeout - elapsedTime;
        
        AetherLink.Instance.SendData(30202, elapsedTime, remainingTime, activePlayers.Count);
    }
    
    void BroadcastPlayerJoined(PlayerSession player)
    {
        AetherLink.Instance.SendData(30203, player.PlayerId, player.PlayerName);
    }
    
    void BroadcastPlayerDisconnected(int playerId)
    {
        AetherLink.Instance.SendData(30204, playerId);
    }
    
    void BroadcastPlayerReadyUpdate(int playerId, bool isReady)
    {
        AetherLink.Instance.SendData(30205, playerId, isReady);
    }
    
    void BroadcastSessionStateChange()
    {
        AetherLink.Instance.SendData(30206, currentSessionState);
    }
    
    void BroadcastGameStart()
    {
        AetherLink.Instance.SendData(30207, sessionStartTime);
    }
    
    void BroadcastGameEnd(string reason, SessionResults results)
    {
        AetherLink.Instance.SendData(30208, reason, results);
    }
    
    void BroadcastSessionPaused(string reason)
    {
        AetherLink.Instance.SendData(30209, reason);
    }
    
    void BroadcastGameAction(int playerId, string actionType, object actionData)
    {
        AetherLink.Instance.SendData(30210, playerId, actionType, actionData);
    }
    
    void ProcessGameAction(int playerId, string actionType, object actionData)
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
            PlayerCount = activePlayers.Count,
            // Add other session statistics
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
    public Dictionary<string, object> CustomData = new Dictionary<string, object>();
}

[System.Serializable]
public struct SessionResults : IAetherSerializable
{
    public float Duration;
    public int PlayerCount;
    public Dictionary<int, int> PlayerScores;
    
    public void Serialize(BinaryWriter writer)
    {
        writer.Write(Duration);
        writer.Write(PlayerCount);
        
        writer.Write(PlayerScores?.Count ?? 0);
        if (PlayerScores != null)
        {
            foreach (var kvp in PlayerScores)
            {
                writer.Write(kvp.Key);
                writer.Write(kvp.Value);
            }
        }
    }
    
    public void Deserialize(BinaryReader reader)
    {
        Duration = reader.ReadSingle();
        PlayerCount = reader.ReadInt32();
        
        int scoreCount = reader.ReadInt32();
        PlayerScores = new Dictionary<int, int>(scoreCount);
        
        for (int i = 0; i < scoreCount; i++)
        {
            int playerId = reader.ReadInt32();
            int score = reader.ReadInt32();
            PlayerScores[playerId] = score;
        }
    }
}
```

This comprehensive advanced scenarios tutorial demonstrates sophisticated AetherLink usage patterns including async streams, managed handlers, advanced connection management, and building robust networked applications. The examples show real-world patterns that can be adapted for complex multiplayer games and networked applications.


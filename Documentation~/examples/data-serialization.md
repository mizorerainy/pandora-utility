# Data Serialization Tutorial

This tutorial covers AetherLink's comprehensive data serialization system, from basic built-in types to advanced custom serialization. You'll learn how to efficiently send and receive complex data structures across the network.

## Table of Contents

- [Understanding AetherLink Serialization](#understanding-aetherlink-serialization)
- [Built-in Type Support](#built-in-type-support)
- [Custom Type Serialization](#custom-type-serialization)
- [Implementing IAetherSerializable](#implementing-iaetherserializable)
- [Advanced Serialization Patterns](#advanced-serialization-patterns)
- [Performance Considerations](#performance-considerations)
- [Best Practices](#best-practices)
- [Common Pitfalls](#common-pitfalls)
- [Examples and Use Cases](#examples-and-use-cases)

---

## Understanding AetherLink Serialization

AetherLink provides a flexible serialization system that handles:

1. **Built-in Types**: Automatic serialization for common C# and Unity types
2. **Custom Types**: Extensible system via `IAetherSerializable` interface
3. **Type Registration**: Static registration system for custom types
4. **Binary Efficiency**: Compact binary representation for network transmission

### Key Concepts

- **Automatic Serialization**: Built-in types are serialized automatically
- **Type IDs**: Custom types require unique identifier registration
- **Order Preservation**: Data is serialized and deserialized in the same order
- **Type Safety**: Runtime type checking prevents data corruption

---

## Built-in Type Support

AetherLink automatically handles serialization for these types:

### Primitive Types
```csharp
// All primitive types are supported
bool isActive = true;
byte health = 100;
sbyte temperature = -5;
short score = 1500;
ushort level = 42;
int playerId = 123456;
uint timestamp = 987654321;
long experience = 999999999L;
ulong bigNumber = 18446744073709551615UL;
float position = 15.75f;
double precision = 3.141592653589793;
string playerName = "Player123";

// Send multiple primitive types
AetherLink.Instance.SendData(1001,
isActive, health, score, playerId, position, playerName);
```
### Unity Types
```csharp
// Unity mathematics and transform types
Vector2 screenPos = new Vector2(100f, 200f);
Vector3 worldPos = new Vector3(1f, 2f, 3f);
Quaternion rotation = Quaternion.Euler(0f, 90f, 0f);

// Send Unity types
AetherLink.Instance.SendData(1002, worldPos, rotation);
```
### Built-in Type Examples
```csharp
public class BuiltInTypeExample : MonoBehaviour
{
    public UnityPacketEvent onPacketReceived;

    void Start()
    {
        onPacketReceived.AddListener(HandleReceivedData);
    }
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && AetherLink.Instance.IsConnected)
        {
            SendBuiltInTypes();
        }
    }
    
    void SendBuiltInTypes()
    {
        // Send various built-in types
        AetherLink.Instance.SendData(1001,
            "Player Data",           // string
            transform.position,      // Vector3
            transform.rotation,      // Quaternion
            Time.time,              // float
            Random.Range(1, 100),   // int
            true                    // bool
        );
    }
    
    void HandleReceivedData(PacketResponse packet)
    {
        if (packet.Header == 1001)
        {
            object[] data = packet.ReadObjects();
            
            string description = (string)data[0];
            Vector3 position = (Vector3)data[1];
            Quaternion rotation = (Quaternion)data[2];
            float time = (float)data[3];
            int randomValue = (int)data[4];
            bool isActive = (bool)data[5];
            
            Debug.Log($"Received: {description} at {position} with rotation {rotation}");
            Debug.Log($"Time: {time}, Random: {randomValue}, Active: {isActive}");
        }
    }
}
```
---

## Custom Type Serialization

For complex data structures, implement the `IAetherSerializable` interface:

### Basic Custom Type
```csharp
using System.IO;
using PandoraUtility.Network.Interfaces;

[System.Serializable]
public struct PlayerData : IAetherSerializable
{
    public string playerName;
    public Vector3 position;
    public Quaternion rotation;
    public float health;
    public int level;
    public bool isAlive;

    public void Serialize(BinaryWriter writer)
    {
        // Write data in a specific order
        writer.Write(playerName ?? string.Empty);
        writer.Write(position.x);
        writer.Write(position.y);
        writer.Write(position.z);
        writer.Write(rotation.x);
        writer.Write(rotation.y);
        writer.Write(rotation.z);
        writer.Write(rotation.w);
        writer.Write(health);
        writer.Write(level);
        writer.Write(isAlive);
    }
    
    public void Deserialize(BinaryReader reader)
    {
        // Read data in the same order as Serialize
        playerName = reader.ReadString();
        position = new Vector3(
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle()
        );
        rotation = new Quaternion(
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle()
        );
        health = reader.ReadSingle();
        level = reader.ReadInt32();
        isAlive = reader.ReadBoolean();
    }
}
```
### Registering Custom Types
```csharp
public class CustomTypeRegistration : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterCustomTypes()
    {
        // Register custom types with unique IDs
        AetherLink.RegisterSerializableType<PlayerData>(2001);
        AetherLink.RegisterSerializableType<GameState>(2002);
        AetherLink.RegisterSerializableType<InventoryItem>(2003);

        Debug.Log("Custom serialization types registered");
    }
}
```
### Using Custom Types
```csharp
public class CustomTypeExample : MonoBehaviour
{
    public UnityPacketEvent onPacketReceived;

    void Start()
    {
        onPacketReceived.AddListener(HandleCustomData);
    }
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C) && AetherLink.Instance.IsConnected)
        {
            SendCustomPlayerData();
        }
    }
    
    void SendCustomPlayerData()
    {
        var playerData = new PlayerData
        {
            playerName = "TestPlayer",
            position = transform.position,
            rotation = transform.rotation,
            health = 85.5f,
            level = 12,
            isAlive = true
        };
        
        // Send custom type using its registered ID
        AetherLink.Instance.SendData(2001, playerData);
        Debug.Log($"Sent player data for: {playerData.playerName}");
    }
    
    void HandleCustomData(PacketResponse packet)
    {
        if (packet.Header == 2001)
        {
            object[] data = packet.ReadObjects();
            PlayerData playerData = (PlayerData)data[0];
            
            Debug.Log($"Received player: {playerData.playerName}");
            Debug.Log($"Position: {playerData.position}, Health: {playerData.health}");
        }
    }
}
```
---

## Implementing IAetherSerializable

### Advanced Custom Type with Collections
```csharp
using System.Collections.Generic;
using System.IO;

[System.Serializable]
public class GameState : IAetherSerializable
{
    public float gameTime;
    public int currentLevel;
    public List<Vector3> checkpoints;
    public Dictionary<string, float> playerScores;
    public bool isPaused;

    public GameState()
    {
        checkpoints = new List<Vector3>();
        playerScores = new Dictionary<string, float>();
    }
    
    public void Serialize(BinaryWriter writer)
    {
        // Basic types
        writer.Write(gameTime);
        writer.Write(currentLevel);
        writer.Write(isPaused);
        
        // Collections - write count first, then elements
        writer.Write(checkpoints.Count);
        foreach (var checkpoint in checkpoints)
        {
            writer.Write(checkpoint.x);
            writer.Write(checkpoint.y);
            writer.Write(checkpoint.z);
        }
        
        // Dictionary
        writer.Write(playerScores.Count);
        foreach (var kvp in playerScores)
        {
            writer.Write(kvp.Key);
            writer.Write(kvp.Value);
        }
    }
    
    public void Deserialize(BinaryReader reader)
    {
        // Basic types
        gameTime = reader.ReadSingle();
        currentLevel = reader.ReadInt32();
        isPaused = reader.ReadBoolean();
        
        // Collections
        checkpoints.Clear();
        int checkpointCount = reader.ReadInt32();
        for (int i = 0; i < checkpointCount; i++)
        {
            Vector3 checkpoint = new Vector3(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle()
            );
            checkpoints.Add(checkpoint);
        }
        
        // Dictionary
        playerScores.Clear();
        int scoreCount = reader.ReadInt32();
        for (int i = 0; i < scoreCount; i++)
        {
            string playerName = reader.ReadString();
            float score = reader.ReadSingle();
            playerScores[playerName] = score;
        }
    }
}
```
### Complex Nested Structures
```csharp
[System.Serializable]
public class InventoryItem : IAetherSerializable
{
    public int itemId;
    public string itemName;
    public ItemType type;
    public List<ItemProperty> properties;

    public InventoryItem()
    {
        properties = new List<ItemProperty>();
    }
    
    public void Serialize(BinaryWriter writer)
    {
        writer.Write(itemId);
        writer.Write(itemName ?? string.Empty);
        writer.Write((int)type);
        
        // Serialize nested structures
        writer.Write(properties.Count);
        foreach (var property in properties)
        {
            property.Serialize(writer);
        }
    }
    
    public void Deserialize(BinaryReader reader)
    {
        itemId = reader.ReadInt32();
        itemName = reader.ReadString();
        type = (ItemType)reader.ReadInt32();
        
        // Deserialize nested structures
        properties.Clear();
        int propertyCount = reader.ReadInt32();
        for (int i = 0; i < propertyCount; i++)
        {
            var property = new ItemProperty();
            property.Deserialize(reader);
            properties.Add(property);
        }
    }
}

[System.Serializable]
public struct ItemProperty : IAetherSerializable
{
    public string propertyName;
    public float value;

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(propertyName ?? string.Empty);
        writer.Write(value);
    }
    
    public void Deserialize(BinaryReader reader)
    {
        propertyName = reader.ReadString();
        value = reader.ReadSingle();
    }
}

public enum ItemType
{
    Weapon = 0,
    Armor = 1,
    Consumable = 2,
    Quest = 3
}
```
---

## Advanced Serialization Patterns

### Versioning Support
```csharp
[System.Serializable]
public class VersionedData : IAetherSerializable
{
    private const byte CURRENT_VERSION = 2;

    public string playerName;
    public Vector3 position;
    public float health;
    
    // New fields added in version 2
    public int experience;
    public List<string> achievements;
    
    public VersionedData()
    {
        achievements = new List<string>();
    }
    
    public void Serialize(BinaryWriter writer)
    {
        // Always write version first
        writer.Write(CURRENT_VERSION);
        
        // Version 1 data
        writer.Write(playerName ?? string.Empty);
        writer.Write(position.x);
        writer.Write(position.y);
        writer.Write(position.z);
        writer.Write(health);
        
        // Version 2 data
        writer.Write(experience);
        writer.Write(achievements.Count);
        foreach (string achievement in achievements)
        {
            writer.Write(achievement);
        }
    }
    
    public void Deserialize(BinaryReader reader)
    {
        byte version = reader.ReadByte();
        
        // Version 1 data (always present)
        playerName = reader.ReadString();
        position = new Vector3(
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle()
        );
        health = reader.ReadSingle();
        
        // Version 2 data (conditional)
        if (version >= 2)
        {
            experience = reader.ReadInt32();
            
            achievements.Clear();
            int achievementCount = reader.ReadInt32();
            for (int i = 0; i < achievementCount; i++)
            {
                achievements.Add(reader.ReadString());
            }
        }
        else
        {
            // Set defaults for newer fields
            experience = 0;
            achievements.Clear();
        }
    }
}
```
### Conditional Serialization
```csharp
[System.Serializable]
public class OptimizedPlayerData : IAetherSerializable
{
    public string playerName;
    public Vector3 position;
    public Vector3 velocity;
    public float health;
    public bool hasWeapon;
    public string weaponName; // Only serialized if hasWeapon is true

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(playerName ?? string.Empty);
        writer.Write(position.x);
        writer.Write(position.y);
        writer.Write(position.z);
        writer.Write(velocity.x);
        writer.Write(velocity.y);
        writer.Write(velocity.z);
        writer.Write(health);
        writer.Write(hasWeapon);
        
        // Conditional serialization saves bandwidth
        if (hasWeapon)
        {
            writer.Write(weaponName ?? string.Empty);
        }
    }
    
    public void Deserialize(BinaryReader reader)
    {
        playerName = reader.ReadString();
        position = new Vector3(
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle()
        );
        velocity = new Vector3(
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle()
        );
        health = reader.ReadSingle();
        hasWeapon = reader.ReadBoolean();
        
        if (hasWeapon)
        {
            weaponName = reader.ReadString();
        }
        else
        {
            weaponName = null;
        }
    }
}
```
---

## Performance Considerations

### Efficient Serialization Tips
```csharp
// Good: Use structs for small, frequently transmitted data
[System.Serializable]
public struct FastPlayerUpdate : IAetherSerializable
{
    public Vector3 position;
    public float rotation;
    public byte health; // Use smaller types when possible

    public void Serialize(BinaryWriter writer)
    {
        // Pack data efficiently
        writer.Write(position.x);
        writer.Write(position.y);
        writer.Write(position.z);
        writer.Write(rotation);
        writer.Write(health);
    }
    
    public void Deserialize(BinaryReader reader)
    {
        position = new Vector3(
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle()
        );
        rotation = reader.ReadSingle();
        health = reader.ReadByte();
    }
}

// Good: Batch updates to reduce packet overhead
public class BatchedUpdates : MonoBehaviour
{
    private List<FastPlayerUpdate> updateBatch = new List<FastPlayerUpdate>();

    void SendBatchedUpdates()
    {
        if (updateBatch.Count > 0)
        {
            AetherLink.Instance.SendData(3001, updateBatch.ToArray());
            updateBatch.Clear();
        }
    }
    
    public void AddToBatch(FastPlayerUpdate update)
    {
        updateBatch.Add(update);
        
        // Send when batch reaches optimal size
        if (updateBatch.Count >= 10)
        {
            SendBatchedUpdates();
        }
    }
}
```
### Memory Pool Pattern
```csharp
public class PooledCustomType : IAetherSerializable
{
    // Use object pooling for frequently allocated custom types
    private static Stack<PooledCustomType> pool = new Stack<PooledCustomType>();

    public Vector3 position;
    public string data;
    
    public static PooledCustomType Get()
    {
        if (pool.Count > 0)
        {
            return pool.Pop();
        }
        return new PooledCustomType();
    }
    
    public void Return()
    {
        // Reset state
        position = Vector3.zero;
        data = null;
        
        pool.Push(this);
    }
    
    public void Serialize(BinaryWriter writer)
    {
        writer.Write(position.x);
        writer.Write(position.y);
        writer.Write(position.z);
        writer.Write(data ?? string.Empty);
    }
    
    public void Deserialize(BinaryReader reader)
    {
        position = new Vector3(
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle()
        );
        data = reader.ReadString();
    }
}
```
---

## Best Practices

### 1. Type Registration
```csharp
// Always register types at startup
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
static void RegisterAllCustomTypes()
{
    // Use consistent IDs across all builds
    AetherLink.RegisterSerializableType<PlayerData>(1001);
    AetherLink.RegisterSerializableType<GameState>(1002);
    AetherLink.RegisterSerializableType<ChatMessage>(1003);
    // ... register all types
}
```
### 2. Error Handling
```csharp
void SafeDataHandling(PacketResponse packet)
{
    try
    {
        object[] data = packet.ReadObjects();

        if (data.Length == 0)
        {
            Debug.LogWarning("Received empty data packet");
            return;
        }
        
        // Always validate data before using
        if (data[0] is PlayerData playerData)
        {
            ProcessPlayerData(playerData);
        }
        else
        {
            Debug.LogError($"Expected PlayerData, got {data[0].GetType()}");
        }
    }
    catch (System.Exception ex)
    {
        Debug.LogError($"Error processing packet: {ex.Message}");
    }
}
```
### 3. Header Constants
```csharp
public static class PacketHeaders
{
    // Use constants to avoid magic numbers
    public const ushort PLAYER_DATA = 1001;
    public const ushort GAME_STATE = 1002;
    public const ushort CHAT_MESSAGE = 1003;
    public const ushort INVENTORY_UPDATE = 1004;
}
```
---

## Common Pitfalls

### 1. Order Mismatch
```csharp
// BAD: Different order in Serialize vs Deserialize
public void Serialize(BinaryWriter writer)
{
    writer.Write(name);
    writer.Write(position.x);
    writer.Write(health);
}

public void Deserialize(BinaryReader reader)
{
    health = reader.ReadSingle();  // Wrong order!
    name = reader.ReadString();
    position.x = reader.ReadSingle();
}

// GOOD: Same order
public void Serialize(BinaryWriter writer)
{
    writer.Write(name);
    writer.Write(position.x);
    writer.Write(health);
}

public void Deserialize(BinaryReader reader)
{
    name = reader.ReadString();
    position.x = reader.ReadSingle();
    health = reader.ReadSingle();
}
```
### 2. Null Reference Handling
```csharp
// GOOD: Always handle null strings
public void Serialize(BinaryWriter writer)
{
    writer.Write(playerName ?? string.Empty);
}

public void Deserialize(BinaryReader reader)
{
    playerName = reader.ReadString();
    // String from BinaryReader is never null, but can be empty
}
```
### 3. Collection Handling
```csharp
// GOOD: Always write count before elements
public void Serialize(BinaryWriter writer)
{
    writer.Write(items?.Count ?? 0);
    if (items != null)
    {
        foreach (var item in items)
            {
                writer.Write(item);
            }
        }
    }
    
    public void Deserialize(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        items = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
        items.Add(reader.ReadString());
        }
    }
}
```
---

## Examples and Use Cases

### Real-time Game Synchronization
```csharp
[System.Serializable]
public struct GameSyncData : IAetherSerializable
{
    public float timestamp;
    public Vector3[] playerPositions;
    public byte[] playerStates;
    public int gameScore;

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(timestamp);
        writer.Write(gameScore);
        
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
        writer.Write(playerStates?.Length ?? 0);
        if (playerStates != null)
        {
            writer.Write(playerStates);
        }
    }
    
    public void Deserialize(BinaryReader reader)
    {
        timestamp = reader.ReadSingle();
        gameScore = reader.ReadInt32();
        
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
        int stateCount = reader.ReadInt32();
        playerStates = reader.ReadBytes(stateCount);
    }
}
```
This comprehensive guide covers all aspects of AetherLink's data serialization system, from basic types to advanced custom serialization patterns, with practical examples and best practices for building efficient networked applications.

---

*Continue with the [Advanced Usage Examples](../examples/) to see serialization in action within complete applications.*

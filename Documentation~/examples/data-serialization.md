# Zero-Allocation Data Serialization Tutorial

This tutorial covers AetherLink's modern, high-performance data serialization system. You'll learn how to define and send `unmanaged` structs over the network with absolutely zero garbage collection overhead.

## Table of Contents

- [Understanding AetherLink Serialization](#understanding-aetherlink-serialization)
- [Sending Primitive Values](#sending-primitive-values)
- [Defining Complex Structs](#defining-complex-structs)
- [Fixed Size Buffers & Arrays](#fixed-size-buffers--arrays)
- [Handling Strings](#handling-strings)

---

## Understanding AetherLink Serialization

AetherLink's latest serialization engine completely abandons `BinaryReader/Writer` patterns in favor of direct memory marshalling. 

By enforcing an `unmanaged` constraint on the `SendData<T>` payload, we ensure that data is stored contiguously in memory. This allows it to be streamed over TCP directly from a pinned `GCHandle`. On the receiving end, `packet.ReadAs<T>()` precisely interprets the incoming byte stream back into your native C# structure.

**Key Concepts:**
- **Zero Allocation**: Casting network bytes into value-type structs generates no garbage.
- **Unmanaged Types Only**: You cannot serialize managed reference types like `class`, `string`, `int[]`, or `List<T>`.
- **StructLayout**: Always use `[StructLayout(LayoutKind.Sequential)]` to guarantee your fields are kept in the order they were defined.

---

## Sending Primitive Values

Because types like `int`, `float`, `bool`, and `byte` are natively unmanaged, they can be utilized directly as simple data packages if you only need to transmit one value.

```csharp
void SendSimpleScore()
{
    int currentScore = 1500;
    
    // Generically parameterizes to `int`
    AetherLink.Instance.SendData(1001, currentScore);
}

void HandleReceivedData(PacketResponse packet)
{
    if (packet.Header == 1001)
    {
        var receivedScore = packet.ReadAs<int>();
        Debug.Log($"Score updated to: {receivedScore}");
    }
}
```

---

## Defining Complex Structs

For complex data structures, simply construct a standard C# struct. Unity mathematics types (`Vector2`, `Vector3`, `Quaternion`) are naturally unmanaged and permitted.

```csharp
using System.Runtime.InteropServices;
using UnityEngine;
using PandoraUtility.Network;

[StructLayout(LayoutKind.Sequential)]
public struct PlayerSnapshot
{
    public int PlayerId;
    public Vector3 Position;
    public Quaternion Rotation;
    public float Health;
    public bool IsAlive;
}

public class PlayerController : MonoBehaviour
{
    public void BroadcastSnapshot()
    {
        var snapshot = new PlayerSnapshot
        {
            PlayerId = 4012,
            Position = transform.position,
            Rotation = transform.rotation,
            Health = 100f,
            IsAlive = true
        };
        
        AetherLink.Instance.SendData(2001, snapshot);
    }

    public void OnPacketReceived(PacketResponse packet)
    {
        if (packet.Header == 2001)
        {
            // Parses effortlessly and safely
            var snapshot = packet.ReadAs<PlayerSnapshot>();
            Debug.Log($"Player {snapshot.PlayerId} is at {snapshot.Position}");
        }
    }
}
```

---

## Fixed Size Buffers & Arrays

Since dynamic arrays (like `int[]`) are reference types, they are disallowed within unmanaged structs. Instead, C# supports **fixed-size buffers**. Note that dealing with fixed buffers requires the `unsafe` keyword in Unity.

*Note: Ensure "Allow 'unsafe' code" is checked in your Unity Player Settings.*

```csharp
[StructLayout(LayoutKind.Sequential)]
public unsafe struct InventoryData
{
    public int OwnerId;
    // Embeds an array of precisely 10 integers directly inside the struct
    public fixed int ItemIds[10];
    public int ActiveItemCount;
}

public unsafe void BroadcastInventory(int[] items)
{
    var data = new InventoryData
    {
        OwnerId = 4012,
        ActiveItemCount = Mathf.Min(items.Length, 10)
    };

    // Copying items into the fixed buffer
    for (int i = 0; i < data.ActiveItemCount; i++)
    {
        data.ItemIds[i] = items[i];
    }

    AetherLink.Instance.SendData(2002, data);
}

public unsafe void HandleInventoryData(PacketResponse packet)
{
    var data = packet.ReadAs<InventoryData>();
    
    // Iterating the fixed buffer
    for (int i = 0; i < data.ActiveItemCount; i++)
    {
        Debug.Log($"User {data.OwnerId} owns item {data.ItemIds[i]}");
    }
}
```

---

## Handling Strings

Just like arrays, the C# `string` type is a managed reference and cannot be serialized.

If you are heavily utilizing strings across the network, we highly recommend installing the `Unity.Collections` package and utilizing their `FixedString` variants (such as `FixedString32Bytes`). These are true, 100% unmanaged struct representations of strings.

### Strategy 1: Fixed Byte Array (Vanilla C#)
If you don't wish to install external dependencies, you can serialize string bytes into a fixed buffer manually.

```csharp
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ChatMessage
{
    // Reserve space for a 64-character ascii string
    public fixed byte NameBuffer[64];
    public int NameLength;
}

// Sending:
public unsafe void SendName(string name)
{
    var message = new ChatMessage();
    byte[] stringBytes = System.Text.Encoding.ASCII.GetBytes(name);
    message.NameLength = Mathf.Min(stringBytes.Length, 64);
    
    for (int i = 0; i < message.NameLength; i++)
    {
        message.NameBuffer[i] = stringBytes[i];
    }
    
    AetherLink.Instance.SendData(3001, message);
}

// Receiving:
public unsafe void ReceiveName(PacketResponse packet)
{
    var message = packet.ReadAs<ChatMessage>();
    
    byte[] stringBytes = new byte[message.NameLength];
    for (int i = 0; i < message.NameLength; i++)
    {
        stringBytes[i] = message.NameBuffer[i];
    }
    
    string decodedName = System.Text.Encoding.ASCII.GetString(stringBytes);
    Debug.Log($"User joined: {decodedName}");
}
```

### Strategy 2: Unity.Collections FixedString (Recommended)

If you have `Unity.Collections` in your project, dealing with strings is significantly easier and natively unmanaged!

```csharp
using Unity.Collections;

[StructLayout(LayoutKind.Sequential)]
public struct ModernChatMessage
{
    // A standard unmanaged struct containing up to 32 bytes of text
    public FixedString32Bytes SenderName;
    public FixedString128Bytes MessageContent;
}

public void SendModernChat()
{
    var msg = new ModernChatMessage
    {
        SenderName = new FixedString32Bytes("PlayerOne"),
        MessageContent = new FixedString128Bytes("Hello world over the network!")
    };
    
    AetherLink.Instance.SendData(3001, msg);
}

public void ReceiveModernChat(PacketResponse packet)
{
    var msg = packet.ReadAs<ModernChatMessage>();
    
    // Automatically interoperates back to System.String implicitly when logging out
    Debug.Log($"[{msg.SenderName}]: {msg.MessageContent}");
}
```

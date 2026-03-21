# Cross-Project Data Handshakes (Shared Contracts)

When utilizing AetherLink's zero-allocation unmanaged struct architecture (`ReadAs<T>()`), performance is maximized by deserializing byte payloads directly into C# memory. 
However, **if you are networking between two entirely different Unity Projects** (e.g., a Dedicated Server repository and a Mobile Client repository), you must guarantee that the C# compilers on both ends compute the exact same memory layout for your structs.

If one project defines a struct slightly differently than the other, memory corruption will occur, crashing the application or returning garbage values.

## The Solution: Shared Contracts Assembly

To solve this correctly, do not declare your network structs independently in both projects. Instead, declare them in a **Shared Assembly Definition (`.asmdef`)** that is referenced by both projects.

### Step 1: Create the Contracts Assembly
Create a folder named `NetworkContracts` and add two files to it:
1. `Pandora.Network.Contracts.asmdef`
2. `PlayerSyncData.cs`

**Example `PlayerSyncData.cs`:**
```csharp
using System.Runtime.InteropServices;
using UnityEngine;

namespace Pandora.Network.Contracts
{
    // Force the memory layout explicitly so C# compiling differences 
    // never misalign the data across OS platforms or projects.
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct PlayerSyncData
    {
        [FieldOffset(0)]  public int PlayerId;
        [FieldOffset(4)]  public int Health;
        [FieldOffset(8)]  public float CurrentSpeed;
        [FieldOffset(12)] public Vector3 Position; // 12 bytes
    }
}
```

### Step 2: Share between Projects
Rather than copying the `.cs` files back and forth manually, either:
- **Git Submodule**: Put the `NetworkContracts` folder in a dedicated Git repository and include it as a submodule in both projects.
- **Symlink**: Create a symbolic link pointing from `ProjectB/Assets/` to `ProjectA/Assets/NetworkContracts`.
- **Custom Unity Package**: Package the `NetworkContracts` folder using the Unity Package Manager and install it locally in both projects.

### Step 3: Implement Safely
Both your Dedicated Server and your Client will now parse data reliably:

```csharp
void OnDataReceived(PacketResponse packet)
{
    // The sizes are perfectly guaranteed to match!
    var data = packet.ReadAs<PlayerSyncData>();
    Debug.Log($"Synchronizing Player {data.PlayerId} at {data.Position}");
}
```

Using `LayoutKind.Explicit` ensures that padding differences between Mono and IL2CPP, or Unity versions, never ruin your data layout across the wire.

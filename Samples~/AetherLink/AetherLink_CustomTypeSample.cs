using System.Runtime.InteropServices;
using UnityEngine;

namespace MizoreRainy.Pandora.Samples.NetworkUtility
{
    /// <summary>
    /// This sample demonstrates how to create a high-performance, zero-allocation network payload using Unmanaged Structs.
    /// It entirely replaces the legacy `IAetherSerializable` interface and object[] boxing arrays.
    /// 
    /// **Important Cross-Project Note (Shared Contracts Assembly):**
    /// If you are sending this struct between two DIFFERENT Unity Projects (e.g., a Dedicated Server and a Client),
    /// you must put this struct into a Shared Assembly Definition (.asmdef) and use `[StructLayout(LayoutKind.Explicit)]`.
    /// This prevents different C# compilers from accidentally padding the bytes differently, which would corrupt the unmanaged memory copy!
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 28)]
    public struct AetherLink_CustomTypeSample
    {
        // 4 bytes (int)
        [FieldOffset(0)] 
        public int PlayerNetworkId;

        // 4 bytes (int)
        [FieldOffset(4)] 
        public int Health;

        // 4 bytes (string replacements: max 4 characters as uint for unmanaged safety, or use fixed char buffers)
        // Here we just use a flags enum or similar.
        [FieldOffset(8)] 
        public uint StateFlags;

        // 16 bytes (Vector3 is 3 floats = 12 bytes + some padding)
        [FieldOffset(12)] 
        public Vector3 Position;

        [FieldOffset(24)] 
        public float RotationY;

        /// <summary>
        /// To SEND this packet:
        /// AetherLink.Instance.SendData(1001, new AetherLink_CustomTypeSample { Health = 100 });
        /// 
        /// To RECEIVE this packet:
        /// AetherLink.Instance.OnDataReceived().ForEachAsync(packet => {
        ///     var data = packet.ReadAs<AetherLink_CustomTypeSample>();
        ///     Debug.Log(data.Health);
        /// });
        /// </summary>
        public override string ToString()
        {
            return $"Player {PlayerNetworkId} | HP: {Health} | Pos: {Position}";
        }
    }
}
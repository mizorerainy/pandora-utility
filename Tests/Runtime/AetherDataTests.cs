using NUnit.Framework;
using MizoreRainy.Pandora.NetworkUtility;
using System.Net;
using System.Runtime.InteropServices;

namespace MizoreRainy.Pandora.Tests.Runtime
{
    public class AetherDataTests
    {
        public struct TestData
        {
            public int ID;
            public float Value;
        }

        [Test]
        public void PacketResponse_ReadAs_DeserializesUnmanagedStructs()
        {
            var data = new TestData { ID = 42, Value = 13.37f };
            byte[] payload = new byte[Marshal.SizeOf<TestData>()];
            
            var handle = GCHandle.Alloc(payload, GCHandleType.Pinned);
            try
            {
                Marshal.StructureToPtr(data, handle.AddrOfPinnedObject(), false);
            }
            finally
            {
                handle.Free();
            }

            // Create network response
            var packet = new AetherLink.PacketResponse(1, payload, new IPEndPoint(IPAddress.Loopback, 1234), "Local", 0);
            
            // Test zero-allocation ReadAs utility wrapper
            var result = packet.ReadAs<TestData>();
            Assert.AreEqual(42, result.ID);
            Assert.AreEqual(13.37f, result.Value);
        }

        [Test]
        public void PacketResponse_ReadAs_ReturnsDefaultOnInsufficientData()
        {
            byte[] shortPayload = new byte[2]; // Structs require more than 2 bytes
            var packet = new AetherLink.PacketResponse(1, shortPayload, new IPEndPoint(IPAddress.Loopback, 1234), "Local", 0);
            
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("too small to read"));
            var result = packet.ReadAs<TestData>();
            Assert.AreEqual(0, result.ID);
            Assert.AreEqual(0f, result.Value);
        }
    }
}

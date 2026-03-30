using NUnit.Framework;
using UnityEngine;
using MizoreRainy.Pandora.NetworkUtility;
using System.Reflection;
using System;

namespace MizoreRainy.Pandora.Tests.Runtime
{
    public class AetherProtocolTests
    {
        private AetherLink _link;
        private MethodInfo _calcFletcher16;
        private MethodInfo _createTcpPacket;
        private MethodInfo _validatePacketIntegrity;

        [SetUp]
        public void Setup()
        {
            var go = new GameObject("AetherLinkTest");
            _link = go.AddComponent<AetherLink>();
            
            _calcFletcher16 = typeof(AetherLink).GetMethod("CalculateFletcher16", BindingFlags.NonPublic | BindingFlags.Instance);
            _createTcpPacket = typeof(AetherLink).GetMethod("CreateTcpPacket", BindingFlags.NonPublic | BindingFlags.Instance);
            _validatePacketIntegrity = typeof(AetherLink).GetMethod("ValidatePacketIntegrity", BindingFlags.NonPublic | BindingFlags.Instance);
            
            Assert.IsNotNull(_calcFletcher16, "CalculateFletcher16 not found.");
            Assert.IsNotNull(_createTcpPacket, "CreateTcpPacket not found.");
            Assert.IsNotNull(_validatePacketIntegrity, "ValidatePacketIntegrity not found.");
        }

        [TearDown]
        public void Teardown()
        {
            UnityEngine.Object.DestroyImmediate(_link.gameObject);
        }

        [Test]
        public void CalculateFletcher16_ComputesCorrectChecksum()
        {
            // Fletcher-16 test for: 1, 2, 3, 4, 5
            // Sum1: 1, 3, 6, 10, 15
            // Sum2: 1, 4, 10, 20, 35
            // Result: (35 << 8) | 15 = 8960 | 15 = 8975
            byte[] data = new byte[] { 1, 2, 3, 4, 5 };
            ushort checksum = (ushort)_calcFletcher16.Invoke(_link, new object[] { data, 0, data.Length });
            Assert.AreEqual(8975, checksum);
        }

        [Test]
        public void CreateTcpPacket_BuildsValidFraming()
        {
            ushort header = 0xAA;
            byte[] payload = new byte[] { 10, 20, 30 };
            
            byte[] packet = (byte[])_createTcpPacket.Invoke(_link, new object[] { header, payload });
            
            // Expected length: 2 (start) + 2 (len) + 2 (hdr) + 3 (payload) + 2 (checksum) + 2 (end) = 13 bytes.
            Assert.AreEqual(13, packet.Length, "Packet was not the structurally expected length.");
            
            // Validate parsing manually
            ushort magicStart = BitConverter.ToUInt16(packet, 0);
            ushort length = BitConverter.ToUInt16(packet, 2);
            ushort parsedHeader = BitConverter.ToUInt16(packet, 4);
            ushort magicEnd = BitConverter.ToUInt16(packet, packet.Length - 2);
            
            Assert.AreEqual(0xAE77, magicStart, "Invalid Magic Start Byte");
            Assert.AreEqual(0x77EA, magicEnd, "Invalid Magic End Byte");
            Assert.AreEqual(3, length, "Invalid encoded payload length");
            Assert.AreEqual(0xAA, parsedHeader, "Invalid header ID");
        }

        [Test]
        public void ValidatePacketIntegrity_FailsOnCorruptedData()
        {
            ushort header = 0xAA;
            byte[] payload = new byte[] { 10, 20, 30 };
            byte[] validPacket = (byte[])_createTcpPacket.Invoke(_link, new object[] { header, payload });
            
            object[] validArgs = new object[] { validPacket, (ushort)0 };
            bool isValid = (bool)_validatePacketIntegrity.Invoke(_link, validArgs);
            Assert.IsTrue(isValid, "Original packet should be considered structurally sound.");
            
            // Corrupt the payload (index 6 is the first payload byte after start(2)+len(2)+hdr(2))
            byte[] corruptedPacket = new byte[validPacket.Length];
            Array.Copy(validPacket, corruptedPacket, validPacket.Length);
            corruptedPacket[6] = 99; // Corrupt data, therefore checksum shouldn't match
            
            object[] corruptedArgs = new object[] { corruptedPacket, (ushort)0 };
            bool isCorruptValid = (bool)_validatePacketIntegrity.Invoke(_link, corruptedArgs);
            Assert.IsFalse(isCorruptValid, "Corrupted packet should be rejected due to invalid Checksum.");
        }
    }
}

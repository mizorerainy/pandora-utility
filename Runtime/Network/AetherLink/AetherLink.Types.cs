// =====================================================================================================================
//
// AetherLink.cs
//
// A high-performance, flexible LAN communication manager for Unity.
// Implemented as a persistent Singleton with a fully extensible custom type serialization system.
//
// Features:
// - Master/Slave architecture with UDP auto-discovery.
// - Efficient, point-to-point TCP heartbeats for connection stability.
// - Robust binary protocols for both UDP (discovery) and TCP (data transfer).
// - Extensible Serialization: Register any custom class/struct via an interface (IAetherSerializable).
// - Dual API System:
//   1. Inspector-friendly UnityEvents for designers.
//   2. A modern, safe, and flexible async API for programmers (Managed Handlers & Advanced Streams).
// - Robust data integrity protocol (Magic Bytes and Checksum).
//
// =====================================================================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using MizoreRainy.Pandora;
using MizoreRainy.Pandora.NetworkUtility.Interfaces;
using UnityEngine;
using UnityEngine.Events;
#if HAVE_CYSHARP_UNITASK
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using System.Threading;
#endif

// ReSharper disable once CheckNamespace
namespace MizoreRainy.Pandora.NetworkUtility
{
	/// <summary>
	///     A high-performance, flexible LAN communication manager. This version features a unified event system,
	///     a modern, reactive async stream API, and is implemented as a persistent Singleton.
	/// </summary>
	// ReSharper disable once PartialTypeWithSinglePart
	public partial class AetherLink : MonoBehaviour
	{
		#region Nested Types

		/// <summary>
		///     Contains all configuration for the AetherLink component. Can be set in the Inspector or from code.
		/// </summary>
		[Serializable]
		public struct Settings
		{
			[Header("General")] public Mode LinkMode;

			[Tooltip("If true, the link will start automatically when the component is enabled.")]
			public bool StartOnEnable;

			[Tooltip("If true, the link will stop when the application is paused.")]
			public bool StopOnPause;

			[Tooltip(
				"If true, a Slave will attempt to connect to the same machine (localhost) if no Master is found via UDP broadcast. Recommended for testing.")]
			public bool AllowSameMachineConnection;

			[Header("Network Ports")]
			[Range(1024, 65535)]
			public int UdpBroadcastPort;

			[Range(1024, 65535)] public int TcpConnectionPort;

			[Header("Timings (Milliseconds)")]
			[Range(100, 5000)]
			public int HandshakeInterval;

			[Range(100, 5000)] public int HeartbeatInterval;
			[Range(500, 10000)] public int HeartbeatTimeout;

			[Header("Advanced")]
			[Tooltip("Maximum size for TCP packets in bytes (default: 16MB)")]
			[Range(1024, 64 * 1024 * 1024)]
			public int MaxPacketSize;

			[Tooltip("Buffer size for TCP operations")]
			[Range(1024, 64 * 1024)]
			public int TcpBufferSize;

			[Header("Debugging")] public bool DebugUdpMessages;
			public bool DebugTcpMessages;

			/// <summary>
			///     Provides a default set of configuration values.
			/// </summary>
			public static Settings Default => new()
			{
				LinkMode = Mode.Master,
				StartOnEnable = false,
				StopOnPause = false,
				AllowSameMachineConnection = false,
				UdpBroadcastPort = 7778,
				TcpConnectionPort = 7777,
				HandshakeInterval = 2000,
				HeartbeatInterval = 1000,
				HeartbeatTimeout = 5000,
				MaxPacketSize = 1024 * 1024, // Reduced to 1MB to prevent large memory allocations by default
				TcpBufferSize = 8192,
				DebugUdpMessages = false,
				DebugTcpMessages = false
			};
		}

		/// <summary>
		///     Represents a received data packet, containing the header and the raw data payload.
		/// </summary>
		[Serializable]
		public class PacketResponse
		{
			/// <summary>The user-defined header code for this packet.</summary>
			public ushort Header { get; }

			/// <summary>The raw byte data payload of the packet.</summary>
			public byte[] Data { get; }

			/// <summary>The network endpoint of the sender.</summary>
			public IPEndPoint RemoteEndPoint { get; }

			public PacketResponse(ushort _header, byte[] _data, IPEndPoint _remoteEndPoint)
			{
				Header = _header;
				Data = _data;
				RemoteEndPoint = _remoteEndPoint;
			}

			/// <summary>
			///     Reads objects from the binary data stream.
			/// </summary>
			/// <returns>Array of objects in the order they were written.</returns>
			public object[] ReadObjects()
			{
				if (Data == null || Data.Length == 0)
					return Array.Empty<object>();

				var result = new List<object>();
				using (var stream = new MemoryStream(Data))
				using (var reader = new BinaryReader(stream))
				{
					try
					{
						while (stream.Position < stream.Length)
						{
							var typeCode = (TypeCode)reader.ReadByte();
							var obj = ReadObjectByType(reader, typeCode);
							result.Add(obj);
						}
					}
					catch (Exception ex)
					{
						PandoraLogger.LogNetworkError($"Failed to read objects from packet: {ex.Message}");
					}
				}

				return result.ToArray();
			}

			/// <summary>
			///     Reads a single object from a binary stream based on its TypeCode.
			/// </summary>
			/// <param name="_reader">The binary reader to read from.</param>
			/// <param name="_typeCode">The TypeCode indicating what type of data to read.</param>
			/// <returns>The deserialized object.</returns>
			private object ReadObjectByType(BinaryReader _reader, TypeCode _typeCode)
			{
				switch (_typeCode)
				{
					case TypeCode.Boolean: return _reader.ReadBoolean();
					case TypeCode.Byte: return _reader.ReadByte();
					case TypeCode.SByte: return _reader.ReadSByte();
					case TypeCode.Int16: return _reader.ReadInt16();
					case TypeCode.UInt16: return _reader.ReadUInt16();
					case TypeCode.Int32: return _reader.ReadInt32();
					case TypeCode.UInt32: return _reader.ReadUInt32();
					case TypeCode.Int64: return _reader.ReadInt64();
					case TypeCode.UInt64: return _reader.ReadUInt64();
					case TypeCode.Single: return _reader.ReadSingle();
					case TypeCode.Double: return _reader.ReadDouble();
					case TypeCode.String: return _reader.ReadString();
					case TypeCode.Object: // This now signifies a custom IAetherSerializable type
						var typeId = _reader.ReadUInt16();
						if (CustomTypeFactories.TryGetValue(typeId, out var factory))
						{
							var instance = factory();
							instance.Deserialize(_reader);
							return instance;
						}

						throw new NotSupportedException($"Received an unregistered custom type ID: {typeId}");
					default:
						throw new NotSupportedException($"Type {_typeCode} is not supported");
				}
			}
		}

		/// <summary>A UnityEvent that can pass PacketResponse objects, making it usable in the Inspector.</summary>
		[Serializable]
		public class UnityPacketEvent : UnityEvent<PacketResponse>
		{
		}

		/// <summary>A UnityEvent that can pass IPEndPoint objects for the OnConnected event.</summary>
		[Serializable]
		public class UnityIPEndPointEvent : UnityEvent<IPEndPoint>
		{
		}

		#endregion

		#region Network Statistics

		[Serializable]
		public struct NetworkStats
		{
			public int PacketsSent;
			public int PacketsReceived;
			public long BytesSent;
			public long BytesReceived;
			public float LastPacketTime;
			public int ConnectionAttempts;
			public int DisconnectionCount;
			public int CorruptedPackets;
			public int MalformedPackets;


			public void Reset()
			{
				PacketsSent = 0;
				PacketsReceived = 0;
				BytesSent = 0;
				BytesReceived = 0;
				LastPacketTime = 0;
				CorruptedPackets = 0;
				MalformedPackets = 0;
			}
		}

		private NetworkStats _Statistics;

		#endregion

	}
}

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
	[AddComponentMenu("Pandora/Networking/AetherLink")]
	// ReSharper disable once PartialTypeWithSinglePart
	public partial class AetherLink : MonoBehaviour
	{
		#region Singleton Pattern

		private static AetherLink _Instance;
		private static readonly object Lock = new();

		/// <summary>
		///     Gets the singleton instance of the AetherLink.
		///     If no instance exists, it will be automatically created in the scene.
		/// </summary>
		public static AetherLink Instance
		{
			get
			{
				lock (Lock)
				{
					if (_Instance == null)
					{
						// Try to find an existing instance in the scene
						_Instance = FindFirstObjectByType<AetherLink>();

						if (_Instance == null)
						{
							// No instance found, create a new one
							var go = new GameObject("[AetherLink Singleton]");
							_Instance = go.AddComponent<AetherLink>();
							Debug.Log("[AetherLink] An instance was automatically created.");
						}
					}

					return _Instance;
				}
			}
		}

		#endregion

		#region Custom Type Registry

		private static readonly Dictionary<ushort, Func<IAetherSerializable>> CustomTypeFactories = new();
		private static readonly Dictionary<Type, ushort> CustomTypeIds = new();

		/// <summary>
		///     Registers a custom type for serialization with AetherLink.
		///     This must be called once at startup for each custom type you wish to send.
		///     A great place to call this is in a method marked with [RuntimeInitializeOnLoadMethod].
		/// </summary>
		/// <typeparam name="T">The type to register. Must implement IAetherSerializable and have a parameterless constructor.</typeparam>
		/// <param name="_typeId">
		///     A unique ushort ID for this type. This ID must be the same in both the sending and receiving
		///     applications.
		/// </param>
		/// <param name="_override">
		///     If true, allows overwriting an existing type registration with the same ID. If false, throws an
		///     exception when attempting to register a duplicate ID.
		/// </param>
		/// <exception cref="InvalidOperationException">
		///     Thrown when the type or type ID is already registered and _override is
		///     false.
		/// </exception>
		public static void RegisterSerializableType<T>(ushort _typeId, bool _override = false)
			where T : IAetherSerializable, new()
		{
			var type = typeof(T);

			if (CustomTypeIds.TryGetValue(type, out var oldId))
				throw new InvalidOperationException(
					$"[AetherLink] Type {type.Name} is already registered with ID {oldId}. Cannot register the same type twice.");

			if (CustomTypeFactories.TryGetValue(_typeId, out var factory))
			{
				var oldType = factory().GetType();
				if (_override)
				{
					Debug.LogWarning(
						$"[AetherLink] Type ID {_typeId} is already registered to {oldType.Name}. Overwriting with {type.Name}.");
					CustomTypeIds.Remove(oldType);
				}
				else
				{
					throw new InvalidOperationException(
						$"[AetherLink] Type ID {_typeId} is already registered to {oldType.Name}. Cannot register the same ID twice.");
				}
			}

			CustomTypeIds[type] = _typeId;
			CustomTypeFactories[_typeId] = () => new T();
			Debug.Log($"[AetherLink] Registered custom type '{type.Name}' with ID {_typeId}.");
		}

		#endregion

		#region Nested Classes & Structs

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

			[Header("Network Ports")] [Range(1024, 65535)]
			public int UdpBroadcastPort;

			[Range(1024, 65535)] public int TcpConnectionPort;

			[Header("Timings (Milliseconds)")] [Range(100, 5000)]
			public int HandshakeInterval;

			[Range(100, 5000)] public int HeartbeatInterval;
			[Range(500, 10000)] public int HeartbeatTimeout;

			[Header("Advanced")]
			[Tooltip("Maximum size for TCP packets in bytes (default: 16MB)")]
			[Range(1024, 64 * 1024 * 1024)]
			public int MaxPacketSize;

			[Tooltip("Buffer size for TCP operations")] [Range(1024, 64 * 1024)]
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
				MaxPacketSize = 16 * 1024 * 1024,
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
						Debug.LogError($"[AetherLink] Failed to read objects from packet: {ex.Message}");
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

		#region Enums and Constants

		/// <summary>
		///     Defines the operational role of the AetherLink instance.
		/// </summary>
		public enum Mode
		{
			Master = 0,
			Slave = 1
		}

		private const byte _UDP_CMD_HANDSHAKE = 0xAB;
		private const int _UDP_PACKET_SIZE = 17;
		private const int _GUID_BYTE_SIZE = 16;
		private const int _UDP_HEADER_SIZE = _UDP_PACKET_SIZE - _GUID_BYTE_SIZE;

		private const ushort _TCP_CMD_HEARTBEAT = 0xFAF0; // Internal heartbeat will not be exposed to the user.

		private const ushort _MAGIC_START = 0xAE77;
		private const ushort _MAGIC_END = 0x77EA;

		private const int _PAYLOAD_LENGTH_OFFSET = 2;
		private const int _HEADER_OFFSET = 2;
		private const int _MAGIC_OFFSET = 2;
		private const int _CHECKSUM_OFFSET = 1;

		private const float _MS_TO_SEC_MULTIPLIER = .001f;

		private const int _CONNECTION_VALIDATION_DELAY_MICRO_SEC = 100;

		private static readonly byte[] MagicStartBytes = BitConverter.GetBytes(_MAGIC_START);
		private static readonly byte[] MagicEndBytes = BitConverter.GetBytes(_MAGIC_END);

		#endregion

		#region Public Properties

		/// <summary>Gets a value indicating whether the link is currently running (i.e., StartLink has been called).</summary>
		public bool IsRunning { get; private set; }

		/// <summary>Gets a value indicating whether the link has an active and established TCP connection.</summary>
		public bool IsConnected => IsRunning && _IsTcpConnected;

		/// <summary>
		///     Gets the current network mode (Master/Slave)
		/// </summary>
		public Mode CurrentMode => m_Settings.LinkMode;

		/// <summary>
		///     Returns true if this instance is running as a master node
		/// </summary>
		public bool IsMaster => CurrentMode == Mode.Master;

		/// <summary>
		///     Returns true if this instance is running as a slave node
		/// </summary>
		public bool IsSlave => CurrentMode == Mode.Slave;

		/// <summary>Gets the network endpoint of the currently connected remote party.</summary>
		public IPEndPoint RemoteEndPoint { get; private set; }

		/// <summary>Gets network statistics for monitoring.</summary>
		public NetworkStats Statistics => _Statistics;

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

		#region Public Events (For Inspector)

		/// <summary>Fired when a TCP connection is successfully established. Use this for both Inspector and code subscriptions.</summary>
		[Tooltip("Fired when a TCP connection is successfully established.")]
		public UnityIPEndPointEvent OnConnectedInspector = new();

		/// <summary>Fired when a data packet is received over TCP. This is the primary event for receiving data.</summary>
		[Tooltip("Fired when a data packet is received over TCP.")]
		public UnityPacketEvent OnPacketReceivedInspector = new();

		/// <summary>Fired when the TCP connection is lost or closed.</summary>
		[Tooltip("Fired when the TCP connection is lost or closed.")]
		public UnityEvent OnDisconnectedInspector = new();

		#endregion

		#region Managed Handlers API (Recommended)

#if HAVE_CYSHARP_UNITASK
		/// <summary>
		///     Registers a safe, managed handler for connection events that runs for the lifetime of the provided token.
		///     This is the recommended way to listen for events as it handles exceptions automatically.
		/// </summary>
		/// <param name="_handler">The action to execute when a connection is made.</param>
		/// <param name="_cancellationToken">The token that controls the lifetime of the listener.</param>
		public void RegisterConnectionHandler(Action<IPEndPoint> _handler, CancellationToken _cancellationToken)
		{
			UniTask.Create(async () =>
			{
				await OnConnected().ForEachAsync(_endpoint =>
				{
					try
					{
						_handler(_endpoint);
					}
					catch (Exception ex)
					{
						Debug.LogError($"[AetherLink] Error in user-provided connection handler: {ex}");
					}
				}, _cancellationToken);
			}).Forget(); // .Forget() is safe here as UniTask logs the exception.
		}

		/// <summary>
		///     Registers a safe, managed handler for disconnection events.
		/// </summary>
		/// <param name="_handler">The action to execute on disconnection.</param>
		/// <param name="_cancellationToken">The token that controls the lifetime of the listener.</param>
		public void RegisterDisconnectionHandler(Action _handler, CancellationToken _cancellationToken)
		{
			UniTask.Create(async () =>
			{
				await OnDisconnected().ForEachAsync(_ =>
				{
					try
					{
						_handler();
					}
					catch (Exception ex)
					{
						Debug.LogError($"[AetherLink] Error in user-provided disconnection handler: {ex}");
					}
				}, _cancellationToken);
			}).Forget();
		}

		/// <summary>
		///     Registers a safe, managed handler for all incoming data packets.
		/// </summary>
		/// <param name="_handler">The action to execute for each received packet.</param>
		/// <param name="_cancellationToken">The token that controls the lifetime of the listener.</param>
		public void RegisterDataHandler(Action<PacketResponse> _handler, CancellationToken _cancellationToken)
		{
			UniTask.Create(async () =>
			{
				await OnDataReceived().ForEachAsync(_packet =>
				{
					try
					{
						_handler(_packet);
					}
					catch (Exception ex)
					{
						Debug.LogError(
							$"[AetherLink] Error in user-provided data handler for header {_packet.Header}: {ex}");
					}
				}, _cancellationToken);
			}).Forget();
		}

		/// <summary>
		///     Registers a safe, managed handler for data packets with a specific header.
		/// </summary>
		/// <param name="_header">The packet header to listen for.</param>
		/// <param name="_handler">The action to execute for matching packets.</param>
		/// <param name="_cancellationToken">The token that controls the lifetime of the listener.</param>
		public void RegisterDataHandler(ushort _header, Action<PacketResponse> _handler,
			CancellationToken _cancellationToken)
		{
			UniTask.Create(async () =>
			{
				// Use the .Where() operator to filter the stream before iterating
				await OnDataReceived().Where(_p => _p.Header == _header).ForEachAsync(_packet =>
				{
					try
					{
						_handler(_packet);
					}
					catch (Exception ex)
					{
						Debug.LogError(
							$"[AetherLink] Error in user-provided data handler for header {_packet.Header}: {ex}");
					}
				}, _cancellationToken);
			}).Forget();
		}
#endif

		#endregion

		#region Advanced Async Stream API

#if HAVE_CYSHARP_UNITASK
		/// <summary>
		///     Subscribes to a stream of events that are triggered when a TCP connection
		///     is successfully established.
		/// </summary>
		/// <returns>
		///     An asynchronous enumerable of <see cref="IPEndPoint" /> instances representing
		///     the remote endpoints that successfully connected.
		/// </returns>
		public IUniTaskAsyncEnumerable<IPEndPoint> OnConnected()
		{
			return _OnConnectedChannel.Reader.ReadAllAsync();
		}

		/// <summary>
		///     Occurs when the TCP connection is lost or closed. This provides an asynchronous stream
		///     that consumers can use to handle disconnection events.
		/// </summary>
		/// <returns>
		///     A UniTask asynchronous enumerable that signals whenever the connection is disconnected.
		///     Subscribers can use this to react to disconnection events.
		/// </returns>
		public IUniTaskAsyncEnumerable<AsyncUnit> OnDisconnected()
		{
			return _OnDisconnectedChannel.Reader.ReadAllAsync();
		}

		/// <summary>
		///     Asynchronously provides a stream of data packets received over the TCP connection.
		///     Developers can use this to react to incoming data packets by iterating over the resulting sequence.
		/// </summary>
		/// <returns>
		///     A task-based asynchronous stream representing data packets received. Each packet is provided as
		///     a <see cref="PacketResponse" /> object.
		/// </returns>
		public IUniTaskAsyncEnumerable<PacketResponse> OnDataReceived()
		{
			return _OnPacketReceivedChannel.Reader.ReadAllAsync();
		}

		/// <summary>
		///     Waits for a data packet with a specific header to be received.
		///     This method listens for incoming packets and resolves with the first one that matches the specified header.
		/// </summary>
		/// <param name="_header">The unique header ID to filter packets by.</param>
		/// <param name="_cancellationToken">
		///     A token to monitor for cancellation requests. If the token is canceled before the packet is received,
		///     the operation is stopped and will not complete.
		/// </param>
		/// <returns>
		///     A UniTask that resolves to a <see cref="PacketResponse" /> object containing the packet data when a matching packet
		///     is received.
		/// </returns>
		public UniTask<PacketResponse> WaitForPacketAsync(ushort _header, CancellationToken _cancellationToken)
		{
			return OnDataReceived().Where(_packet => _packet.Header == _header).FirstAsync(_cancellationToken);
		}
#endif

		#endregion

		#region Private State

		[SerializeField] private Settings m_Settings = Settings.Default;
#if HAVE_CYSHARP_UNITASK
		// Channels are the backing source for the public async stream API
		private readonly Channel<IPEndPoint> _OnConnectedChannel = Channel.CreateSingleConsumerUnbounded<IPEndPoint>();
		private readonly Channel<AsyncUnit> _OnDisconnectedChannel = Channel.CreateSingleConsumerUnbounded<AsyncUnit>();
		private readonly Channel<PacketResponse> _OnPacketReceivedChannel =
			Channel.CreateSingleConsumerUnbounded<PacketResponse>();
#endif

		// Networking components
		private UdpClient _UDPClient;
		private TcpListener _TCPListener;
		private TcpClient _TCPConnection;

		// State variables
		private bool _IsUdpConnected;
		private bool _IsTcpConnected;
		private float _LastHeartbeatTime;
		private Guid _InstanceId;
#if HAVE_CYSHARP_UNITASK
		private CancellationTokenSource _CancellationTokenSource;
#endif
		private bool _WasRunningBeforePause;

		// Memory management
		private readonly object _DisposeLock = new();
		private bool _IsDisposed;

		#endregion

		#region Unity Lifecycle

#if HAVE_CYSHARP_UNITASK
		private void Awake()
		{
			if (_Instance != null && _Instance != this)
			{
				Debug.LogWarning(
					"[AetherLink] Another instance of AetherLink was found and is being destroyed. Only one instance should exist.");
				Destroy(gameObject);
				return;
			}

			_Instance = this;
			DontDestroyOnLoad(gameObject); // Make the instance persistent across scene loads.

			_Statistics.Reset();
		}
#endif

		private void OnEnable()
		{
			if (m_Settings.StartOnEnable) StartLink();
		}

		private void OnDisable()
		{
			StopLink();
		}

		private void OnDestroy()
		{
			DisposeResources();
		}

		/// <summary>
		///     Handles application pausing to optionally stop and resume the link.
		/// </summary>
		/// <param name="_pauseStatus">True if the application is pausing, false if resuming.</param>
		private void OnApplicationPause(bool _pauseStatus)
		{
			if (!m_Settings.StopOnPause) return;

			if (_pauseStatus)
			{
				_WasRunningBeforePause = IsRunning;
				if (_WasRunningBeforePause) StopLink();
			}
			else if (_WasRunningBeforePause)
			{
				StartLink();
			}
		}

		#endregion

		#region Public Control Methods

		/// <summary>
		///     Initializes the link with custom settings. Must be called before starting if not using Inspector values.
		/// </summary>
		/// <param name="_settings">The configuration to use.</param>
		public void Initialize(Settings _settings)
		{
			if (IsRunning)
			{
				Debug.LogWarning("[AetherLink] Cannot initialize while the link is running. Please stop it first.");
				return;
			}

			m_Settings = _settings;
		}

		/// <summary>
		///     Starts all networking operations, including discovery and listening.
		/// </summary>
		public void StartLink()
		{
			if (IsRunning)
			{
				Debug.LogWarning("[AetherLink] Link is already running.");
				return;
			}

			if (_IsDisposed)
			{
				Debug.LogError("[AetherLink] Cannot start link on disposed component.");
				return;
			}

			Debug.Log($"[AetherLink] Starting in {m_Settings.LinkMode} mode.");
			IsRunning = true;
			_InstanceId = Guid.NewGuid();
#if HAVE_CYSHARP_UNITASK
			_CancellationTokenSource = new CancellationTokenSource();
			_Statistics.ConnectionAttempts++;
			InitializeAndStartLoops(_CancellationTokenSource.Token).Forget();
#endif
		}

		/// <summary>
		///     Stops all networking operations and cleans up all resources gracefully.
		/// </summary>
		public void StopLink()
		{
			if (!IsRunning) return;

			Debug.Log("[AetherLink] Stopping link...");
			IsRunning = false;
			if (_IsTcpConnected) _Statistics.DisconnectionCount++;

			CleanupNetworkResources();
		}

		/// <summary>
		///     Resets network statistics.
		/// </summary>
		public void ResetStatistics()
		{
			_Statistics.Reset();
		}

		#endregion

		#region Resource Management

		/// <summary>
		///     Safely closes and disposes all active network resources.
		/// </summary>
		private void CleanupNetworkResources()
		{
			try
			{
#if HAVE_CYSHARP_UNITASK
				_CancellationTokenSource?.Cancel();
				_CancellationTokenSource?.Dispose();
				_CancellationTokenSource = null;
#endif

				_UDPClient?.Close();
				_UDPClient?.Dispose();
				_UDPClient = null;

				_TCPListener?.Stop();
				_TCPListener = null;

				_TCPConnection?.Close();
				_TCPConnection?.Dispose();
				_TCPConnection = null;

				_IsTcpConnected = false;
				_IsUdpConnected = false;
				RemoteEndPoint = null;
			}
			catch (Exception ex)
			{
				Debug.LogError($"[AetherLink] Error during resource cleanup: {ex.Message}");
			}
		}

		/// <summary>
		///     Final resource disposal, called when the component is destroyed.
		/// </summary>
		private void DisposeResources()
		{
			lock (_DisposeLock)
			{
				if (_IsDisposed) return;
				_IsDisposed = true;

				StopLink();

#if HAVE_CYSHARP_UNITASK
				// Complete and dispose channels
				_OnConnectedChannel.Writer.TryComplete();
				_OnDisconnectedChannel.Writer.TryComplete();
				_OnPacketReceivedChannel.Writer.TryComplete();
#endif
			}
		}

		#endregion

		#region Data Sending

		/// <summary>
		///     Sends an array of objects as binary data in a fire-and-forget manner.
		/// </summary>
		/// <param name="_header">The ushort header code identifying the message type.</param>
		/// <param name="_data">Array of objects to send.</param>
		public void SendData(ushort _header, params object[] _data)
		{
#if HAVE_CYSHARP_UNITASK
			SendDataAsync(_header, _data).Forget();
#endif
		}

#if HAVE_CYSHARP_UNITASK
		/// <summary>
		///     Asynchronously sends an array of objects as binary data and waits for the send operation to complete.
		/// </summary>
		/// <param name="_header">The ushort header code identifying the message type.</param>
		/// <param name="_data">Array of objects to send.</param>
		/// <returns>A UniTask that completes when the data has been sent.</returns>
		public async UniTask SendDataAsync(ushort _header, params object[] _data)
		{
			if (_header == _TCP_CMD_HEARTBEAT)
				throw new ArgumentException(
					$"Header value {_TCP_CMD_HEARTBEAT} is reserved for internal AetherLink use and cannot be used to send data.",
					nameof(_header));

			if (_data == null)
			{
				Debug.LogError("[AetherLink] Data array cannot be null.");
				return;
			}

			try
			{
				var payload = SerializeObjects(_data);

				if (payload.Length > m_Settings.MaxPacketSize)
				{
					Debug.LogError(
						$"[AetherLink] Packet size ({payload.Length}) exceeds maximum allowed size ({m_Settings.MaxPacketSize}).");
					return;
				}

				await SendDataInternalAsync(_header, payload);
			}
			catch (Exception ex)
			{
				Debug.LogError($"[AetherLink] Failed to serialize or send data for header {_header}: {ex.Message}");
			}
		}
#endif

		/// <summary>
		///     Serializes an array of objects into a compact binary format.
		/// </summary>
		/// <param name="_objects">The array of objects to serialize.</param>
		/// <returns>A byte array representing the serialized objects.</returns>
		private byte[] SerializeObjects(object[] _objects)
		{
			using var stream = new MemoryStream();
			using var writer = new BinaryWriter(stream);
			foreach (var obj in _objects) WriteObject(writer, obj);
			return stream.ToArray();
		}

		/// <summary>
		///     Writes a single object to the binary stream based on its type.
		/// </summary>
		/// <param name="_writer">The binary writer to write to.</param>
		/// <param name="_obj">The object to write.</param>
		private void WriteObject(BinaryWriter _writer, object _obj)
		{
			if (_obj == null)
			{
				_writer.Write((byte)TypeCode.Empty);
				return;
			}

			var type = _obj.GetType();

			if (_obj is IAetherSerializable serializable)
			{
				if (CustomTypeIds.TryGetValue(type, out var typeId))
				{
					_writer.Write((byte)TypeCode.Object); // Signal a custom-registered type
					_writer.Write(typeId);
					serializable.Serialize(_writer);
				}
				else
				{
					throw new NotSupportedException(
						$"Type {type.Name} implements IAetherSerializable but has not been registered with AetherLink.RegisterSerializableType().");
				}
			}
			else // Handle primitive types
			{
				var typeCode = Type.GetTypeCode(type);
				_writer.Write((byte)typeCode);
				switch (typeCode)
				{
					case TypeCode.Boolean: _writer.Write((bool)_obj); break;
					case TypeCode.Byte: _writer.Write((byte)_obj); break;
					case TypeCode.SByte: _writer.Write((sbyte)_obj); break;
					case TypeCode.Int16: _writer.Write((short)_obj); break;
					case TypeCode.UInt16: _writer.Write((ushort)_obj); break;
					case TypeCode.Int32: _writer.Write((int)_obj); break;
					case TypeCode.UInt32: _writer.Write((uint)_obj); break;
					case TypeCode.Int64: _writer.Write((long)_obj); break;
					case TypeCode.UInt64: _writer.Write((ulong)_obj); break;
					case TypeCode.Single: _writer.Write((float)_obj); break;
					case TypeCode.Double: _writer.Write((double)_obj); break;
					case TypeCode.String: _writer.Write((string)_obj); break;
					default:
						throw new NotSupportedException(
							$"Type {type.Name} is not supported for binary serialization. Implement IAetherSerializable and register the type.");
				}
			}
		}

#if HAVE_CYSHARP_UNITASK

		/// <summary>
		///     Sends a data packet asynchronously using the internal TCP connection.
		/// </summary>
		/// <param name="_header">
		///     The header value for the packet, representing its type or purpose.
		/// </param>
		/// <param name="_payload">
		///     The byte array containing the payload data to be sent over the network.
		/// </param>
		/// <returns>
		///     A UniTask representing the asynchronous operation. Completes when the data has been sent or in case of an error.
		/// </returns>
		/// <exception cref="ArgumentException">
		///     Thrown when the payload or packet data is invalid.
		/// </exception>
		/// <exception cref="IOException">
		///     Thrown when an I/O error occurs during the network operation.
		/// </exception>
		/// <exception cref="ObjectDisposedException">
		///     Thrown when attempting to send data using a disposed connection.
		/// </exception>
		/// <exception cref="Exception">
		///     Thrown when an unexpected error occurs during the operation, allowing further error handling by the caller.
		/// </exception>
		private async UniTask SendDataInternalAsync(ushort _header, byte[] _payload)
		{
			if (!IsConnected || _TCPConnection == null)
			{
				Debug.LogWarning("[AetherLink] Cannot send data: Not connected");
				return;
			}

			try
			{
				var packet = CreateTcpPacket(_header, _payload);
				var stream = _TCPConnection.GetStream();

				await stream.WriteAsync(packet, 0, packet.Length, _CancellationTokenSource.Token);
				await stream.FlushAsync(_CancellationTokenSource.Token);

				_Statistics.PacketsSent++;
				_Statistics.BytesSent += packet.Length;

				if (m_Settings.DebugTcpMessages && _header != _TCP_CMD_HEARTBEAT)
					Debug.Log($"[AetherLink] Sent TCP packet: Header={_header}, Size={packet.Length} bytes");
			}
			catch (ArgumentException ex)
			{
				Debug.LogError($"[AetherLink] Invalid packet data: {ex.Message}");
			}
			catch (IOException ex)
			{
				Debug.LogError($"[AetherLink] Network I/O error: {ex.Message}");
				await HandleDisconnection();
			}
			catch (ObjectDisposedException)
			{
				Debug.LogWarning("[AetherLink] Cannot send data: Connection disposed");
				await HandleDisconnection();
			}
			catch (Exception ex)
			{
				Debug.LogError($"[AetherLink] Unexpected error sending data: {ex.Message}");
				// Rethrow to allow the calling method to handle it
				throw;
			}
		}
#endif

		#endregion

		#region Packet Creation

		/// <summary>
		///     Creates a TCP packet with proper framing and integrity checking.
		///     Packet format: [MAGIC_START][length][header][payload][checksum][MAGIC_END]
		/// </summary>
		/// <param name="_header">Header identifier for the packet</param>
		/// <param name="_payload">Payload data to include in the packet</param>
		/// <returns>Complete TCP packet ready for transmission</returns>
		/// <exception cref="ArgumentException">Thrown when payload exceeds maximum size</exception>
		private byte[] CreateTcpPacket(ushort _header, byte[] _payload)
		{
			_payload ??= Array.Empty<byte>();

			if (_payload.Length > m_Settings.MaxPacketSize)
				throw new ArgumentException(
					$"Payload too large: {_payload.Length} bytes exceeds maximum {m_Settings.MaxPacketSize} bytes");

			var payloadLength = (ushort)_payload.Length;
			var totalPacketSize = MagicStartBytes.Length + _PAYLOAD_LENGTH_OFFSET + _HEADER_OFFSET + _payload.Length +
			                      _CHECKSUM_OFFSET + MagicEndBytes.Length;
			var packet = new byte[totalPacketSize];

			var offset = 0;

			Buffer.BlockCopy(MagicStartBytes, 0, packet, offset, MagicStartBytes.Length);
			offset += MagicStartBytes.Length;

			Buffer.BlockCopy(BitConverter.GetBytes(payloadLength), 0, packet, offset, _PAYLOAD_LENGTH_OFFSET);
			offset += _PAYLOAD_LENGTH_OFFSET;

			Buffer.BlockCopy(BitConverter.GetBytes(_header), 0, packet, offset, _HEADER_OFFSET);
			offset += _HEADER_OFFSET;

			Buffer.BlockCopy(_payload, 0, packet, offset, _payload.Length);
			offset += _payload.Length;

			var checksum = CalculateSimpleChecksum(packet, 0, offset);
			packet[offset] = checksum;
			offset += _CHECKSUM_OFFSET;

			Buffer.BlockCopy(MagicEndBytes, 0, packet, offset, MagicEndBytes.Length);

			return packet;
		}

		/// <summary>
		///     Calculates a simple checksum for a specified segment of a byte array.
		///     The checksum is computed as the sum of the byte values in the range
		///     defined by the provided offset and length.
		/// </summary>
		/// <param name="_data">The byte array containing the data for which the checksum is to be calculated.</param>
		/// <param name="_offset">
		///     The zero-based index in the byte array from which to start calculating the checksum.
		/// </param>
		/// <param name="_length">The number of bytes to include in the checksum calculation starting from the offset.</param>
		/// <returns>A single byte representing the calculated checksum of the specified segment.</returns>
		private byte CalculateSimpleChecksum(byte[] _data, int _offset, int _length)
		{
			byte sum = 0;
			for (var i = _offset; i < _offset + _length; i++) sum += _data[i];
			return sum;
		}

		/// <summary>
		///     Creates the UDP packet with a specified command and includes the unique instance ID within the packet.
		/// </summary>
		/// <param name="_command">
		///     The command identifier to be placed as the first byte of the UDP packet.
		/// </param>
		/// <returns>
		///     A byte array representing the UDP packet with a predefined size, where the first byte is the command,
		///     and the later bytes include the GUID of the current instance.
		/// </returns>
		private byte[] CreateUdpPacket(byte _command)
		{
			var packet = new byte[_UDP_PACKET_SIZE];
			packet[0] = _command;
			Buffer.BlockCopy(_InstanceId.ToByteArray(), 0, packet, 1, 16);
			return packet;
		}

		#endregion

		#region Core Loops & Connection Logic

#if HAVE_CYSHARP_UNITASK
		/// <summary>
		///     Initializes the networking components and starts the execution loops for the AetherLink based
		///     on the current configuration and mode (Master or Slave).
		/// </summary>
		/// <param name="_token">
		///     The cancellation token used to manage the running tasks and handle cancellation requests.
		/// </param>
		/// <returns>
		///     A UniTaskVoid that can execute asynchronously. It completes when all the initialization and
		///     networking loops have started successfully.
		/// </returns>
		private async UniTaskVoid InitializeAndStartLoops(CancellationToken _token)
		{
			try
			{
				_UDPClient = new UdpClient();
				_UDPClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
				_UDPClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
				_UDPClient.Client.ReceiveBufferSize = m_Settings.TcpBufferSize;
				_UDPClient.Client.SendBufferSize = m_Settings.TcpBufferSize;
				_UDPClient.EnableBroadcast = true;
				_UDPClient.Client.Bind(new IPEndPoint(IPAddress.Any, m_Settings.UdpBroadcastPort));
			}
			catch (Exception e)
			{
				Debug.LogError($"[AetherLink] Failed to initialize UDP client: {e.Message}");
				StopLink();
				return;
			}

			var tasks = new List<UniTask>
			{
				HeartbeatLoop(_token),
				HeartbeatMonitorLoop(_token)
			};

			if (m_Settings.LinkMode == Mode.Master)
			{
				tasks.Add(TcpListenLoop(_token));
				tasks.Add(UdpHandshakeBroadcastLoop(_token));
			}
			else // Slave
			{
				tasks.Add(UdpListenLoop(_token)); // Only a slave needs to listen for discovery
				tasks.Add(SameMachineFallbackLoop(_token));
			}

			await UniTask.WhenAll(tasks).SuppressCancellationThrow();
		}

		/// <summary>
		///     Handles the TCP receive loop for processing incoming data streams.
		///     Continuously reads data from the TCP connection, decodes packets, and processes them
		///     until the cancellation token is triggered or the connection is closed.
		/// </summary>
		/// <param name="_token">
		///     The cancellation token used to signal the termination of the receiver loop.
		/// </param>
		/// <returns>
		///     A UniTask representing the asynchronous operation of the receiver loop.
		///     This completes when the loop ends due to cancellation or connection disconnection.
		/// </returns>
		private async UniTask TcpReceiveLoop(CancellationToken _token)
		{
			var stream = _TCPConnection.GetStream();
			var magicBuffer = new byte[2];
			var lengthBuffer = new byte[2];

			try
			{
				while (!_token.IsCancellationRequested && _TCPConnection.Connected)
				{
					if (!IsConnectionValid(_TCPConnection))
					{
						Debug.Log("[AetherLink] TCP connection is no longer valid.");
						break;
					}

					// Read magic start
					await ReadExactlyAsync(stream, magicBuffer, _MAGIC_OFFSET, _token);
					var magicStart = BitConverter.ToUInt16(magicBuffer, 0);
					if (magicStart != _MAGIC_START)
					{
						Debug.LogError("[AetherLink] Invalid magic start. Disconnecting.");
						_Statistics.MalformedPackets++;
						break;
					}

					// Read payload length
					await ReadExactlyAsync(stream, lengthBuffer, _PAYLOAD_LENGTH_OFFSET, _token);
					var payloadLength = BitConverter.ToUInt16(lengthBuffer, 0);

					if (payloadLength > m_Settings.MaxPacketSize)
					{
						Debug.LogError($"[AetherLink] Payload too large: {payloadLength}. Disconnecting.");
						_Statistics.MalformedPackets++;
						break;
					}

					// Read the complete packet body (header + payload + checksum + magic end)
					var bodySize = _HEADER_OFFSET + payloadLength + _CHECKSUM_OFFSET + _MAGIC_OFFSET;
					var bodyBuffer = new byte[bodySize];
					await ReadExactlyAsync(stream, bodyBuffer, bodySize, _token);

					// Reconstruct a full packet for validation
					var fullPacket = new byte[_MAGIC_OFFSET + _PAYLOAD_LENGTH_OFFSET + bodySize];
					Buffer.BlockCopy(magicBuffer, 0, fullPacket, 0, _MAGIC_OFFSET);
					Buffer.BlockCopy(lengthBuffer, 0, fullPacket, _PAYLOAD_LENGTH_OFFSET, _PAYLOAD_LENGTH_OFFSET);
					Buffer.BlockCopy(bodyBuffer, 0, fullPacket, _MAGIC_OFFSET + _PAYLOAD_LENGTH_OFFSET, bodySize);

					// Validate packet integrity
					if (!ValidatePacketIntegrity(fullPacket))
					{
						Debug.LogError("[AetherLink] Packet integrity check failed. Dropping packet.");
						_Statistics.CorruptedPackets++;
						continue;
					}

					// Extract header
					var header = BitConverter.ToUInt16(bodyBuffer, 0);

					// Check if it's an internal heartbeat packet
					if (header == _TCP_CMD_HEARTBEAT)
					{
						_LastHeartbeatTime = Time.time;
						continue; // Skip the rest of the loop for heartbeats
					}

					// It's a regular data packet, extract payload
					var payload = new byte[payloadLength];
					Buffer.BlockCopy(bodyBuffer, _HEADER_OFFSET, payload, 0, payloadLength);

					_Statistics.PacketsReceived++;
					_Statistics.BytesReceived += fullPacket.Length;
					_Statistics.LastPacketTime = Time.time;

					if (m_Settings.DebugTcpMessages)
						Debug.Log($"[AetherLink] Received TCP packet: Header={header}, Size={fullPacket.Length} bytes");

					var response = new PacketResponse(header, payload, RemoteEndPoint);

					// Write to the channel for the async API
					_OnPacketReceivedChannel.Writer.TryWrite(response);

					// Post the UnityEvent invocation to the main thread
					await UniTask.SwitchToMainThread();
					OnPacketReceivedInspector?.Invoke(response);
				}
			}
			catch (Exception ex)
			{
				if (!_token.IsCancellationRequested) Debug.LogError($"[AetherLink] TCP receive error: {ex.Message}");
			}
			finally
			{
				await HandleDisconnection();
			}
		}
#endif
		/// <summary>
		///     Validates the integrity of a network packet by checking its structure, magic numbers,
		///     and checksum.
		/// </summary>
		/// <param name="_packet">
		///     The complete network packet as a byte array, including headers, payload,
		///     checksum, and magic numbers.
		/// </param>
		/// <returns>
		///     Returns true if the packet's structure and checksum are valid; otherwise, false.
		/// </returns>
		private bool ValidatePacketIntegrity(byte[] _packet)
		{
			const ushort minPacketSize = _MAGIC_OFFSET + _HEADER_OFFSET + _PAYLOAD_LENGTH_OFFSET + _CHECKSUM_OFFSET +
			                             _MAGIC_OFFSET;
			if (_packet.Length < minPacketSize) return false; // Minimum packet size

			var magicStart = BitConverter.ToUInt16(_packet, 0);
			if (magicStart != _MAGIC_START) return false;

			var magicEnd = BitConverter.ToUInt16(_packet, _packet.Length - _MAGIC_OFFSET);
			if (magicEnd != _MAGIC_END) return false;

			var receivedChecksum = _packet[^(_CHECKSUM_OFFSET + _MAGIC_OFFSET)];
			var calculatedChecksum =
				CalculateSimpleChecksum(_packet, 0, _packet.Length - (_CHECKSUM_OFFSET + _MAGIC_OFFSET));

			return receivedChecksum == calculatedChecksum;
		}

		/// <summary>
		///     Validates whether the given TCP client connection is active and still functional.
		/// </summary>
		/// <param name="_client">The TCP client to validate.</param>
		/// <returns>
		///     True if the TCP connection is valid (connected, socket is functional, and not in a disconnected state);
		///     otherwise, false.
		/// </returns>
		private bool IsConnectionValid(TcpClient _client)
		{
			try
			{
				if (_client?.Client == null) return false;
				if (!_client.Connected) return false;

				return !_client.Client.Poll(_CONNECTION_VALIDATION_DELAY_MICRO_SEC, SelectMode.SelectRead) ||
				       _client.Client.Available > 0;
			}
			catch
			{
				return false;
			}
		}

#if HAVE_CYSHARP_UNITASK
		/// <summary>
		///     Continuously listens for incoming UDP packets and processes them for the discovery mechanism.
		///     Intended to run as a background task for the slave mode.
		/// </summary>
		/// <param name="_token">The cancellation token used to stop the loop gracefully.</param>
		/// <returns>A UniTask representing the asynchronous UDP listen operation.</returns>
		private async UniTask UdpListenLoop(CancellationToken _token)
		{
			while (!_token.IsCancellationRequested)
				try
				{
					var result = await _UDPClient.ReceiveAsync().AsUniTask().AttachExternalCancellation(_token);
					if (result.Buffer.Length != _UDP_PACKET_SIZE) continue;

					var command = result.Buffer[0];
					var guidBytes = new byte[_GUID_BYTE_SIZE];
					Buffer.BlockCopy(result.Buffer, _UDP_HEADER_SIZE, guidBytes, 0, _GUID_BYTE_SIZE);
					var senderId = new Guid(guidBytes);

					if (senderId == _InstanceId) continue;

					await UniTask.SwitchToMainThread();
					HandleUdpMessage(command, result.RemoteEndPoint);
				}
				catch (OperationCanceledException)
				{
					break;
				}
				catch (ObjectDisposedException)
				{
					break;
				}
				catch (ArgumentOutOfRangeException ex)
				{
					Debug.LogError($"Invalid packet data: {ex.Message}");
				}
				catch (ArgumentException ex)
				{
					Debug.LogError($"Packet parsing failed: {ex.Message}");
				}
				catch (Exception ex)
				{
					if (!_token.IsCancellationRequested) Debug.LogError($"[AetherLink] UDP listen error: {ex.Message}");
					break;
				}
		}

		/// <summary>
		///     Processes incoming UDP messages and performs actions based on the command received.
		///     Handles specific UDP commands and optionally initiates connections or logs debug information.
		/// </summary>
		/// <param name="_command">The command identifier from the received UDP message.</param>
		/// <param name="_remoteEndPoint">The endpoint of the remote sender sending the UDP message.</param>
		private void HandleUdpMessage(byte _command, IPEndPoint _remoteEndPoint)
		{
			if (m_Settings.DebugUdpMessages)
				Debug.Log($"[AetherLink] UDP Received: Command '0x{_command:X2}' from {_remoteEndPoint.Address}");

			switch (_command)
			{
				case _UDP_CMD_HANDSHAKE:
					if (m_Settings.LinkMode == Mode.Slave && !IsConnected)
					{
						Debug.Log(
							$"[AetherLink] Master discovered at {_remoteEndPoint.Address}. Initiating TCP connection.");
						_IsUdpConnected = true;
						TcpConnectToMaster(_remoteEndPoint.Address, _CancellationTokenSource.Token).Forget();
					}

					break;
			}
		}

		/// <summary>
		///     Continuously attempts to establish a TCP connection to the master node as a slave while adhering to the provided
		///     settings.
		///     The method ensures that the slave is persistently trying to connect, especially in scenarios where
		///     a same-machine connection is allowed and no active connection exists.
		/// </summary>
		/// <param name="_token">
		///     Token used to observe cancellation requests, allowing the connection loop to stop when requested.
		/// </param>
		/// <returns>
		///     A UniTask representing the asynchronous execution of the slave connection loop until the token is canceled.
		/// </returns>
		private async UniTask SameMachineFallbackLoop(CancellationToken _token)
		{
			while (!_token.IsCancellationRequested)
			{
				if (m_Settings.AllowSameMachineConnection && !IsConnected && !_IsUdpConnected)
					TcpConnectToMaster(IPAddress.Loopback, _token).Forget();
				await UniTask.Delay(m_Settings.HandshakeInterval, cancellationToken: _token);
			}
		}

		/// <summary>
		///     Establishes a TCP connection to the specified Master IP address.
		///     This method attempts to connect asynchronously and handles new TCP connections with success.
		/// </summary>
		/// <param name="_masterIp">
		///     The IP address of the Master server to connect to.
		/// </param>
		/// <param name="_token">
		///     A cancellation token used to observe cancellation requests during the connection process.
		/// </param>
		/// <returns>
		///     A UniTask that represents the asynchronous operation of establishing the TCP connection.
		/// </returns>
		/// <exception cref="OperationCanceledException">
		///     Thrown when the operation is canceled via the provided cancellation token.
		/// </exception>
		/// <exception cref="Exception">
		///     Thrown when an error occurs while attempting to connect to the Master server, excluding cancellation.
		///     Logs detailed connection failure information.
		/// </exception>
		private async UniTask TcpConnectToMaster(IPAddress _masterIp, CancellationToken _token)
		{
			if (IsConnected) return;

			var client = new TcpClient();
			try
			{
				client.ReceiveBufferSize = m_Settings.TcpBufferSize;
				client.SendBufferSize = m_Settings.TcpBufferSize;
				await client.ConnectAsync(_masterIp, m_Settings.TcpConnectionPort).AsUniTask()
					.AttachExternalCancellation(_token);
				await UniTask.SwitchToMainThread();
				HandleNewTcpConnection(client, _token);
			}
			catch (Exception ex)
			{
				if (!_token.IsCancellationRequested)
					Debug.Log($"[AetherLink] Failed to connect to Master at {_masterIp}: {ex.Message}.");
				client.Close();
				client.Dispose();
			}
		}

		/// <summary>
		///     Handles the disconnection of the TCP connection within AetherLink.
		///     This method ensures proper cleanup and state updates upon a disconnection.
		///     It increments the disconnection count, resets connection-related state,
		///     and publishes disconnection events through both the async stream API
		///     and Unity's Inspector UnityEvent.
		/// </summary>
		/// <returns>
		///     A UniTask that completes when the disconnection handling process is finalized.
		/// </returns>
		private async UniTask HandleDisconnection()
		{
			await UniTask.SwitchToMainThread();
			if (!_IsTcpConnected) return;

			Debug.Log("[AetherLink] Handling disconnection...");
			_IsTcpConnected = false;
			_IsUdpConnected = false;
			_Statistics.DisconnectionCount++;

			_TCPConnection?.Close();
			_TCPConnection?.Dispose();
			_TCPConnection = null;
			RemoteEndPoint = null;

			// Publish to both the async stream API and the Inspector UnityEvent
			_OnDisconnectedChannel.Writer.TryWrite(AsyncUnit.Default);
			OnDisconnectedInspector?.Invoke();
		}

		/// <summary>
		///     Handles the initialization of a new TCP connection.
		///     Configures the client settings, sets up the connection state, and triggers
		///     connection-related events or handlers.
		/// </summary>
		/// <param name="_client">
		///     The TCPClient instance representing the newly established connection.
		/// </param>
		/// <param name="_token">
		///     A CancellationToken used to handle operation cancellation during connection processing.
		/// </param>
		private void HandleNewTcpConnection(TcpClient _client, CancellationToken _token)
		{
			_TCPConnection = _client;
			if (_TCPConnection.Client?.RemoteEndPoint is IPEndPoint remoteEndPoint)
			{
				Debug.Log($"[AetherLink] TCP connection established with {remoteEndPoint.Address}");
				RemoteEndPoint = remoteEndPoint;
				_IsUdpConnected = true;
				_IsTcpConnected = true;
				_LastHeartbeatTime = Time.time;

				// Publish to both the async stream API and the Inspector UnityEvent
				_OnConnectedChannel.Writer.TryWrite(remoteEndPoint);
				OnConnectedInspector?.Invoke(remoteEndPoint);

				TcpReceiveLoop(_token).Forget();
			}
		}

		/// <summary>
		///     Executes a loop to send UDP handshake packets at regular intervals for discovery and connection initialization.
		///     Broadcasts handshake packets to a specified port until a connection is established or the operation is canceled.
		/// </summary>
		/// <param name="_token">
		///     A CancellationToken used to manage the lifetime of the loop. The loop stops when the token is canceled.
		/// </param>
		/// <returns>
		///     A UniTask representing the asynchronous operation of the UDP handshake broadcast loop.
		/// </returns>
		/// <exception cref="Exception">
		///     Thrown if an error occurs during the operation while the cancellation token is not yet canceled.
		/// </exception>
		private async UniTask UdpHandshakeBroadcastLoop(CancellationToken _token)
		{
			try
			{
				var handshakeBytes = CreateUdpPacket(_UDP_CMD_HANDSHAKE);
				var broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, m_Settings.UdpBroadcastPort);
				while (!_token.IsCancellationRequested)
				{
					if (!IsConnected)
						try
						{
							if (m_Settings.DebugUdpMessages)
								Debug.Log("[AetherLink] Broadcasting UDP handshake packet...");
							await _UDPClient.SendAsync(handshakeBytes, handshakeBytes.Length, broadcastEndpoint);
						}
						catch (Exception ex)
						{
							if (!_token.IsCancellationRequested)
								Debug.LogError($"[AetherLink] UDP handshake broadcast error: {ex.Message}");
						}

					await UniTask.Delay(m_Settings.HandshakeInterval, cancellationToken: _token);
				}
			}
			catch (Exception e)
			{
				Debug.LogError($"[AetherLink] UdpHandshakeBroadcastLoop error: {e.Message}");
				throw;
			}
		}

		/// <summary>
		///     Continuously sends heartbeat signals to maintain a connection while the link is active.
		///     It sends periodic messages to the remote endpoint to ensure the connection remains alive.
		/// </summary>
		/// <param name="_token">
		///     A <see cref="CancellationToken" /> that monitors for cancellation requests, allowing
		///     the loop to terminate gracefully when the operation is no longer needed.
		/// </param>
		/// <returns>
		///     A <see cref="UniTask" /> representing the asynchronous heartbeat loop operation.
		/// </returns>
		private async UniTask HeartbeatLoop(CancellationToken _token)
		{
			while (!_token.IsCancellationRequested)
			{
				if (IsConnected) await SendDataInternalAsync(_TCP_CMD_HEARTBEAT, Array.Empty<byte>());
				await UniTask.Delay(m_Settings.HeartbeatInterval, cancellationToken: _token);
			}
		}

		/// <summary>
		///     Monitors the connection by validating the time difference between heartbeats. If the time exceeds the
		///     configured heartbeat timeout, it triggers a disconnection process.
		/// </summary>
		/// <param name="_token">
		///     A CancellationToken used to stop the loop when cancellation is requested.
		/// </param>
		/// <returns>
		///     A UniTask representing the asynchronous monitoring operation.
		/// </returns>
		private async UniTask HeartbeatMonitorLoop(CancellationToken _token)
		{
			while (!_token.IsCancellationRequested)
			{
				if (IsConnected && Time.time - _LastHeartbeatTime > m_Settings.HeartbeatTimeout * _MS_TO_SEC_MULTIPLIER)
				{
					Debug.LogWarning("[AetherLink] Connection lost due to heartbeat timeout!");
					await HandleDisconnection();
				}

				await UniTask.Delay(250, cancellationToken: _token);
			}
		}

		/// <summary>
		///     Listens for incoming TCP connections and handles new client connections asynchronously.
		///     This method is designed to run in a background task and continues until the provided cancellation token is
		///     triggered.
		/// </summary>
		/// <param name="_token">
		///     A cancellation token used to gracefully terminate the listening loop. When triggered, the listener will stop
		///     accepting new connections.
		/// </param>
		/// <returns>
		///     A UniTask representing the asynchronous operation of the listening loop.
		/// </returns>
		/// <exception cref="OperationCanceledException">
		///     Thrown when the operation is canceled via the provided cancellation token.
		/// </exception>
		/// <exception cref="ObjectDisposedException">
		///     Thrown when the TCP listener is disposed while the loop is running.
		/// </exception>
		/// <exception cref="Exception">
		///     Logs and stops the link if any unexpected errors occur during the listening loop.
		/// </exception>
		private async UniTask TcpListenLoop(CancellationToken _token)
		{
			try
			{
				_TCPListener = new TcpListener(IPAddress.Any, m_Settings.TcpConnectionPort);
				_TCPListener.Start();
				while (!_token.IsCancellationRequested)
				{
					if (IsConnected)
					{
						await UniTask.Yield(_token);
						continue;
					}

					var client = await _TCPListener.AcceptTcpClientAsync().AsUniTask()
						.AttachExternalCancellation(_token);
					client.ReceiveBufferSize = m_Settings.TcpBufferSize;
					client.SendBufferSize = m_Settings.TcpBufferSize;
					await UniTask.SwitchToMainThread();
					HandleNewTcpConnection(client, _token);
				}
			}
			catch (OperationCanceledException)
			{
				/* Expected */
			}
			catch (ObjectDisposedException)
			{
				/* Expected */
			}
			catch (Exception ex)
			{
				if (!_token.IsCancellationRequested)
				{
					Debug.LogError($"[AetherLink] TCP listen error: {ex.Message}");
					StopLink();
				}
			}
			finally
			{
				_TCPListener?.Stop();
			}
		}

		/// <summary>
		///     Reads an exact number of bytes from a network stream into the specified buffer.
		///     Continues reading until the specified number of bytes is read or the stream is closed.
		/// </summary>
		/// <param name="_stream">The network streams to read data from.</param>
		/// <param name="_buffer">The buffer to store the data read from the stream.</param>
		/// <param name="_bytesToRead">The number of bytes to read from the stream.</param>
		/// <param name="_token">A cancellation token to observe during the operation.</param>
		/// <returns>The total number of bytes read, which will equal the specified number unless an exception is thrown.</returns>
		/// <exception cref="EndOfStreamException">Thrown if the connection is closed before the requested number of bytes is read.</exception>
		private async UniTask<int> ReadExactlyAsync(NetworkStream _stream, byte[] _buffer, int _bytesToRead,
			CancellationToken _token)
		{
			var totalBytesRead = 0;
			while (totalBytesRead < _bytesToRead)
			{
				var bytesRead = await _stream.ReadAsync(_buffer, totalBytesRead, _bytesToRead - totalBytesRead, _token);
				if (bytesRead == 0) throw new EndOfStreamException("Connection closed prematurely.");
				totalBytesRead += bytesRead;
			}

			return totalBytesRead;
		}
#endif

		#endregion
	}
}
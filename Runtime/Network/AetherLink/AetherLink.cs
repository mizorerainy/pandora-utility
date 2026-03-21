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
// - High-Performance Serialization: Zero-allocation structuring using generic structs and PtrToStructure/StructureToPtr.
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
							PandoraLogger.LogNetwork("An instance was automatically created.");
						}
					}

					return _Instance;
				}
			}
		}

		#endregion



		#region Constants

		/// <summary>
		///     Defines the operational role of the AetherLink instance.
		/// </summary>
		public enum Mode
		{
			Master = 0,
			Slave = 1
		}

		private const byte _UDP_CMD_HANDSHAKE = 0xAB;
		private const int _UDP_PACKET_SIZE = 21;
		private const int _GUID_BYTE_SIZE = 16;
		private const int _UDP_HEADER_SIZE = _UDP_PACKET_SIZE - _GUID_BYTE_SIZE;
		private const uint _APP_SIGNATURE = 0x50414E44; // "PAND" magic signature for UDP discovery

		private const ushort _TCP_CMD_HEARTBEAT = 0xFAF0; // Internal heartbeat will not be exposed to the user.

		private const ushort _MAGIC_START = 0xAE77;
		private const ushort _MAGIC_END = 0x77EA;

		private const int _PAYLOAD_LENGTH_OFFSET = 2;
		private const int _HEADER_OFFSET = 2;
		private const int _MAGIC_OFFSET = 2;
		private const int _CHECKSUM_OFFSET = 2; // using Fletcher-16 (2 bytes)

		private const float _MS_TO_SEC_MULTIPLIER = .001f;

		private const int _CONNECTION_VALIDATION_DELAY_MICRO_SEC = 100;

		private static readonly byte[] MagicStartBytes = BitConverter.GetBytes(_MAGIC_START);
		private static readonly byte[] MagicEndBytes = BitConverter.GetBytes(_MAGIC_END);

		#endregion

		#region Properties

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


		#region Events

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



		#region Fields

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
				PandoraLogger.LogNetworkWarning(
					"Another instance of AetherLink was found and is being destroyed. Only one instance should exist.");
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
				PandoraLogger.LogNetworkError($"Error during resource cleanup: {ex.Message}");
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



	}
}

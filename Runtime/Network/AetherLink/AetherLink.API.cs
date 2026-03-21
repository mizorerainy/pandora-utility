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
	// ReSharper disable once PartialTypeWithSinglePart
	public partial class AetherLink : MonoBehaviour
	{
		#region Public API - Managed Handlers

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
						PandoraLogger.LogNetworkError($"Error in user-provided connection handler: {ex}");
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
						PandoraLogger.LogNetworkError($"Error in user-provided disconnection handler: {ex}");
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
						PandoraLogger.LogNetworkError(
							$"Error in user-provided data handler for header {_packet.Header}: {ex}");
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
						PandoraLogger.LogNetworkError(
							$"Error in user-provided data handler for header {_packet.Header}: {ex}");
					}
				}, _cancellationToken);
			}).Forget();
		}
#endif

		#endregion

		#region Public API - Async Streams

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

		#region Public API

		/// <summary>
		///     Initializes the link with custom settings. Must be called before starting if not using Inspector values.
		/// </summary>
		/// <param name="_settings">The configuration to use.</param>
		public void Initialize(Settings _settings)
		{
			if (IsRunning)
			{
				PandoraLogger.LogNetworkWarning("Cannot initialize while the link is running. Please stop it first.");
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
				PandoraLogger.LogNetworkWarning("Link is already running.");
				return;
			}

			if (_IsDisposed)
			{
				PandoraLogger.LogNetworkError("Cannot start link on disposed component.");
				return;
			}

			PandoraLogger.LogNetwork($"Starting in {m_Settings.LinkMode} mode.");
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

			PandoraLogger.LogNetwork("Stopping link...");
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

	}
}

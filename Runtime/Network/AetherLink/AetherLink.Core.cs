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
				PandoraLogger.LogNetworkError($"Failed to initialize UDP client: {e.Message}");
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
						PandoraLogger.LogNetwork("TCP connection is no longer valid.");
						break;
					}

					// Read magic start
					await ReadExactlyAsync(stream, magicBuffer, _MAGIC_OFFSET, _token);
					var magicStart = BitConverter.ToUInt16(magicBuffer, 0);
					if (magicStart != _MAGIC_START)
					{
						PandoraLogger.LogNetworkError("Invalid magic start. Disconnecting.");
						_Statistics.MalformedPackets++;
						break;
					}

					// Read payload length
					await ReadExactlyAsync(stream, lengthBuffer, _PAYLOAD_LENGTH_OFFSET, _token);
					var payloadLength = BitConverter.ToUInt16(lengthBuffer, 0);

					if (payloadLength > m_Settings.MaxPacketSize)
					{
						PandoraLogger.LogNetworkError($"Payload too large: {payloadLength}. Disconnecting.");
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
						PandoraLogger.LogNetworkError("Packet integrity check failed. Dropping packet.");
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
						PandoraLogger.LogNetwork($"Received TCP packet: Header={header}, Size={fullPacket.Length} bytes");

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
				if (!_token.IsCancellationRequested) PandoraLogger.LogNetworkError($"TCP receive error: {ex.Message}");
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
					PandoraLogger.LogNetworkError($"Invalid packet data: {ex.Message}");
				}
				catch (ArgumentException ex)
				{
					PandoraLogger.LogNetworkError($"Packet parsing failed: {ex.Message}");
				}
				catch (Exception ex)
				{
					if (!_token.IsCancellationRequested) PandoraLogger.LogNetworkError($"UDP listen error: {ex.Message}");
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
				PandoraLogger.LogNetwork($"UDP Received: Command '0x{_command:X2}' from {_remoteEndPoint.Address}");

			switch (_command)
			{
				case _UDP_CMD_HANDSHAKE:
					if (m_Settings.LinkMode == Mode.Slave && !IsConnected)
					{
						PandoraLogger.LogNetwork(
							$"Master discovered at {_remoteEndPoint.Address}. Initiating TCP connection.");
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
					PandoraLogger.LogNetwork($"Failed to connect to Master at {_masterIp}: {ex.Message}.");
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

			PandoraLogger.LogNetwork("Handling disconnection...");
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
				PandoraLogger.LogNetwork($"TCP connection established with {remoteEndPoint.Address}");
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
								PandoraLogger.LogNetwork("Broadcasting UDP handshake packet...");
							await _UDPClient.SendAsync(handshakeBytes, handshakeBytes.Length, broadcastEndpoint);
						}
						catch (Exception ex)
						{
							if (!_token.IsCancellationRequested)
								PandoraLogger.LogNetworkError($"UDP handshake broadcast error: {ex.Message}");
						}

					await UniTask.Delay(m_Settings.HandshakeInterval, cancellationToken: _token);
				}
			}
			catch (Exception e)
			{
				PandoraLogger.LogNetworkError($"UdpHandshakeBroadcastLoop error: {e.Message}");
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
					PandoraLogger.LogNetworkWarning("Connection lost due to heartbeat timeout!");
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
					PandoraLogger.LogNetworkError($"TCP listen error: {ex.Message}");
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

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
		#region Data Sending

		/// <summary>
		///     Sends a zero-allocation generic struct by converting it directly to bytes. (High Performance)
		/// </summary>
		/// <param name="_header">The ushort header code identifying the message type.</param>
		/// <param name="_data">The unmanaged struct containing the data.</param>
		public void SendData<T>(ushort _header, T _data) where T : unmanaged
		{
#if HAVE_CYSHARP_UNITASK
			SendDataAsync(_header, _data).Forget();
#endif
		}

#if HAVE_CYSHARP_UNITASK
		/// <summary>
		///     Asynchronously sends a zero-allocation generic struct.
		/// </summary>
		public async UniTask SendDataAsync<T>(ushort _header, T _data) where T : unmanaged
		{
			if (_header == _TCP_CMD_HEARTBEAT)
				throw new ArgumentException("Reserved Header.");

			try
			{
				int size = System.Runtime.InteropServices.Marshal.SizeOf<T>();
				if (size > m_Settings.MaxPacketSize)
				{
					PandoraLogger.LogNetworkError($"Packet size ({size}) exceeds maximum allowed size.");
					return;
				}

				byte[] payload = new byte[size];
				var handle = System.Runtime.InteropServices.GCHandle.Alloc(payload, System.Runtime.InteropServices.GCHandleType.Pinned);
				try
				{
					System.Runtime.InteropServices.Marshal.StructureToPtr(_data, handle.AddrOfPinnedObject(), false);
				}
				finally
				{
					handle.Free();
				}

				await SendDataInternalAsync(_header, payload);
			}
			catch (Exception ex)
			{
				PandoraLogger.LogNetworkError($"Failed to prepare memory payload: {ex.Message}");
			}
		}
#endif



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
			if (!IsConnected)
			{
				PandoraLogger.LogNetworkWarning("Cannot send data: Not connected");
				return;
			}

#if UNITY_EDITOR
			if (_IsSimulationMode)
			{
				var simPacket = CreateTcpPacket(_header, _payload);
				_Statistics.PacketsSent++;
				_Statistics.BytesSent += simPacket.Length;

				if (_header != _TCP_CMD_HEARTBEAT)
				{
					PandoraLogger.LogNetwork($"[Simulated] Sent TCP packet: Header={_header}, Size={simPacket.Length} bytes");
				}

				return;
			}
#endif

			if (_TCPConnection == null)
			{
				PandoraLogger.LogNetworkWarning("Cannot send data: No TCP connection");
				return;
			}

			try
			{
				var packet = CreateTcpPacket(_header, _payload);
				var stream = _TCPConnection.GetStream();

				await stream.WriteAsync(packet, 0, packet.Length, _CancellationTokenSource.Token);
				// stream.FlushAsync() removed to allow TCP stream to batch network sends when NoDelay is enabled.

				_Statistics.PacketsSent++;
				_Statistics.BytesSent += packet.Length;

				if (m_Settings.DebugTcpMessages && _header != _TCP_CMD_HEARTBEAT)
					PandoraLogger.LogNetwork($"Sent TCP packet: Header={_header}, Size={packet.Length} bytes");
			}
			catch (ArgumentException ex)
			{
				PandoraLogger.LogNetworkError($"Invalid packet data: {ex.Message}");
			}
			catch (IOException ex)
			{
				PandoraLogger.LogNetworkError($"Network I/O error: {ex.Message}");
				await HandleDisconnection();
			}
			catch (ObjectDisposedException)
			{
				PandoraLogger.LogNetworkWarning("Cannot send data: Connection disposed");
				await HandleDisconnection();
			}
			catch (Exception ex)
			{
				PandoraLogger.LogNetworkError($"Unexpected error sending data: {ex.Message}");
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

			var checksum = CalculateFletcher16(packet, 0, offset);
			Buffer.BlockCopy(BitConverter.GetBytes(checksum), 0, packet, offset, _CHECKSUM_OFFSET);
			offset += _CHECKSUM_OFFSET;

			Buffer.BlockCopy(MagicEndBytes, 0, packet, offset, MagicEndBytes.Length);

			return packet;
		}

		/// <summary>
		///     Calculates a robust Fletcher-16 checksum for a specified segment of a byte array, detecting swapped bytes.
		/// </summary>
		private ushort CalculateFletcher16(byte[] _data, int _offset, int _length)
		{
			ushort sum1 = 0;
			ushort sum2 = 0;

			for (var i = _offset; i < _offset + _length; i++)
			{
				sum1 = (ushort)((sum1 + _data[i]) % 255);
				sum2 = (ushort)((sum2 + sum1) % 255);
			}

			return (ushort)((sum2 << 8) | sum1);
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
			Buffer.BlockCopy(BitConverter.GetBytes(_APP_SIGNATURE), 0, packet, 1, 4);
			Buffer.BlockCopy(_InstanceId.ToByteArray(), 0, packet, 5, 16);
			return packet;
		}

		#endregion

	}
}

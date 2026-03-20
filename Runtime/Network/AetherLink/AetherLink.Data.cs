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
				PandoraLogger.LogNetworkError("Data array cannot be null.");
				return;
			}

			try
			{
				var payload = SerializeObjects(_data);

				if (payload.Length > m_Settings.MaxPacketSize)
				{
					PandoraLogger.LogNetworkError(
						$"Packet size ({payload.Length}) exceeds maximum allowed size ({m_Settings.MaxPacketSize}).");
					return;
				}

				await SendDataInternalAsync(_header, payload);
			}
			catch (Exception ex)
			{
				PandoraLogger.LogNetworkError($"Failed to serialize or send data for header {_header}: {ex.Message}");
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
				PandoraLogger.LogNetworkWarning("Cannot send data: Not connected");
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

	}
}

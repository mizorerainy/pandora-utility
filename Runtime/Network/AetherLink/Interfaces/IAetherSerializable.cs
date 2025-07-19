using System.IO;

namespace MizoreRainy.Pandora.NetworkUtility.Interfaces
{
	/// <summary>
	/// Defines an interface for objects that can be serialized into a binary format for AetherLink.
	/// </summary>
	public interface IAetherSerializable
	{
		/// <summary>
		/// Writes the object's data to a binary stream.
		/// </summary>
		/// <param name="_writer">The BinaryWriter to write data to.</param>
		void Serialize(BinaryWriter _writer);

		/// <summary>
		/// Reads the object's data from a binary stream.
		/// </summary>
		/// <param name="_reader">The BinaryReader to read data from.</param>
		void Deserialize(BinaryReader _reader);
	}
}
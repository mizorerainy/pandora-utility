using System;
using System.IO;
using MizoreRainy.Pandora.NetworkUtility.Interfaces;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace MizoreRainy.Pandora.NetworkUtility
{
    /// <summary>
    ///     This static class provides examples of how to implement IAetherSerializable for custom types
    ///     and registers them with the AetherLink system upon application startup.
    ///     This is the recommended pattern for extending AetherLink's serialization capabilities.
    /// </summary>
    public static class AetherLinkCustomTypes
	{
		// Define unique IDs for your custom types.
		// IMPORTANT: These IDs must be the same on both the client and server applications.
		public const ushort TYPE_ID_TEST_STRUCT = 100;
		public const ushort TYPE_ID_VECTOR3 = 101;
		public const ushort TYPE_ID_QUATERNION = 102;
		public const ushort TYPE_ID_VECTOR2 = 103;
		public const ushort TYPE_ID_COLOR = 104;


        /// <summary>
        ///     Unity calls this method automatically when the application starts, before any scene loads.
        ///     It's the perfect place to register all your custom network types with AetherLink.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		public static void Register()
		{
			AetherLink.RegisterSerializableType<TestStruct>(TYPE_ID_TEST_STRUCT);
			AetherLink.RegisterSerializableType<Vector3Serializable>(TYPE_ID_VECTOR3);
			AetherLink.RegisterSerializableType<QuaternionSerializable>(TYPE_ID_QUATERNION);
			AetherLink.RegisterSerializableType<Vector2Serializable>(TYPE_ID_VECTOR2);
			AetherLink.RegisterSerializableType<ColorSerializable>(TYPE_ID_COLOR);
		}
	}

	// --- Example Implementations ---

    /// <summary>
    ///     An example custom struct that implements IAetherSerializable.
    /// </summary>
    public struct TestStruct : IAetherSerializable
	{
		public int A;
		public bool B;

		public void Serialize(BinaryWriter _writer)
		{
			_writer.Write(A);
			_writer.Write(B);
		}

		public void Deserialize(BinaryReader _reader)
		{
			A = _reader.ReadInt32();
			B = _reader.ReadBoolean();
		}

		public override string ToString()
		{
			return $"TestStruct(A: {A}, B: {B})";
		}
	}

    /// <summary>
    ///     Represents a serializable wrapper for Unity's Vector3 structure, specifically designed to be used
    ///     with the AetherLink serialization system. This struct ensures compatibility with binary serialization
    ///     and supports comparison operations.
    /// </summary>
    /// <remarks>
    ///     This struct provides methods to serialize and deserialize a Vector3 object, ensuring type safety
    ///     and easy integration with network utilities. It includes mechanisms to validate and prohibit NaN
    ///     values in the Vector3 representation during initialization.
    /// </remarks>
    public struct Vector3Serializable : IAetherSerializable, IEquatable<Vector3Serializable>
	{
        /// <summary>
        ///     Represents a Vector3 value used for serialization purposes.
        /// </summary>
        public Vector3 Value;

        /// <summary>
        ///     Represents a serializable wrapper for Unity's Vector3 structure, specifically designed
        ///     to ensure compatibility with the AetherLink serialization system and support binary serialization.
        /// </summary>
        /// <remarks>
        ///     This struct facilitates the secure transmission of Vector3 data by converting to and from
        ///     serialized binary format. It includes validation mechanisms to prevent NaN values in the vector.
        /// </remarks>
        public Vector3Serializable(Vector3 _value)
		{
			if (float.IsNaN(_value.x) || float.IsNaN(_value.y) || float.IsNaN(_value.z))
				throw new ArgumentException("Vector3 contains NaN values");

			Value = _value;
		}


        /// <summary>
        ///     Serializes the current object into binary format using the provided BinaryWriter.
        /// </summary>
        /// <param name="_writer">The BinaryWriter used to write the serialized data.</param>
        public void Serialize(BinaryWriter _writer)
		{
			_writer.Write(Value.x);
			_writer.Write(Value.y);
			_writer.Write(Value.z);
		}

        /// <summary>
        ///     Reads the object's data from the specified BinaryReader and reconstructs it, effectively deserializing the object.
        ///     This method is part of the IAetherSerializable interface implementation and is designed to allow custom objects to
        ///     be reconstructed from a binary format.
        /// </summary>
        /// <param name="_reader">
        ///     The BinaryReader instance to read data from. This reader should contain the serialized data for
        ///     the object.
        /// </param>
        public void Deserialize(BinaryReader _reader)
		{
			Value.x = _reader.ReadSingle();
			Value.y = _reader.ReadSingle();
			Value.z = _reader.ReadSingle();
		}

        /// <summary>
        ///     Returns a string representation of the Vector3Serializable instance, providing the x, y, and z components
        ///     formatted to two decimal places within a "Vector3(x, y, z)" structure.
        /// </summary>
        /// <returns>A string that represents the current Vector3Serializable instance in a readable format.</returns>
        public override string ToString()
		{
			return $"Vector3({Value.x:F2}, {Value.y:F2}, {Value.z:F2})";
		}


		// Optional: Add an implicit conversion for convenience, allowing you to treat this struct like a Vector3.
        /// <summary>
        ///     Defines implicit conversion operators to enable seamless interaction between
        ///     Vector3 and Vector3Serializable types. Allows implicit casting in both directions.
        /// </summary>
        public static implicit operator Vector3(Vector3Serializable _s)
		{
			return _s.Value;
		}

        /// <summary>
        ///     Implicit conversion operator that allows a standard UnityEngine.Vector3
        ///     to be converted into a Vector3Serializable type.
        /// </summary>
        public static implicit operator Vector3Serializable(Vector3 _v)
		{
			return new Vector3Serializable(_v);
		}

        /// <summary>
        ///     Determines whether two Vector3Serializable instances are equal by comparing their underlying Vector3 values.
        /// </summary>
        /// <param name="_left">The first Vector3Serializable instance to compare.</param>
        /// <param name="_right">The second Vector3Serializable instance to compare.</param>
        /// <returns>True if the two instances have identical Vector3 values; otherwise, false.</returns>
        public static bool operator ==(Vector3Serializable _left, Vector3Serializable _right)
		{
			return _left.Value == _right.Value;
		}

        /// <summary>
        ///     Determines whether two instances of Vector3Serializable are considered unequal.
        /// </summary>
        /// <param name="_left">The first Vector3Serializable instance to compare.</param>
        /// <param name="_right">The second Vector3Serializable instance to compare.</param>
        /// <returns>True if the two instances are not equal; otherwise, false.</returns>
        public static bool operator !=(Vector3Serializable _left, Vector3Serializable _right)
		{
			return !(_left == _right);
		}

        /// <summary>
        ///     Determines whether the current instance is equal to another instance of the same type.
        /// </summary>
        /// <param name="_other">The other <see cref="Vector3Serializable" /> instance to compare with the current instance.</param>
        /// <returns>True if the current instance is equal to the other instance; otherwise, false.</returns>
        public bool Equals(Vector3Serializable _other)
		{
			return Value.Equals(_other.Value);
		}

        /// <summary>
        ///     Determines whether the specified object is equal to the current Vector3Serializable instance.
        /// </summary>
        /// <param name="_obj">The object to compare with the current Vector3Serializable instance.</param>
        /// <returns>true if the specified object is equal to the current instance; otherwise, false.</returns>
        public override bool Equals(object _obj)
		{
			return _obj is Vector3Serializable other && Equals(other);
		}

        /// <summary>
        ///     Generates a hash code for the current instance of the object.
        ///     This method is often used for optimizing lookups in hash-based collections or for object comparisons.
        /// </summary>
        /// <returns>
        ///     An integer representing the hash code for the current object.
        /// </returns>
        public override int GetHashCode()
		{
			return Value.GetHashCode();
		}
	}

    /// <summary>
    ///     A serializable wrapper for Unity's Quaternion with comprehensive equality and validation support.
    /// </summary>
    public struct QuaternionSerializable : IAetherSerializable, IEquatable<QuaternionSerializable>
	{
		public Quaternion Value;

		public QuaternionSerializable(Quaternion _value)
		{
			// Validate quaternion (optional - quaternions are generally safe)
			if (float.IsNaN(_value.x) || float.IsNaN(_value.y) || float.IsNaN(_value.z) || float.IsNaN(_value.w))
				throw new ArgumentException("Quaternion contains NaN values");
			Value = _value;
		}

        /// <summary>
        ///     Serializes the quaternion components to binary format.
        /// </summary>
        /// <param name="_writer">Binary writer to write data to</param>
        public void Serialize(BinaryWriter _writer)
		{
			_writer.Write(Value.x);
			_writer.Write(Value.y);
			_writer.Write(Value.z);
			_writer.Write(Value.w);
		}

        /// <summary>
        ///     Deserializes quaternion components from a binary format.
        /// </summary>
        /// <param name="_reader">Binary reader to read data from</param>
        public void Deserialize(BinaryReader _reader)
		{
			Value.x = _reader.ReadSingle();
			Value.y = _reader.ReadSingle();
			Value.z = _reader.ReadSingle();
			Value.w = _reader.ReadSingle();
		}

        /// <summary>
        ///     Returns a string representation of the quaternion with formatted components.
        /// </summary>
        /// <returns>String representation of the quaternion</returns>
        public override string ToString()
		{
			return $"Quaternion({Value.x:F2}, {Value.y:F2}, {Value.z:F2}, {Value.w:F2})";
		}

		// Implicit conversion operators
		public static implicit operator Quaternion(QuaternionSerializable _s)
		{
			return _s.Value;
		}

		public static implicit operator QuaternionSerializable(Quaternion _q)
		{
			return new QuaternionSerializable(_q);
		}

		// Equality operators
        /// <summary>
        ///     Determines whether two QuaternionSerializable instances are equal.
        /// </summary>
        public static bool operator ==(QuaternionSerializable _left, QuaternionSerializable _right)
		{
			return _left.Value == _right.Value;
		}

        /// <summary>
        ///     Determines whether two QuaternionSerializable instances are not equal.
        /// </summary>
        public static bool operator !=(QuaternionSerializable _left, QuaternionSerializable _right)
		{
			return !(_left == _right);
		}

        /// <summary>
        ///     Determines whether the current instance is equal to another QuaternionSerializable.
        /// </summary>
        public bool Equals(QuaternionSerializable _other)
		{
			return Value.Equals(_other.Value);
		}

        /// <summary>
        ///     Determines whether the specified object is equal to the current QuaternionSerializable.
        /// </summary>
        public override bool Equals(object _obj)
		{
			return _obj is QuaternionSerializable other && Equals(other);
		}

        /// <summary>
        ///     Returns the hash code for this QuaternionSerializable.
        /// </summary>
        public override int GetHashCode()
		{
			return Value.GetHashCode();
		}
	}

    /// <summary>
    ///     A serializable wrapper for Unity's Vector2 with comprehensive equality and validation support.
    /// </summary>
    public struct Vector2Serializable : IAetherSerializable, IEquatable<Vector2Serializable>
	{
		public Vector2 Value;

		public Vector2Serializable(Vector2 _value)
		{
			if (float.IsNaN(_value.x) || float.IsNaN(_value.y))
				throw new ArgumentException("Vector2 contains NaN values");
			Value = _value;
		}

        /// <summary>
        ///     Serializes the Vector2 components to binary format.
        /// </summary>
        /// <param name="_writer">Binary writer to write data to</param>
        public void Serialize(BinaryWriter _writer)
		{
			_writer.Write(Value.x);
			_writer.Write(Value.y);
		}

        /// <summary>
        ///     Deserializes Vector2 components from a binary format.
        /// </summary>
        /// <param name="_reader">Binary reader to read data from</param>
        public void Deserialize(BinaryReader _reader)
		{
			Value.x = _reader.ReadSingle();
			Value.y = _reader.ReadSingle();
		}

        /// <summary>
        ///     Returns a string representation of the Vector2 with formatted components.
        /// </summary>
        /// <returns>String representation of the Vector2</returns>
        public override string ToString()
		{
			return $"Vector2({Value.x:F2}, {Value.y:F2})";
		}

		// Implicit conversion operators
		public static implicit operator Vector2(Vector2Serializable _s)
		{
			return _s.Value;
		}

		public static implicit operator Vector2Serializable(Vector2 _v)
		{
			return new Vector2Serializable(_v);
		}

		// Equality operators
        /// <summary>
        ///     Determines whether two Vector2Serializable instances are equal.
        /// </summary>
        public static bool operator ==(Vector2Serializable _left, Vector2Serializable _right)
		{
			return _left.Value == _right.Value;
		}

        /// <summary>
        ///     Determines whether two Vector2Serializable instances are not equal.
        /// </summary>
        public static bool operator !=(Vector2Serializable _left, Vector2Serializable _right)
		{
			return !(_left == _right);
		}

        /// <summary>
        ///     Determines whether the current instance is equal to another Vector2Serializable.
        /// </summary>
        public bool Equals(Vector2Serializable _other)
		{
			return Value.Equals(_other.Value);
		}

        /// <summary>
        ///     Determines whether the specified object is equal to the current Vector2Serializable.
        /// </summary>
        public override bool Equals(object _obj)
		{
			return _obj is Vector2Serializable other && Equals(other);
		}

        /// <summary>
        ///     Returns the hash code for this Vector2Serializable.
        /// </summary>
        public override int GetHashCode()
		{
			return Value.GetHashCode();
		}
	}

    /// <summary>
    ///     A serializable wrapper for Unity's Color with comprehensive equality and validation support.
    /// </summary>
    public struct ColorSerializable : IAetherSerializable, IEquatable<ColorSerializable>
	{
		public Color Value;

		public ColorSerializable(Color _value)
		{
			if (float.IsNaN(_value.r) || float.IsNaN(_value.g) || float.IsNaN(_value.b) || float.IsNaN(_value.a))
				throw new ArgumentException("Color contains NaN values");
			Value = _value;
		}

        /// <summary>
        ///     Serializes the Color components to binary format.
        /// </summary>
        /// <param name="_writer">Binary writer to write data to</param>
        public void Serialize(BinaryWriter _writer)
		{
			_writer.Write(Value.r);
			_writer.Write(Value.g);
			_writer.Write(Value.b);
			_writer.Write(Value.a);
		}

        /// <summary>
        ///     Deserializes Color components from binary format.
        /// </summary>
        /// <param name="_reader">Binary reader to read data from</param>
        public void Deserialize(BinaryReader _reader)
		{
			Value.r = _reader.ReadSingle();
			Value.g = _reader.ReadSingle();
			Value.b = _reader.ReadSingle();
			Value.a = _reader.ReadSingle();
		}

        /// <summary>
        ///     Returns a string representation of the Color with formatted components.
        /// </summary>
        /// <returns>String representation of the Color</returns>
        public override string ToString()
		{
			return $"Color({Value.r:F2}, {Value.g:F2}, {Value.b:F2}, {Value.a:F2})";
		}

		// Implicit conversion operators
		public static implicit operator Color(ColorSerializable _s)
		{
			return _s.Value;
		}

		public static implicit operator ColorSerializable(Color _c)
		{
			return new ColorSerializable(_c);
		}

		// Equality operators
        /// <summary>
        ///     Determines whether two ColorSerializable instances are equal.
        /// </summary>
        public static bool operator ==(ColorSerializable _left, ColorSerializable _right)
		{
			return _left.Value == _right.Value;
		}

        /// <summary>
        ///     Determines whether two ColorSerializable instances are not equal.
        /// </summary>
        public static bool operator !=(ColorSerializable _left, ColorSerializable _right)
		{
			return !(_left == _right);
		}

        /// <summary>
        ///     Determines whether the current instance is equal to another ColorSerializable.
        /// </summary>
        public bool Equals(ColorSerializable _other)
		{
			return Value.Equals(_other.Value);
		}

        /// <summary>
        ///     Determines whether the specified object is equal to the current ColorSerializable.
        /// </summary>
        public override bool Equals(object _obj)
		{
			return _obj is ColorSerializable other && Equals(other);
		}

        /// <summary>
        ///     Returns the hash code for this ColorSerializable.
        /// </summary>
        public override int GetHashCode()
		{
			return Value.GetHashCode();
		}
	}
}
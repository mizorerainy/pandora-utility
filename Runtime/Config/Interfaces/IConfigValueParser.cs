// =================================================================================
// File: IConfigValueParser.cs
// Author: MizoreRainy
// Description: The interface for creating custom value parsers for complex types.
//              This system is dependency-free.
// =================================================================================

using System;

// ReSharper disable once CheckNamespace
namespace MizoreRainy.Pandora.ConfigUtility
{
	/// <summary>
	/// Provides methods for parsing configuration values, enabling conversion between
	/// complex objects and their string representations for storage and retrieval.
	/// </summary>
	public interface IConfigValueParser
	{
		/// <summary>
		/// Checks if this parser can handle the specified type.
		/// </summary>
		/// <param name="_type">The type to check against the parser capabilities.</param>
		/// <returns>True if the parser can handle the specified type; otherwise, false.</returns>
		bool CanParse(Type _type);

		/// <summary>
		/// Converts the specified value object into its string representation.
		/// </summary>
		/// <param name="_value">The value object to convert into a string representation.</param>
		/// <returns>A string representation of the specified value object.</returns>
		string ToString(object _value);

		/// <summary>
		/// Tries to parse a raw string value into an object of the specified type.
		/// </summary>
		/// <param name="_rawValue">The raw string value to be parsed.</param>
		/// <param name="_type">The type of the object to parse the string into.</param>
		/// <param name="_result">The parsed object if the operation is successful; otherwise, null.</param>
		/// <returns>True if the parsing is successful; otherwise, false.</returns>
		bool TryParse(string _rawValue, Type _type, out object _result);
	}
}
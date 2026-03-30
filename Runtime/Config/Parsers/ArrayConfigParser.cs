// =================================================================================
// File: ArrayConfigParser.cs
// Author: MizoreRainy
// Description: Built-in parser that enables native array support `T[]` for configuration fields.
// =================================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MizoreRainy.Pandora.ConfigUtility.Parsers
{
	/// <summary>
	///     Represents a native built-in parser that provides support for T[] arrays within Pandora configuration.
	///     Capable of parsing both comma-separated inline strings `[1, 2, 3]` and native YAML sequence blocks.
	/// </summary>
	public class ArrayConfigParser : IConfigValueParser
	{
		#region Public API
		public bool CanParse(Type _type)
		{
			return _type.IsArray && _type.GetArrayRank() == 1;
		}

		public bool TryParse(string _rawValue, Type _type, out object _result)
		{
			Type elementType = _type.GetElementType()!;

			if (string.IsNullOrWhiteSpace(_rawValue))
			{
				_result = Array.CreateInstance(elementType, 0);
				return true;
			}

			string content = _rawValue.Trim();
			if (content.StartsWith("[") && content.EndsWith("]"))
			{
				content = content.Substring(1, content.Length - 2).Trim();
			}

			if (string.IsNullOrEmpty(content))
			{
				_result = Array.CreateInstance(elementType, 0);
				return true;
			}

			var elements = SplitRespectingQuotes(content, ',');
			var array = Array.CreateInstance(elementType, elements.Count);

			for (int i = 0; i < elements.Count; i++)
			{
				try
				{
					var el = elements[i].Trim();
					if (el.StartsWith("\"") && el.EndsWith("\""))
					{
						el = el.Substring(1, el.Length - 2).Replace("\\\"", "\"");
					}

					var p = ConfigLoader.GetParserForType(elementType);
					if (p != null)
					{
						if (p.TryParse(el, elementType, out object childResult))
							array.SetValue(childResult, i);
						else
							array.SetValue(elementType.IsValueType ? Activator.CreateInstance(elementType) : null, i);
					}
					else if (elementType.IsEnum)
					{
						array.SetValue(Enum.Parse(elementType, el), i);
					}
					else if (elementType == typeof(Vector3))
					{
						string[] vSplit = el.Replace("(", "").Replace(")", "").Split(',');
						if (vSplit.Length >= 3)
							array.SetValue(new Vector3(float.Parse(vSplit[0]), float.Parse(vSplit[1]), float.Parse(vSplit[2])), i);
					}
					else if (elementType == typeof(Color))
					{
						if (ColorUtility.TryParseHtmlString(el, out Color c))
							array.SetValue(c, i);
					}
					else
					{
						array.SetValue(Convert.ChangeType(el, elementType, System.Globalization.CultureInfo.InvariantCulture), i);
					}
				}
				catch (Exception)
				{
					array.SetValue(elementType.IsValueType ? Activator.CreateInstance(elementType) : null, i);
				}
			}

			_result = array;
			return true;
		}

		public string ToString(object _value)
		{
			if (_value == null) return "[]";
			
			Type arrayType = _value.GetType();
			Type elementType = arrayType.GetElementType()!;
			var array = (Array)_value;
			var elements = new List<string>();

			for (int i = 0; i < array.Length; i++)
			{
				var el = array.GetValue(i);
				string strVal = el?.ToString() ?? "";

				var p = ConfigLoader.GetParserForType(elementType);
				if (p != null)
				{
					strVal = p.ToString(el);
				}

				if (strVal.Contains(",") || strVal.Contains(" ") || strVal.StartsWith("[") || strVal.EndsWith("]"))
				{
					strVal = $"\"{strVal.Replace("\"", "\\\"")}\"";
				}
				elements.Add(strVal);
			}

			return "[" + string.Join(", ", elements) + "]";
		}

		#endregion

		#region Internal & Interface Implementations

		private List<string> SplitRespectingQuotes(string text, char separator)
		{
			var result = new List<string>();
			bool inQuotes = false;
			int start = 0;

			for (int i = 0; i < text.Length; i++)
			{
				if (text[i] == '\"')
				{
					if (i > 0 && text[i - 1] == '\\') continue; // Escaped quote
					inQuotes = !inQuotes;
				}
				else if (text[i] == separator && !inQuotes)
				{
					result.Add(text.Substring(start, i - start));
					start = i + 1;
				}
			}

			result.Add(text.Substring(start));
			return result;
		}

		#endregion
	}
}

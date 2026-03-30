using MizoreRainy.Pandora;
// =================================================================================
// File: ConfigLoader.cs
// Author: MizoreRainy
// Description: The core engine for loading, parsing, and saving configuration.
//    This system is dependency-free and uses built-in .NET Task async.
// =================================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
#if HAVE_VYAML
using VYaml.Serialization;
#endif

// This attribute grants the specified editor assembly access to this assembly's internal members.
[assembly: InternalsVisibleTo("MizoreRainy.Pandora.Editor.Config")]

// ReSharper disable once CheckNamespace
namespace MizoreRainy.Pandora.ConfigUtility
{
	/// <summary>
	///     Provides functionality for initializing, managing, and interacting with application configuration settings.
	///     This class includes methods for loading, saving, runtime updates, and monitoring configuration files.
	/// </summary>
	public static partial class ConfigLoader
	{
		#region Internal & Interface Implementations
#if HAVE_VYAML
		// Loads configuration settings synchronously from a YAML file.
		// If an error occurs during the process, default values are applied.
		private static void LoadFromYamlSync(string _path)
		{
			Dictionary<string, object> yamlData;
			try
			{
				byte[] yamlBytes;
				lock (FileLock)
				{
					yamlBytes = File.ReadAllBytes(_path);
				}

				var rawData = YamlSerializer.Deserialize<object>(yamlBytes);
				yamlData = FlattenYaml(rawData);
			}
			catch (Exception e)
			{
				PandoraLogger.LogConfigError($"Failed to read YAML config. Error: {e.Message}");
				yamlData = new Dictionary<string, object>();
			}

			foreach (var setting in Registry.Settings)
			{
				// The key in the flattened dictionary will be the full path (e.g., "Group.SubGroup.Key")
				var fullKey = $"{setting.GroupName}.{setting.Key}";
				if (yamlData.TryGetValue(fullKey, out var value))
					setting.SetValueFromString(value?.ToString() ?? "");
				else
					setting.SetToDefault();
			}
		}

		// Asynchronously loads configuration settings from a YAML file.
		private static Task LoadFromYamlAsync(string _path)
		{
			LoadFromYamlSync(_path);
			return Task.CompletedTask;
		}

		// Saves the current configuration settings to a YAML file synchronously.
		private static void SaveToYamlSync(string _path)
		{
			try
			{
				var yamlString = GenerateYamlString();
				var yamlBytes = Encoding.UTF8.GetBytes(yamlString);
				lock (FileLock)
				{
					File.WriteAllBytes(_path, yamlBytes);
				}
			}
			catch (Exception e)
			{
				PandoraLogger.LogConfigError($"Failed to save YAML config! Error: {e.Message}");
			}
		}

		// Saves the current application configuration settings to a YAML file asynchronously.
		private static async Task SaveToYamlAsync(string _path)
		{
			try
			{
				var yamlString = GenerateYamlString();
				var yamlBytes = Encoding.UTF8.GetBytes(yamlString);
				await Task.Run(() =>
				{
					lock (FileLock)
					{
						File.WriteAllBytes(_path, yamlBytes);
					}
				});
			}
			catch (Exception e)
			{
				PandoraLogger.LogConfigError($"Failed to save YAML config! Error: {e.Message}");
			}
		}

		// Generates a YAML formatted string containing all configuration settings.
		// Organizes settings hierarchically based on their group names and injects descriptions as comments.
		private static string GenerateYamlString()
		{
			var sb = new StringBuilder();
			sb.AppendLine($"# Last saved: {DateTime.Now}");

			var topLevelGroups = Registry.Settings
				.Select(_s => _s.GroupName.Split('.')[0])
				.Distinct()
				.OrderBy(_g => Registry.GroupOrders.TryGetValue(_g, out var _order) ? _order : 0)
				.ThenBy(_g => _g);

			foreach (var topLevelGroup in topLevelGroups)
			{
				sb.AppendLine("\n#==================================================");
				sb.AppendLine($"# :: {topLevelGroup} Settings");
				sb.AppendLine("#==================================================");
				sb.AppendLine($"{topLevelGroup}:");
				BuildYamlNode(sb, topLevelGroup, 1);
			}

			return sb.ToString();
		}

		// Recursively builds a hierarchical representation of configuration settings.
		private static void BuildYamlNode(StringBuilder _sb, string _currentPath, int _indentLevel)
		{
			string indent = new string(' ', _indentLevel * 2);

			var directSettings = Registry.Settings.Where(_s => _s.GroupName == _currentPath);
			foreach (var setting in directSettings)
			{
				if (!string.IsNullOrEmpty(setting.Description))
				{
					var lines = setting.Description.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
					foreach (var line in lines)
						_sb.AppendLine($"{indent}# {line}");
				}

				string val = setting.GetValueAsString();
				if (val == "[]")
				{
					_sb.AppendLine($"{indent}{setting.Key}: []");
				}
				else if (val.StartsWith("[") && val.EndsWith("]"))
				{
					_sb.AppendLine($"{indent}{setting.Key}:");
					string innerVal = val.Substring(1, val.Length - 2);
					var elements = innerVal.Split(new[] { ", " }, StringSplitOptions.None);
					foreach (var el in elements)
					{
						_sb.AppendLine($"{indent}  - {FormatYamlValue(el)}");
					}
				}
				else
				{
					val = FormatYamlValue(val);
					_sb.AppendLine($"{indent}{setting.Key}: {val}");
				}
			}

			var childrenGroups = Registry.Settings
				.Where(_s => _s.GroupName.StartsWith(_currentPath + "."))
				.Select(_s => _s.GroupName.Substring(_currentPath.Length + 1).Split('.')[0])
				.Distinct()
				.OrderBy(_g => Registry.GroupOrders.TryGetValue($"{_currentPath}.{_g}", out var _order) ? _order : 0)
				.ThenBy(_g => _g);

			foreach (var childGroup in childrenGroups)
			{
				_sb.AppendLine($"{indent}{childGroup}:");
				BuildYamlNode(_sb, $"{_currentPath}.{childGroup}", _indentLevel + 1);
			}
		}

		// Formats a value to ensure valid YAML scalar representation. Quotes the value if it contains spaces or special characters.
		private static string FormatYamlValue(string _val)
		{
			if (string.IsNullOrEmpty(_val)) return "\"\"";

			bool needsQuotes = false;
			if (_val.Contains(" ") || _val.Contains(":") || _val.Contains("#") ||
				_val.Contains("\n") || _val.Contains("\r") ||
				_val.StartsWith("[") || _val.StartsWith("{") ||
				_val.StartsWith("\"") || _val.StartsWith("'"))
			{
				needsQuotes = true;
			}

			if (needsQuotes)
			{
				_val = _val.Replace("\\", "\\\\").Replace("\"", "\\\"");
				return $"\"{_val}\"";
			}

			return _val;
		}


		// Flattens a nested YAML data structure into a flat dictionary using dot-separated keys.
		private static Dictionary<string, object> FlattenYaml(object _yamlData, string _prefix = "")
		{
			var result = new Dictionary<string, object>();
			if (_yamlData is Dictionary<object, object> dictionary)
			{
				foreach (var kvp in dictionary)
				{
					var newPrefix = string.IsNullOrEmpty(_prefix) ? kvp.Key.ToString() : $"{_prefix}.{kvp.Key}";
					var flattenedChildren = FlattenYaml(kvp.Value, newPrefix);
					foreach (var flattenedKvp in flattenedChildren) result[flattenedKvp.Key] = flattenedKvp.Value;
				}
			}
			else if (_yamlData is IList<object> list)
			{
				var elements = new List<string>();
				foreach (var element in list)
				{
					if (element == null) elements.Add("");
					else
					{
						string strVal = element.ToString();
						if (strVal.Contains(",") || strVal.Contains(" ") || strVal.StartsWith("[") || strVal.EndsWith("]"))
							elements.Add($"\"{strVal.Replace("\"", "\\\"")}\"");
						else
							elements.Add(strVal);
					}
				}
				if (!string.IsNullOrEmpty(_prefix)) result[_prefix] = "[" + string.Join(", ", elements) + "]";
			}
			else
			{
				if (!string.IsNullOrEmpty(_prefix)) result[_prefix] = _yamlData;
			}

			return result;
		}
#endif

		#endregion
	}
}

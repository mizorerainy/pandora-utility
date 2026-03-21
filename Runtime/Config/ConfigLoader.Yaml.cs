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
		#region Public API

		#region YAML Processing
#if HAVE_VYAML
		/// <summary>
		///     Loads configuration settings synchronously from a YAML file located at the specified path.
		///     This method uses the YAML serializer to read and parse the configuration,
		///     then applies the parsed values to the registered settings.
		///     If an error occurs during the process,
		///     default values are applied to the settings.
		/// </summary>
		/// <param name="_path">
		///     The file path of the YAML configuration file to be loaded.
		/// </param>
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

		/// <summary>
		///     Asynchronously loads configuration settings from a YAML file.
		/// </summary>
		/// <param name="_path">The path to the YAML configuration file to be loaded.</param>
		/// <returns>A task representing the asynchronous operation.</returns>
		private static Task LoadFromYamlAsync(string _path)
		{
			LoadFromYamlSync(_path);
			return Task.CompletedTask;
		}

		/// <summary>
		///     Saves the current configuration settings to a YAML file synchronously.
		///     This method uses the specified file path to write configuration data
		///     in YAML format with comments preserved.
		///     Thread-safety is ensured to prevent concurrent file access issues.
		/// </summary>
		/// <param name="_path">
		///     The full file path where the YAML configuration file will be saved.
		///     Must be a valid writable file path.
		/// </param>
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

		/// <summary>
		///     Saves the current application configuration settings to a YAML file asynchronously.
		///     Preserves comments from the configuration attribute properties.
		/// </summary>
		/// <param name="_path">The file path where the YAML configuration will be saved.</param>
		/// <returns>A task representing the asynchronous save operation.</returns>
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

		/// <summary>
		///     Generates a YAML formatted string containing all configuration settings.
		///     Organizes settings hierarchically based on their group names and injects
		///     setting descriptions as YAML comments.
		/// </summary>
		/// <returns>A YAML formatted string.</returns>
		private static string GenerateYamlString()
		{
			var sb = new StringBuilder();
			sb.AppendLine($"# Last saved: {DateTime.Now}");

			var topLevelGroups = Registry.Settings.Select(_s => _s.GroupName.Split('.')[0]).Distinct().OrderBy(_g => _g);

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

		/// <summary>
		///     Recursively builds a hierarchical representation of configuration settings
		///     by appending structured text to the StringBuilder.
		/// </summary>
		/// <param name="_sb">The StringBuilder to write the YAML content to.</param>
		/// <param name="_currentPath">The current group path being processed.</param>
		/// <param name="_indentLevel">The current indentation level of the node.</param>
		private static void BuildYamlNode(StringBuilder _sb, string _currentPath, int _indentLevel)
		{
			string indent = new string(' ', _indentLevel * 2);

			var directSettings = Registry.Settings.Where(_s => _s.GroupName == _currentPath).OrderBy(_s => _s.Key);
			foreach (var setting in directSettings)
			{
				if (!string.IsNullOrEmpty(setting.Description))
				{
					var lines = setting.Description.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
					foreach (var line in lines)
						_sb.AppendLine($"{indent}# {line}");
				}

				string val = setting.GetValueAsString();
				val = FormatYamlValue(val);
				_sb.AppendLine($"{indent}{setting.Key}: {val}");
			}

			var childrenGroups = Registry.Settings
				.Where(_s => _s.GroupName.StartsWith(_currentPath + "."))
				.Select(_s => _s.GroupName.Substring(_currentPath.Length + 1).Split('.')[0])
				.Distinct()
				.OrderBy(_g => _g);

			foreach (var childGroup in childrenGroups)
			{
				_sb.AppendLine($"{indent}{childGroup}:");
				BuildYamlNode(_sb, $"{_currentPath}.{childGroup}", _indentLevel + 1);
			}
		}

		/// <summary>
		///     Formats a value to ensure valid YAML scalar representation.
		///     Quotes the value if it contains spaces or special characters.
		/// </summary>
		/// <param name="_val">The raw string value.</param>
		/// <returns>The YAML formatted scalar value.</returns>
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


		/// <summary>
		///     Flattens a nested YAML data structure into a flat dictionary using dot-separated keys.
		///     This method recursively traverses the hierarchy of the given object and converts
		///     any nested dictionaries into a single-layered dictionary with keys representing
		///     the hierarchy structure.
		/// </summary>
		/// <param name="_yamlData">
		///     The nested YAML data to be flattened.
		///     Typically, this is a dictionary
		///     or an object deserialized from a YAML structure.
		/// </param>
		/// <param name="_prefix">
		///     An optional prefix that is prepended to the keys in the resulting dictionary,
		///     representing the hierarchy path of the current level.
		/// </param>
		/// <returns>
		///     A dictionary with flattened, dot-separated keys mapping to their respective values
		///     from the provided YAML data.
		/// </returns>
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
			else
			{
				if (!string.IsNullOrEmpty(_prefix)) result[_prefix] = _yamlData;
			}

			return result;
		}
#endif

		#endregion

		#endregion
	}
}

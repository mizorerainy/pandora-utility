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

		/// <summary>
		///     Synchronously loads configuration settings from a file located on the disk.
		///     If the configuration file is not found, a new file is automatically created containing the default settings.
		///     Invalid or missing entries in the configuration file are replaced with default values
		///     to maintain data integrity.
		///     Ensures consistency by saving the updated configuration back to the file after loading.
		/// </summary>
		private static void LoadFromFileSync()
		{
			var path = GetConfigPath();
			if (!File.Exists(path))
			{
				PandoraLogger.LogConfig("Config file not found. Creating a new one with default values.");
				SaveSync();
				return;
			}

#if USE_YAML_CONFIG && HAVE_VYAML
			LoadFromYamlSync(path);
#else
			LoadFromIniSync(path);
#endif
			SaveSync(); // Self-heal
		}

		/// <summary>
		///     Asynchronously loads application settings from the configuration file.
		///     If the file does not exist, a new configuration file is created with default values.
		///     Missing or invalid entries in the configuration are automatically replaced with defaults,
		///     ensuring the integrity and consistency of the loaded settings.
		/// </summary>
		/// <returns>
		///     A Task representing the operation of reading, validating, and applying
		///     settings from the configuration file.
		/// </returns>
		public static async Task LoadFromFileAsync()
		{
			var path = GetConfigPath();
			if (!File.Exists(path))
			{
				PandoraLogger.LogConfig("Config file not found. Creating a new one with default values.");
				await SaveAsync();
				return;
			}

#if USE_YAML_CONFIG && HAVE_VYAML
			await LoadFromYamlAsync(path);
#else
			await LoadFromIniAsync(path);
#endif
			await SaveAsync(); // Self-heal
		}

		/// <summary>
		///     Synchronously saves all configuration settings to the designated file.
		///     Temporarily halts the file watcher to prevent triggering loops during the save process.
		///     Use the active configuration format (e.g., INI or YAML) for saving.
		///     Ensures the file watcher is properly resumed after the operation concludes.
		/// </summary>
		private static void SaveSync()
		{
			StopWatching(); // Pause watcher to prevent infinite loop
			try
			{
#if USE_YAML_CONFIG && HAVE_VYAML
				SaveToYamlSync(GetConfigPath());
#else
				SaveToIniSync(GetConfigPath());
#endif
			}
			finally
			{
				StartWatching(); // Resume watcher
			}
		}

		/// <summary>
		///     Saves all configuration settings to the associated file asynchronously.
		///     Ensures the configuration's integrity during the save operation by temporarily halting the file watcher,
		///     preventing unintended interactions, and safeguarding against potential infinite loops.
		/// </summary>
		/// <returns>
		///     A task that represents the asynchronous save operation.
		///     The task completes when all changes
		///     are successfully written to the configuration file.
		/// </returns>
		public static async Task SaveAsync()
		{
			StopWatching(); // Pause watcher to prevent infinite loop
			try
			{
#if USE_YAML_CONFIG && HAVE_VYAML
				await SaveToYamlAsync(GetConfigPath());
#else
				await SaveToIniAsync(GetConfigPath());
#endif
			}
			finally
			{
				StartWatching(); // Resume watcher
			}
		}

		/// <summary>
		///     Resets all configuration settings to their default values as defined in the code.
		///     The default values are saved back to the configuration file, overwriting any
		///     previously existing content.
		///     This method ensures that the loader is properly
		///     initialized before performing the reset operation.
		///     The operation is performed asynchronously.
		/// </summary>
		/// <returns>
		///     A task representing the asynchronous operation of resetting
		///     and saving the default configuration settings.
		/// </returns>
		public static async Task ResetToDefaultsAsync()
		{
			// Ensure the loader is initialized so we know about all the settings.
			await InitializeAsync();

			PandoraLogger.LogConfig("Resetting all settings to their default values...");
			foreach (var setting in Settings) setting.SetToDefault();

			// Now, save these default values back to the file.
			await SaveAsync();
			PandoraLogger.LogConfig("All settings have been reset to defaults and saved.");
		}

		#endregion


		#region Domain Logic

		/// <summary>
		///     Synchronously loads configuration settings from a specified INI file.
		///     Processes the file to extract key-value pairs and applies the values to the
		///     corresponding registered settings.
		///     If a configuration entry is missing in the file,
		///     the corresponding setting is reverted to its default value.
		/// </summary>
		/// <param name="_path">
		///     The file path to the INI configuration file being loaded.
		/// </param>
		private static void LoadFromIniSync(string _path)
		{
			var fileValues = new Dictionary<string, string>();
			try
			{
				string[] lines;
				lock (FileLock)
				{
					lines = File.ReadAllLines(_path);
				}

				foreach (var line in lines)
				{
					if (string.IsNullOrWhiteSpace(line) || line.Trim().StartsWith("#")) continue;
					var equalsIndex = line.IndexOf('=');
					if (equalsIndex > 0) fileValues[line[..equalsIndex].Trim()] = line[(equalsIndex + 1)..];
				}
			}
			catch (Exception e)
			{
				PandoraLogger.LogConfigError($"Failed to read INI config. Error: {e.Message}");
			}

			foreach (var setting in Settings)
				if (fileValues.TryGetValue(setting.Key, out var rawValue)) setting.SetValueFromString(rawValue);
				else setting.SetToDefault();
		}

		// ReSharper disable once UnusedMember.Local
		/// <summary>
		///     Asynchronously loads configuration settings from an INI file.
		///     Uses the provided file path to locate and read the configuration data,
		///     ensuring settings are updated with values from the file
		///     while maintaining defaults for missing entries.
		/// </summary>
		/// <param name="_path">The file path of the INI file containing configuration settings.</param>
		/// <returns>
		///     A Task representing the asynchronous operation of loading the configuration from the INI file.
		/// </returns>
		private static Task LoadFromIniAsync(string _path)
		{
			LoadFromIniSync(_path);
			return Task.CompletedTask;
		}

		// ReSharper disable once UnusedMember.Local
		/// <summary>
		///     Saves all configuration settings to an INI file synchronously at the specified file path.
		///     Groups settings by their group names and includes descriptions as comments in the output file.
		/// </summary>
		/// <param name="_path">The file path where the configuration settings will be saved.</param>
		private static void SaveToIniSync(string _path)
		{
			var sb = new StringBuilder();
			sb.AppendLine($"# Last saved: {DateTime.Now}");
			var groupedSettings = Settings.GroupBy(_s => _s.GroupName).OrderBy(_g => _g.Key);
			foreach (var group in groupedSettings)
			{
				sb.AppendLine("\n#==================================================");
				sb.AppendLine($"# :: {group.Key} Settings");
				sb.AppendLine("#==================================================");
				foreach (var setting in group.OrderBy(_s => _s.Key))
				{
					if (!string.IsNullOrEmpty(setting.Description)) sb.AppendLine($"# {setting.Description}");
					sb.AppendLine($"{setting.Key}={setting.GetValueAsString()}");
				}
			}

			try
			{
				var encodedText = Encoding.UTF8.GetBytes(sb.ToString());
				lock (FileLock)
				{
					File.WriteAllBytes(_path, encodedText);
				}
			}
			catch (Exception e)
			{
				PandoraLogger.LogConfigError($"Failed to save INI config! Error: {e.Message}");
			}
		}

		// ReSharper disable once UnusedMember.Local
		/// <summary>
		///     Saves the current configuration settings to an .ini file at the specified file path asynchronously.
		///     The settings are grouped and formatted with descriptions and metadata for better readability.
		/// </summary>
		/// <param name="_path">The file path where the .ini file will be saved.</param>
		/// <returns>A task that represents the asynchronous save operation.</returns>
		private static async Task SaveToIniAsync(string _path)
		{
			var sb = new StringBuilder();
			sb.AppendLine($"# Last saved: {DateTime.Now}");
			var groupedSettings = Settings.GroupBy(_s => _s.GroupName).OrderBy(_g => _g.Key);
			foreach (var group in groupedSettings)
			{
				sb.AppendLine("\n#==================================================");
				sb.AppendLine($"# :: {group.Key} Settings");
				sb.AppendLine("#==================================================");
				foreach (var setting in group.OrderBy(_s => _s.Key))
				{
					if (!string.IsNullOrEmpty(setting.Description)) sb.AppendLine($"# {setting.Description}");
					sb.AppendLine($"{setting.Key}={setting.GetValueAsString()}");
				}
			}

			try
			{
				var encodedText = Encoding.UTF8.GetBytes(sb.ToString());
				await Task.Run(() =>
				{
					lock (FileLock)
					{
						File.WriteAllBytes(_path, encodedText);
					}
				});
			}
			catch (Exception e)
			{
				PandoraLogger.LogConfigError($"Failed to save INI config! Error: {e.Message}");
			}
		}



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

			foreach (var setting in Settings)
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

			var topLevelGroups = Settings.Select(_s => _s.GroupName.Split('.')[0]).Distinct().OrderBy(_g => _g);

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

			var directSettings = Settings.Where(_s => _s.GroupName == _currentPath).OrderBy(_s => _s.Key);
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

			var childrenGroups = Settings
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


		#region Constructors/Initialization

		/// <summary>
		///     Starts monitoring the configuration file for any changes and allows live-reloading functionality.
		///     Uses a file system watcher to detect changes such as modifications to the configuration file,
		///     automatically triggering a reload of the settings when changes occur.
		///     The watcher will stop monitoring when the application is about to terminate,
		///     ensuring proper cleanup of resources.
		///     If the watcher fails to initialize, live-reloading will not be enabled, and an error will be logged.
		///     This feature is available only on supported platforms, such as the Unity Editor or standalone builds.
		/// </summary>
		public static void StartWatching()
		{
#if UNITY_EDITOR || UNITY_STANDALONE
			if (_Watcher != null) return;

			try
			{
				var path = GetConfigPath();
				_Watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(path));
				_Watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
				_Watcher.Changed += OnConfigFileChanged;
				_Watcher.EnableRaisingEvents = true;

				Application.quitting += StopWatching;

				PandoraLogger.LogConfig("Started watching config file for changes.");
			}
			catch (Exception e)
			{
				PandoraLogger.LogConfigError($"Failed to start file watcher. Live-reloading will be disabled. Error: {e.Message}");
				_Watcher?.Dispose();
				_Watcher = null;
			}
#endif
		}

		/// <summary>
		///     Stops monitoring the configuration file for changes.
		///     This method disables the file system watcher, unsubscribes from any associated events,
		///     and releases resources allocated for file monitoring.
		///     Designed to prevent further
		///     automatic updates to the configuration state triggered by file modifications.
		/// </summary>
		public static void StopWatching()
		{
#if UNITY_EDITOR || UNITY_STANDALONE
			if (_Watcher == null) return;

			_Watcher.EnableRaisingEvents = false;
			_Watcher.Changed -= OnConfigFileChanged;
			_Watcher.Dispose();
			_Watcher = null;
			Application.quitting -= StopWatching;
			PandoraLogger.LogConfig("Stopped watching config file.");
#endif
		}

		/// <summary>
		///     Handles the event triggered when the configuration file is changed.
		///     Ensures the updated configuration file is processed, reloading settings
		///     to maintain consistency with the new file state.
		/// </summary>
		/// <param name="_sender">The source of the event, typically an instance of FileSystemWatcher.</param>
		/// <param name="_e">The event arguments containing details about the file change.</param>
		[SuppressMessage("ReSharper", "AsyncVoidLambda")]
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
		private static async void OnConfigFileChanged(object _sender, FileSystemEventArgs _e)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
		{
			if (_IsReloading) return;
			_IsReloading = true;

			_MainThreadContext?.Post(async _ =>
			{
				try
				{
					PandoraLogger.LogConfig("File change detected. Reloading settings...");
					await LoadFromFileAsync();
				}
				finally
				{
					_IsReloading = false;
				}
			}, null);
		}

		#endregion

	}
}

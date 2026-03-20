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
	[DefaultExecutionOrder(-9999)]
	public static class ConfigLoader
	{
		#region Fields

		/// <summary>
		///     Contains a collection of configurable settings used within the application lifecycle.
		///     It serves as a central repository for managing, loading, and resetting configuration entries.
		/// </summary>
		private static readonly List<IConfigEntry> Settings = new();

		/// <summary>
		///     Represents a collection of registered configuration value parsers used within the configuration
		///     system to handle the conversion of various data types to and from their string representations.
		///     These parsers facilitate seamless interaction with custom configuration formats and types.
		/// </summary>
		private static readonly List<IConfigValueParser> Parsers = new();

		/// <summary>
		///     Indicates whether the ConfigLoader has completed its initialization process.
		///     This field is used internally to ensure that configuration operations are only performed
		///     after all required initialization tasks are successfully finished.
		/// </summary>
		private static bool _IsInitialized;

		/// <summary>
		///     Tracks whether the configuration system is currently undergoing the initialization process.
		///     Used internally to ensure initialization operations are not executed concurrently
		///     or redundantly within the application workflow.
		/// </summary>
		private static bool _IsInitializing;

		/// <summary>
		///     Tracks the task representing the asynchronous initialization process
		///     of the configuration system within the application.
		///     This variable ensures that the same initialization logic can be
		///     awaited from multiple callers without initiating concurrent initializations.
		/// </summary>
		private static Task _InitializationTask;

		/// <summary>
		///     Serves as a synchronization mechanism used to ensure thread-safe
		///     execution of critical sections during the initialization process
		///     within the configuration loader.
		/// </summary>
		private static readonly object InitializationLock = new();

		/// <summary>
		///     Holds the file path to the application's configuration file.
		///     This variable is dynamically assigned based on the execution environment,
		///     ensuring the configuration file is correctly located in contexts such as
		///     the Unity Editor, standalone builds, or mobile platforms.
		/// </summary>
		private static string _ConfigFilePath;

		/// <summary>
		///     A private static variable representing a file system watcher
		///     that observes changes to the configuration file during runtime.
		///     This enables functionality such as live-reloading of configuration settings
		///     whenever the file is modified.
		/// </summary>
		/// <remarks>
		///     The watcher is configured to monitor specific file attributes, such as size and last write time,
		///     and is activated in suitable runtime environments like Unity Editor or standalone applications.
		///     It is managed internally by the ConfigLoader class and is disposed of when no longer needed.
		/// </remarks>
		private static FileSystemWatcher _Watcher;

		/// <summary>
		///     Serves as a synchronization object to manage concurrent access
		///     to file operations within the ConfigLoader class.
		///     This variable is used to ensure thread safety and prevent race conditions
		///     during read and write operations on configuration files.
		/// </summary>
		private static readonly object FileLock = new();

		/// <summary>
		///     Holds a reference to the synchronization context of the main thread.
		///     This variable facilitates operations that require execution on the main thread,
		///     such as UI updates or interaction with frameworks that enforce main-thread constraints.
		/// </summary>
		private static SynchronizationContext _MainThreadContext;

		/// <summary>
		///     Denotes whether the configuration system is in the process of being reloaded.
		///     This variable is managed internally to avoid overlapping or redundant reload operations,
		///     ensuring synchronization, especially during configuration file monitoring and updates.
		/// </summary>
		private static volatile bool _IsReloading;

		#endregion

		#region Initialization

#if CONFIG_LOADER_AUTO_INIT
		/// <summary>
		///     Handles automatic initialization of the configuration system before the first scene loads.
		///     This behavior can be disabled by including "CONFIG_LOADER_MANUAL_INIT" in the Scripting Define Symbols.
		///     By default, synchronous initialization is used for reliability.
		///     To enable asynchronous initialization,
		///     include "CONFIG_LOAD_ASYNC" in the Scripting Define Symbols.
		/// </summary>
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void AutoInitialize()
		{
#if CONFIG_LOAD_ASYNC
			// Use async initialization - faster startup, but config values may not be immediately available
			_ = InitializeAsync(); // Fire and forget
#else
			// Use synchronous initialization - ensures config values are ready before scene objects start
			Initialize();
#endif
		}
#endif


		/// <summary>
		///     Synchronously initializes the configuration system.
		///     Discovers and loads all available configuration settings from the configuration file,
		///     ensuring they are ready before proceeding.
		///     It also establishes a file watching
		///     for runtime updates in supported environments, enabling dynamic configuration changes.
		///     This method ensures all initialization tasks are completed before returning,
		///     potentially blocking the calling thread during the process.
		/// </summary>
		public static void Initialize()
		{
			lock (InitializationLock)
			{
				if (_IsInitialized) return;

				if (_IsInitializing)
				{
					// If async initialization is in progress, wait for it
					_InitializationTask?.GetAwaiter().GetResult();
					return;
				}

				_IsInitializing = true;
			}

			try
			{
				_MainThreadContext = SynchronizationContext.Current;
				PandoraLogger.LogConfig($"Initializing synchronously... Config file path: {GetConfigPath()}");

				DiscoverSettings();
				LoadFromFileSync();

				lock (InitializationLock)
				{
					_IsInitialized = true;
					_IsInitializing = false;
				}

				PandoraLogger.LogConfig($"Synchronous initialization complete. {Settings.Count} settings loaded.");

				// Automatically start watching for changes in supported environments.
#if UNITY_EDITOR || UNITY_STANDALONE
				StartWatching();
#endif
			}
			catch (Exception e)
			{
				lock (InitializationLock)
				{
					_IsInitializing = false;
				}

				if (!(e is InvalidOperationException))
					PandoraLogger.LogConfigError($"Synchronous initialization failed: {e.Message}");
				throw;
			}
		}

		/// <summary>
		///     Asynchronously initializes the configuration system.
		///     Discovers and registers configuration settings, loads their values from the configuration file,
		///     and prepares the system for handling runtime updates.
		///     Ensures thread safety during initialization.
		/// </summary>
		/// <returns>Returns a Task representing the asynchronous initialization process.</returns>
		public static Task InitializeAsync()
		{
			lock (InitializationLock)
			{
				if (_IsInitialized)
					return Task.CompletedTask;

				if (_IsInitializing && _InitializationTask != null)
					// Return the existing task if initialization is already in progress
					return _InitializationTask;

				_IsInitializing = true;
				_InitializationTask = InitializeAsyncInternal();
				return _InitializationTask;
			}
		}

		/// <summary>
		///     Internal method that performs asynchronous initialization of the configuration system.
		///     Ensures proper setup of configuration settings, handles loading from files,
		///     and prepares the system for runtime operations.
		///     This method should only
		///     be called within a controlled flow to maintain thread safety and reliability.
		/// </summary>
		/// <returns>
		///     A Task representing the asynchronous operation.
		///     When complete, it indicates that the configuration system has been fully initialized and is ready for use.
		/// </returns>
		private static async Task InitializeAsyncInternal()
		{
			try
			{
				_MainThreadContext = SynchronizationContext.Current;
				PandoraLogger.LogConfig($"Initializing asynchronously... Config file path: {GetConfigPath()}");

				DiscoverSettings();
				await LoadFromFileAsync();

				lock (InitializationLock)
				{
					_IsInitialized = true;
					_IsInitializing = false;
				}

				PandoraLogger.LogConfig($"Asynchronous initialization complete. {Settings.Count} settings loaded.");

				// Automatically start watching for changes in supported environments.
#if UNITY_EDITOR || UNITY_STANDALONE
				StartWatching();
#endif
			}
			catch (Exception e)
			{
				lock (InitializationLock)
				{
					_IsInitializing = false;
					_InitializationTask = null;
				}

				if (!(e is InvalidOperationException))
					PandoraLogger.LogConfigError($"Asynchronous initialization failed: {e.Message}");
				throw;
			}
		}

		/// <summary>
		///     Ensures that the configuration system is fully initialized and ready for use.
		///     If the system has not been initialized, this method will perform a synchronous initialization process,
		///     ensuring all necessary dependencies and configurations are loaded correctly.
		///     This is a safeguard to guarantee the readiness of the configuration system
		///     before executing any operations reliant on its state.
		/// </summary>
		public static void EnsureInitialized()
		{
			if (!_IsInitialized) Initialize();
		}

		/// <summary>
		///     Indicates whether the configuration system has been successfully initialized.
		///     Returns true if the initialization process, involving configuration discovery,
		///     loading, and runtime readiness, has been completed.
		///     Otherwise, returns false.
		/// </summary>
		public static bool IsInitialized => _IsInitialized;

		#endregion

		#region Public API

		/// <summary>
		///     Retrieves the absolute file path of the configuration file used by the system.
		///     The path is determined based on platform-specific directories and file naming conventions:
		///     - In Unity Editor: Located in the project's root directory.
		///     - In standalone builds: Placed next to the application's executable.
		///     - On mobile platforms: Stored in the persistent data directory.
		///     A default file name, such as "config.yaml" or "config.ini", is used based on system flags.
		/// </summary>
		/// <returns>
		///     A string representing the full path to the configuration file.
		/// </returns>
		public static string GetConfigPath()
		{
			if (string.IsNullOrEmpty(_ConfigFilePath))
			{
#if USE_YAML_CONFIG && HAVE_VYAML
				var fileName = "config.yaml";
#else
				var fileName = "config.ini";
#endif

#if UNITY_EDITOR
				_ConfigFilePath = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, fileName);
#elif UNITY_IOS || UNITY_ANDROID
	   _ConfigFilePath = Path.Combine(Application.persistentDataPath, fileName);
#elif UNITY_STANDALONE
	   _ConfigFilePath = Path.Combine(Path.GetDirectoryName(Application.dataPath)!, fileName);
#else
				_ConfigFilePath = Path.Combine(Application.persistentDataPath, fileName);
#endif
			}

			return _ConfigFilePath;
		}

		/// <summary>
		///     Registers a custom parser for handling complex or non-primitive types.
		///     The parser must implement the IConfigValueParser interface and should be added
		///     prior to the completion of the ConfigLoader initialization process.
		/// </summary>
		/// <param name="_parser">
		///     The custom parser to be registered, responsible for
		///     parsing and converting complex types to and from strings within the configuration system.
		/// </param>
		public static void RegisterParser(IConfigValueParser _parser)
		{
			if (_IsInitialized)
			{
				PandoraLogger.LogConfigError("Parsers must be registered before initialization.");
				return;
			}

			if (!Parsers.Contains(_parser)) Parsers.Add(_parser);
		}

		/// <summary>
		///     Retrieves an appropriate parser instance that implements <see cref="IConfigValueParser" />
		///     for handling the specified type.
		///     If a parser that can handle the type is not found, null is returned.
		/// </summary>
		/// <param name="_type">The type for which a suitable parser is being requested.</param>
		/// <returns>
		///     An instance of <see cref="IConfigValueParser" /> capable of parsing the specified type,
		///     or null if no registered parser can handle the given type.
		/// </returns>
		internal static IConfigValueParser GetParserForType(Type _type)
		{
			return Parsers.FirstOrDefault(_p => _p.CanParse(_type));
		}

		#endregion

		#region File Operations

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

		#region INI Parsing

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

		#endregion

		#region YAML Parsing

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

		#region File Watching

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

		#region Settings Discovery

		/// <summary>
		///     Discovers and registers configuration settings by scanning all loaded assemblies for static classes
		///     that contain configuration entries.
		///     This is an internal process used during initialization to
		///     ensure all relevant settings are available for the application's configuration system.
		/// </summary>
		private static void DiscoverSettings()
		{
			Settings.Clear();
			var processedTypes = new HashSet<Type>(); // FIX: Keep track of processed types
			var assemblies = AppDomain.CurrentDomain.GetAssemblies();
			foreach (var assembly in assemblies)
				try
				{
					var types = assembly.GetTypes();
					foreach (var type in types)
					{
						if (processedTypes.Contains(type)) continue;

						if (type.IsClass && type.IsSealed && type.IsAbstract)
							DiscoverSettingsInType(type, type.Name, processedTypes);
					}
				}
				catch (ReflectionTypeLoadException)
				{
					// Ignore assemblies that fail to load types
				}
		}

		/// <summary>
		///     Scans the specified type for static, readonly fields that implement the IConfigEntry interface and
		///     performs discovery of configuration entries.
		///     Handles recursive processing of nested types
		///     and groups the discovered settings under the provided group name.
		///     Ensures each type is processed only once using the collection of processed types.
		/// </summary>
		/// <param name="_type">The type to scan for configuration entry fields.</param>
		/// <param name="_groupName">The group name under which the discovered settings will be categorized.</param>
		/// <param name="_processedTypes">
		///     The collection of types
		///     that have already been processed to prevent redundant scanning.
		/// </param>
		private static void DiscoverSettingsInType(Type _type, string _groupName, ISet<Type> _processedTypes)
		{
			_processedTypes.Add(_type); // Mark this type as processed

			var fields = _type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
			foreach (var field in fields)
				if (field.IsInitOnly && typeof(IConfigEntry).IsAssignableFrom(field.FieldType))
				{
					var attribute = field.GetCustomAttribute<ConfigAttribute>();
					if (attribute != null)
					{
						var existingSetting = Settings.FirstOrDefault(_s => _s.Key == attribute.Key);
						if (existingSetting != null)
						{
							var richMessage =
								"<color=yellow>[ConfigLoader]</color> Initialization failed due to a duplicate configuration key.\n\n" +
								$"<color=red><b>Error:</b></color> The key <color=yellow>'{attribute.Key}'</color> defined in <color=white>{_groupName}.{field.Name}</color> is already in use.\n" +
								$"It was previously defined in the group <color=white>'{existingSetting.GroupName}'</color>. Config keys must be unique.";

							Debug.LogError(richMessage);
							throw new InvalidOperationException(
								$"Duplicate config key '{attribute.Key}' found. Please check the console for details.");
						}

						try
						{
							var entry = (IConfigEntry)Activator.CreateInstance(field.FieldType, attribute, _groupName);
							field.SetValue(null, entry);
							Settings.Add(entry);
						}
						catch (TargetInvocationException ex)
						{
							var innerEx = ex.InnerException;
							if (innerEx is ArgumentException { ParamName: "DefaultValue" } argEx)
							{
								var richMessage =
									"<color=yellow>[ConfigLoader]</color> Initialization failed due to an invalid default value in a [Config] attribute.\n\n" +
									$"<b>Setting:</b>\t<color=white>{_groupName}.{field.Name}</color>\n" +
									"<b>Error:</b>\t\tThe provided default value has the wrong type.\n" +
									$"<b>Details:</b>\t{innerEx.Message.Split('\r', '\n')[0]}\n" +
									$"<b>Parameter:</b>\t<color=#FF6666>{argEx.ParamName}</color>";

								Debug.LogError(richMessage);
								throw new InvalidOperationException(
									$"Initialization failed for setting '{_groupName}.{field.Name}'. Please check the console for details.",
									innerEx);
							}

							throw innerEx ?? ex;
						}

						if (GetParserForType(field.FieldType.GetGenericArguments()[0]) == null &&
							!IsPrimitiveOrEnum(field.FieldType.GetGenericArguments()[0]))
							PandoraLogger.LogConfigError($"Error: The type '{field.FieldType.GetGenericArguments()[0].Name}' for setting '{_groupName}.{field.Name}' is not supported. " +
								"To add support, create a class that implements IConfigValueParser and register it with ConfigLoader.RegisterParser().");
					}
				}

			var nestedTypes = _type.GetNestedTypes(BindingFlags.Public | BindingFlags.Static);
			foreach (var nestedType in nestedTypes)
				DiscoverSettingsInType(nestedType, $"{_groupName}.{nestedType.Name}", _processedTypes);
		}

		#endregion

		#region Utility Methods

		/// <summary>
		///     Determines whether the specified type is a primitive type, a string, or an enum.
		/// </summary>
		/// <param name="_type">The type to evaluate.</param>
		/// <returns>True if the type is a primitive, a string, or an enum; otherwise, false.</returns>
		private static bool IsPrimitiveOrEnum(Type _type)
		{
			return _type.IsPrimitive || _type == typeof(string) || _type.IsEnum;
		}

		#endregion
	}
}
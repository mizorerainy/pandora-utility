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
[assembly: InternalsVisibleTo("MizoreRainy.Pandora.Editor.ConfigUtility")]

// ReSharper disable once CheckNamespace
namespace MizoreRainy.Pandora.ConfigUtility
{
	/// <summary>
	///     Handles the initialization, loading, saving, and monitoring of application configuration settings.
	///     This class provides thread-safe methods for managing configuration files and supports runtime updates
	///     while ensuring compatibility with custom parsers.
	/// </summary>
	public static class ConfigLoader
	{
		#region Fields

		/// <summary>
		///     Represents a collection of configuration settings
		///     that are managed and processed by the configuration loader.
		///     This variable is used
		///     to store instances of implemented configuration entries within the application lifecycle.
		/// </summary>
		private static readonly List<IConfigEntry> Settings = new();

		/// <summary>
		///     Maintains a list of configuration value parsers used to handle
		///     the conversion between various data types and their string representations
		///     within the configuration system.
		/// </summary>
		private static readonly List<IConfigValueParser> Parsers = new();

		/// <summary>
		///     A private field that tracks the initialization state of the ConfigLoader.
		///     This field is set to true once all initialization processes, such as configuration
		///     discovery and loading, have been successfully completed.
		/// </summary>
		private static bool _IsInitialized;

		/// <summary>
		///     Indicates whether the configuration system is currently in the process of initializing.
		///     This variable is used internally to prevent redundant or simultaneous initialization
		///     attempts within the application.
		/// </summary>
		private static bool _IsInitializing;

		/// <summary>
		///     Represents the task that tracks any ongoing asynchronous initialization process
		///     for the configuration system.
		///     Enables safe handling and awaiting of the same
		///     initialization process from multiple callers.
		/// </summary>
		private static Task _InitializationTask;

		/// <summary>
		///     Serves as a synchronization object to ensure thread-safe management of initialization processes.
		///     Used to prevent concurrent execution and maintain consistency during critical operations.
		/// </summary>
		private static readonly object InitializationLock = new();

		/// <summary>
		///     Stores the file path to the configuration file used by the application.
		///     The location of the file is determined dynamically based on the runtime environment.
		///     For instance, in the Unity Editor, it points to the project root directory,
		///     while in standalone builds, it resides next to the application executable.
		/// </summary>
		private static string _ConfigFilePath;

		/// <summary>
		///     A private variable representing a file system watcher that monitors the configuration file for changes.
		///     This is used
		///     to enable live-reloading of configuration settings during runtime
		///     when modifications are detected.
		/// </summary>
		/// <remarks>
		///     The variable is initialized and managed within the ConfigLoader class and is active in environments
		///     like the Unity Editor or Standalone builds.
		///     It observes changes such as file size or last write time updates.
		/// </remarks>
		private static FileSystemWatcher _Watcher;

		/// <summary>
		///     Serves as a synchronization mechanism for protecting access to file operations
		///     within the ConfigLoader class.
		///     Ensures thread-safe interactions with the configuration
		///     file to prevent race conditions and data inconsistencies during read or write operations.
		/// </summary>
		private static readonly object FileLock = new();

		/// <summary>
		///     Represents the synchronization context of the main thread.
		///     This is used to ensure that certain operations, particularly those requiring
		///     the main thread access (e.g., UI updates or interactions with systems like Unity APIs),
		///     can safely switch context back to the main thread.
		/// </summary>
		private static SynchronizationContext _MainThreadContext;

		/// <summary>
		///     Indicates whether the configuration is currently being reloaded.
		///     This flag helps prevent concurrent or redundant reload operations,
		///     especially when reacting to file change events.
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
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
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
		///     Discovers all available configuration settings, loads them
		///     from the configuration file, and establishes files watching
		///     for runtime updates in supported environments.
		///     This method ensures all operations are completed before returning,
		///     blocking the caller if necessary.
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
				Debug.Log($"[ConfigLoader] Initializing synchronously... Config file path: {GetConfigPath()}");

				DiscoverSettings();
				LoadFromFileSync();

				lock (InitializationLock)
				{
					_IsInitialized = true;
					_IsInitializing = false;
				}

				Debug.Log($"[ConfigLoader] Synchronous initialization complete. {Settings.Count} settings loaded.");

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
					Debug.LogError($"[ConfigLoader] Synchronous initialization failed: {e.Message}");
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
		///     When complete, it indicates
		///     that the configuration system has been fully initialized and is ready for use.
		/// </returns>
		private static async Task InitializeAsyncInternal()
		{
			try
			{
				_MainThreadContext = SynchronizationContext.Current;
				Debug.Log($"[ConfigLoader] Initializing asynchronously... Config file path: {GetConfigPath()}");

				DiscoverSettings();
				await LoadFromFileAsync();

				lock (InitializationLock)
				{
					_IsInitialized = true;
					_IsInitializing = false;
				}

				Debug.Log($"[ConfigLoader] Asynchronous initialization complete. {Settings.Count} settings loaded.");

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
					Debug.LogError($"[ConfigLoader] Asynchronous initialization failed: {e.Message}");
				throw;
			}
		}

		/// <summary>
		///     Ensures the ConfigLoader is initialized.
		///     If the loader is not yet initialized,
		///     it will perform a synchronous initialization to guarantee readiness.
		///     This method is designed to be a safe way to ensure that the configuration system
		///     is prepared before proceeding with operations that depend on it.
		/// </summary>
		public static void EnsureInitialized()
		{
			if (!_IsInitialized) Initialize();
		}

		/// <summary>
		///     Gets a value indicating whether the configuration system has been successfully initialized.
		///     When true, all initialization tasks, such as loading configuration files and setting up watchers,
		///     have been completed.
		/// </summary>
		public static bool IsInitialized => _IsInitialized;

		#endregion

		#region Public API

		/// <summary>
		///     Retrieves the file path to the configuration file used by the system.
		///     The location of the file varies depending on the platform:
		///     - In the Unity Editor, the file is located in the project's root directory.
		///     - On standalone builds, it is located next to the executable.
		///     - On mobile platforms, it resides in the persistent data directory.
		/// </summary>
		/// <returns>
		///     The absolute file path of the configuration file as a string.
		/// </returns>
		public static string GetConfigPath()
		{
			if (string.IsNullOrEmpty(_ConfigFilePath))
			{
#if USE_YAML_CONFIG && HAVE_VYAML
				var fileName = "config.yaml";
#else
	   var fileName = " config.ini";
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
		///     parsing
		///     and converting complex types to and from strings within the configuration system.
		/// </param>
		public static void RegisterParser(IConfigValueParser _parser)
		{
			if (_IsInitialized)
			{
				Debug.LogError("[ConfigLoader] Parsers must be registered before initialization.");
				return;
			}

			if (!Parsers.Contains(_parser)) Parsers.Add(_parser);
		}

		/// <summary>
		///     Retrieves an appropriate <see cref="IConfigValueParser" /> for the specified type.
		///     If no registered parser can handle the given type, null is returned.
		/// </summary>
		/// <param name="_type">The type for which a parser is requested.</param>
		/// <returns>
		///     An <see cref="IConfigValueParser" /> instance capable of handling the type, or null if no suitable parser is
		///     registered.
		/// </returns>
		internal static IConfigValueParser GetParserForType(Type _type)
		{
			return Parsers.FirstOrDefault(_p => _p.CanParse(_type));
		}

		#endregion

		#region File Operations

		/// <summary>
		///     Synchronously loads configuration settings from a file on disk.
		///     If the configuration file does not exist, a new file is created with default settings.
		///     Ensures integrity by replacing invalid or missing entries in the file with default values.
		///     Automatically saves the configuration after loading to ensure consistency.
		/// </summary>
		private static void LoadFromFileSync()
		{
			var path = GetConfigPath();
			if (!File.Exists(path))
			{
				Debug.Log("[ConfigLoader] Config file not found. Creating a new one with default values.");
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
		///     Invalid or missing entries are replaced with their corresponding default values to ensure consistency.
		/// </summary>
		/// <returns>
		///     A Task representing the asynchronous operation of loading and validating the configuration file.
		/// </returns>
		public static async Task LoadFromFileAsync()
		{
			var path = GetConfigPath();
			if (!File.Exists(path))
			{
				Debug.Log("[ConfigLoader] Config file not found. Creating a new one with default values.");
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
		///     Use the appropriate format (e.g., INI or YAML) based on the active configuration.
		///     Ensures the file watcher is resumed after the save operation is completed.
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
		///     Ensures the configuration's integrity during the save operation and safeguards against unintended
		///     changes by temporarily stopping the file watcher.
		/// </summary>
		/// <returns>
		///     A task representing the asynchronous save operation,
		///     completing once all changes are written to the configuration file.
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
		///     The default values are then saved back to the configuration file, overwriting any existing content.
		///     This operation is performed asynchronously.
		/// </summary>
		/// <returns>
		///     A task
		///     representing the asynchronous operation
		///     of resetting and saving default configuration settings.
		/// </returns>
		public static async Task ResetToDefaultsAsync()
		{
			// Ensure the loader is initialized so we know about all the settings.
			await InitializeAsync();

			Debug.Log("[ConfigLoader] Resetting all settings to their default values...");
			foreach (var setting in Settings) setting.SetToDefault();

			// Now, save these default values back to the file.
			await SaveAsync();
			Debug.Log("[ConfigLoader] All settings have been reset to defaults and saved.");
		}

		#endregion

		#region INI Parsing

		/// <summary>
		///     Synchronously loads configuration settings from a specified INI file.
		///     Reads key-value pairs from the file, applies values to registered settings, and
		///     sets any missing configuration entries to their default values.
		/// </summary>
		/// <param name="_path">The file path to the INI configuration file.</param>
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
				Debug.LogError($"[ConfigLoader] Failed to read INI config. Error: {e.Message}");
			}

			foreach (var setting in Settings)
				if (fileValues.TryGetValue(setting.Key, out var rawValue)) setting.SetValueFromString(rawValue);
				else setting.SetToDefault();
		}

		// ReSharper disable once UnusedMember.Local
		/// <summary>
		///     Asynchronously loads configuration settings from an INI file.
		///     The provided file path is used to locate and read the configuration data.
		///     This method operates asynchronously but executes the synchronous version of the load operation.
		/// </summary>
		/// <param name="_path">The file path of the INI file to load configuration settings from.</param>
		/// <returns>
		///     Returns a Task
		///     that represents the asynchronous operation of loading the INI configuration.
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
				Debug.LogError($"[ConfigLoader] Failed to save INI config! Error: {e.Message}");
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
				Debug.LogError($"[ConfigLoader] Failed to save INI config! Error: {e.Message}");
			}
		}

		#endregion

		#region YAML Parsing

#if HAVE_VYAML
		/// <summary>
		///     Loads configuration settings synchronously from a YAML file.
		///     Parses the YAML data and populates the registered configuration entries
		///     with their corresponding values or defaults if keys are missing.
		///     If an error occurs during file reading or deserialization,
		///     an empty configuration dictionary is used, and the system logs the error.
		/// </summary>
		/// <param name="_path">The file path to the YAML configuration file.</param>
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

				yamlData = YamlSerializer.Deserialize<Dictionary<string, object>>(yamlBytes) ??
				           new Dictionary<string, object>();
			}
			catch (Exception e)
			{
				Debug.LogError($"[ConfigLoader] Failed to read YAML config. Error: {e.Message}");
				yamlData = new Dictionary<string, object>();
			}

			foreach (var setting in Settings)
				// A more complex implementation could handle nesting based on GroupName
				if (yamlData.TryGetValue(setting.Key, out var value))
					setting.SetValueFromString(value?.ToString() ?? "");
				else setting.SetToDefault();
		}

		/// <summary>
		///     Asynchronously loads configuration settings from a YAML file.
		///     Reads the specified file path, parses the YAML data,
		///     and applies it to the appropriate configuration settings.
		///     The operation completes after the YAML file is fully processed.
		/// </summary>
		/// <param name="_path">The file path to the YAML configuration file to be loaded.</param>
		/// <returns>A task that represents the asynchronous file loading operation.</returns>
		private static Task LoadFromYamlAsync(string _path)
		{
			LoadFromYamlSync(_path);
			return Task.CompletedTask;
		}

		/// <summary>
		///     Saves the current configuration settings to a YAML file synchronously.
		///     This method collects all registered settings and writes them to the specified file path.
		/// </summary>
		/// <param name="_path">The file path where the YAML configuration will be saved.</param>
		private static void SaveToYamlSync(string _path)
		{
			var data = new Dictionary<string, object>();
			foreach (var setting in Settings)
				// A more complex implementation could build a nested dictionary based on GroupName
				data[setting.Key] = setting.GetType().GetProperty("Value")?.GetValue(setting);
			try
			{
				var yamlBytes = YamlSerializer.Serialize(data).ToArray();
				lock (FileLock)
				{
					File.WriteAllBytes(_path, yamlBytes);
				}
			}
			catch (Exception e)
			{
				Debug.LogError($"[ConfigLoader] Failed to save YAML config! Error: {e.Message}");
			}
		}

		/// <summary>
		///     Saves the current application configuration settings to a YAML file asynchronously.
		///     This method serializes all registered configuration settings into a YAML format
		///     and writes them to the specified file path.
		/// </summary>
		/// <param name="_path">
		///     The absolute or relative path to the YAML file
		///     where the configuration settings will be saved.
		/// </param>
		/// <returns>
		///     A task representing the asynchronous save operation.
		///     The task completes when the file writing is finished.
		/// </returns>
		private static async Task SaveToYamlAsync(string _path)
		{
			var data = new Dictionary<string, object>();
			foreach (var setting in Settings)
				data[setting.Key] = setting.GetType().GetProperty("Value")?.GetValue(setting);
			try
			{
				var yamlBytes = YamlSerializer.Serialize(data).ToArray();
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
				Debug.LogError($"[ConfigLoader] Failed to save YAML config! Error: {e.Message}");
			}
		}
#endif

		#endregion

		#region File Watching

		/// <summary>
		///     Starts watching the configuration file for changes and enables live-reloading.
		///     Uses a file system watcher to detect modifications to the configuration file,
		///     which triggers automatic reloading of settings.
		///     The watcher will be automatically stopped when the application terminates.
		///     If the watcher fails to initialize, live-reloading will be disabled, and an error message will be logged.
		///     This functionality is only supported on certain platforms (e.g., Unity Editor or Standalone builds).
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

				Debug.Log("[ConfigLoader] Started watching config file for changes.");
			}
			catch (Exception e)
			{
				Debug.LogError(
					$"[ConfigLoader] Failed to start file watcher. Live-reloading will be disabled. Error: {e.Message}");
				_Watcher?.Dispose();
				_Watcher = null;
			}
#endif
		}

		/// <summary>
		///     Stops monitoring the configuration file for changes.
		///     This method disables the file system watcher, removes event subscriptions,
		///     and releases resources previously used for monitoring changes.
		///     It prevents
		///     further automatic updates to configuration settings based on file modifications.
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
			Debug.Log("[ConfigLoader] Stopped watching config file.");
#endif
		}

		/// <summary>
		///     Handles the event triggered when the configuration file is changed.
		///     This method ensures settings are reloaded from the updated file to keep the application state consistent.
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
					Debug.Log("[ConfigLoader] File change detected. Reloading settings...");
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
		///     Discovers and registers settings defined within the application by scanning all loaded assemblies.
		///     This method identifies and processes static classes to locate configuration entries, ensuring
		///     all applicable settings are registered for configuration management.
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
		///     Scans the specified type for static, readonly fields implementing the IConfigEntry interface and
		///     performs the discovery of configuration settings.
		///     The discovered settings are grouped under the
		///     provided group name and recursively processed for any nested types.
		///     Prevents redundant processing
		///     by tracking types that have already been handled.
		/// </summary>
		/// <param name="_type">The type to examine for static configuration entry fields.</param>
		/// <param name="_groupName">
		///     The name of the group
		///     under which the discovered configuration settings are registered.
		/// </param>
		/// <param name="_processedTypes">
		///     A collection
		///     maintaining types that have been processed
		///     to avoid redundant scans or recursion.
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
								"[ConfigLoader] Initialization failed due to a duplicate configuration key.\n\n" +
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
									"[ConfigLoader] Initialization failed due to an invalid default value in a [Config] attribute.\n\n" +
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
							Debug.LogError(
								$"[ConfigLoader] Error: The type '{field.FieldType.GetGenericArguments()[0].Name}' for setting '{_groupName}.{field.Name}' is not supported. " +
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
// =================================================================================
// File: ConfigLoader.cs
// Author: MizoreRainy
// Description: The core engine for loading, parsing, and saving configuration.
//              This system is dependency-free and uses built-in .NET Task async.
// =================================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace MizoreRainy.Pandora.ConfigUtility
{
    /// <summary>
    /// Handles the loading, saving, and management of application configuration settings.
    /// Provides methods to initialize the configuration system, handle file-based I/O,
    /// and monitor configuration changes during runtime.
    /// </summary>
    public static class ConfigLoader
    {
        #region Fields

        /// <summary>
        /// Represents a container for configuration settings within the application.
        /// This class interacts with the configuration loader to manage and organize settings.
        /// </summary>
        private static readonly List<IConfigEntry> Settings = new();

        /// <summary>
        /// A collection of registered configuration value parsers used by the ConfigLoader.
        /// These parsers handle conversion of complex or non-primitive types to and from strings
        /// for use in the configuration system.
        /// </summary>
        private static readonly List<IConfigValueParser> Parsers = new();

        /// <summary>
        /// Indicates whether the ConfigLoader has been initialized.
        /// When true, initialization tasks (e.g., setting discovery, file loading) have been completed.
        /// </summary>
        private static bool _IsInitialized;

        /// <summary>
        /// Indicates whether the ConfigLoader has started initializing (but may not be complete yet).
        /// Used to prevent multiple simultaneous initialization attempts.
        /// </summary>
        private static bool _IsInitializing;

        /// <summary>
        /// Task that represents the ongoing async initialization, if any.
        /// Used to await the same initialization task from multiple callers.
        /// </summary>
        private static Task _InitializationTask;

        /// <summary>
        /// Lock object for thread-safe initialization state management.
        /// </summary>
        private static readonly object InitializationLock = new();

        /// <summary>
        /// Represents the file path to the configuration file used by the application.
        /// This path is determined based on the application environment:
        /// In Unity Editor, the configuration file is located in the project root directory.
        /// In a standalone build, the configuration file is located next to the executable.
        /// </summary>
        private static string _ConfigFilePath;

        /// <summary>
        /// Represents a file system watcher that monitors changes to the configuration file.
        /// Used to enable live-reloading of settings when the config file is modified.
        /// </summary>
        /// <remarks>
        /// This variable is initialized and managed by the ConfigLoader class. It monitors file updates,
        /// such as changes in size or the last writing time, in supported environments (e.g., Unity Editor or Standalone builds).
        /// </remarks>
        private static FileSystemWatcher _Watcher;

        /// <summary>
        /// A synchronization object used for thread-safe access to file operations
        /// within the ConfigLoader class.
        /// Ensures that multiple threads do not simultaneously read or write
        /// to the configuration file, preventing data corruption.
        /// </summary>
        private static readonly object FileLock = new();

        /// <summary>
        /// Stores the synchronization context associated with the main thread.
        /// This allows for context switching back to the main thread when performing
        /// asynchronous operations that interact with systems requiring the main thread access,
        /// such as Unity's APIs.
        /// </summary>
        private static SynchronizationContext _MainThreadContext;

        #endregion

        #region Initialization

#if CONFIG_LOADER_AUTO_INIT
        /// <summary>
        /// Automatic initialization before the first scené loads.
        /// This can be disabled by adding "CONFIG_LOADER_MANUAL_INIT" to Scripting Define Symbols.
        /// By default, use synchronous initialization for reliability.
        /// Add "CONFIG_LOAD_ASYNC" to Scripting Define Symbols to use asynchronous initialization instead.
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
        /// Synchronously initializes the configuration system.
        /// Discovers configuration settings, loads them from the configuration file,
        /// and sets up file watching if supported.
        /// This method blocks until initialization is complete.
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
                Debug.LogError($"[ConfigLoader] Synchronous initialization failed: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Asynchronously initializes the configuration system.
        /// Discovers configuration settings, loads them from the configuration file,
        /// and sets up file watching if supported.
        /// </summary>
        /// <returns>Returns a Task that represents the asynchronous initialization operation.</returns>
        public static Task InitializeAsync()
        {
            lock (InitializationLock)
            {
                if (_IsInitialized)
                    return Task.CompletedTask;

                if (_IsInitializing && _InitializationTask != null)
                {
                    // Return the existing task if initialization is already in progress
                    return _InitializationTask;
                }

                _IsInitializing = true;
                _InitializationTask = InitializeAsyncInternal();
                return _InitializationTask;
            }
        }

        /// <summary>
        /// Internal async initialization method.
        /// </summary>
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
                Debug.LogError($"[ConfigLoader] Asynchronous initialization failed: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Ensures the ConfigLoader is initialized. If not, initialize it synchronously.
        /// This is a safe method to call when you need guaranteed initialization.
        /// </summary>
        public static void EnsureInitialized()
        {
            if (!_IsInitialized)
            {
                Initialize();
            }
        }

        /// <summary>
        /// Gets whether the ConfigLoader has been fully initialized.
        /// </summary>
        public static bool IsInitialized => _IsInitialized;

        #endregion

        #region Public API

        /// <summary>
        /// Retrieves the path to the configuration file.
        /// When running in the Unity Editor, the file path is located in the project root directory.
        /// When running in a standalone build, the file is located next to the executable.
        /// </summary>
        /// <returns>
        /// The absolute path to the configuration file as a string.
        /// </returns>
        public static string GetConfigPath()
        {
            if (string.IsNullOrEmpty(_ConfigFilePath))
            {
#if UNITY_EDITOR
                _ConfigFilePath = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "config.txt");
#else
                _ConfigFilePath = Path.Combine(Path.GetDirectoryName(Application.dataPath)!, "config.txt");
#endif
            }
            return _ConfigFilePath;
        }

        /// <summary>
        /// Registers a custom parser for handling complex or non-primitive types.
        /// The parser must implement the IConfigValueParser interface and should be added
        /// before the ConfigLoader initialization process completes.
        /// </summary>
        /// <param name="_parser">The custom parser to be registered, used for converting
        /// complex types to and from strings in the configuration system.</param>
        public static void RegisterParser(IConfigValueParser _parser)
        {
            if (_IsInitialized)
            {
                Debug.LogError("[ConfigLoader] Parsers must be registered before initialization.");
                return;
            }
            if (!Parsers.Contains(_parser))
            {
                Parsers.Add(_parser);
            }
        }

        /// <summary>
        /// Retrieves an appropriate <see cref="IConfigValueParser"/> for the specified type.
        /// If no registered parser can handle the given type, null is returned.
        /// </summary>
        /// <param name="_type">The type for which a parser is requested.</param>
        /// <returns>An <see cref="IConfigValueParser"/> instance capable of handling the type, or null if no suitable parser is registered.</returns>
        internal static IConfigValueParser GetParserForType(Type _type)
        {
            foreach (var parser in Parsers)
            {
                if (parser.CanParse(_type))
                {
                    return parser;
                }
            }
            return null;
        }

        #endregion

        #region File Operations

        /// <summary>
        /// Synchronously loads application settings from the configuration file.
        /// If the file does not exist, it creates a new configuration file with default values.
        /// Invalid or missing entries in the file are replaced with default values for the corresponding settings.
        /// </summary>
        private static void LoadFromFileSync()
        {
            string path = GetConfigPath();
            if (!File.Exists(path))
            {
                Debug.Log("[ConfigLoader] Config file not found. Creating a new one with default values.");
                SaveSync();
                return;
            }

            Dictionary<string, string> fileValues = new Dictionary<string, string>();
            try
            {
                string[] lines;
                lock (FileLock)
                {
                    lines = File.ReadAllLines(path);
                }

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.Trim().StartsWith("#"))
                        continue;

                    int equalsIndex = line.IndexOf('=');
                    if (equalsIndex > 0)
                    {
                        string key = line.Substring(0, equalsIndex).Trim();
                        string value = line.Substring(equalsIndex + 1);
                        fileValues[key] = value;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ConfigLoader] Failed to read config file. Using default values. Error: {e.Message}");
            }

            foreach (var setting in Settings)
            {
                if (fileValues.TryGetValue(setting.Key, out string rawValue))
                {
                    setting.SetValueFromString(rawValue);
                }
                else
                {
                    setting.SetToDefault();
                }
            }

            SaveSync();
        }

        /// <summary>
        /// Asynchronously loads application settings from the configuration file.
        /// If the file does not exist, it creates a new configuration file with default values.
        /// Invalid or missing entries in the file are replaced with default values for the corresponding settings.
        /// </summary>
        /// <returns>
        /// A Task that represents the asynchronous operation of loading the configuration file.
        /// </returns>
        public static async Task LoadFromFileAsync()
        {
            string path = GetConfigPath();
            if (!File.Exists(path))
            {
                Debug.Log("[ConfigLoader] Config file not found. Creating a new one with default values.");
                await SaveAsync();
                return;
            }

            Dictionary<string, string> fileValues = new Dictionary<string, string>();
            try
            {
                string[] lines;
                lock (FileLock)
                {
                    lines = File.ReadAllLines(path);
                }

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.Trim().StartsWith("#"))
                        continue;

                    int equalsIndex = line.IndexOf('=');
                    if (equalsIndex > 0)
                    {
                        string key = line.Substring(0, equalsIndex).Trim();
                        string value = line.Substring(equalsIndex + 1);
                        fileValues[key] = value;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ConfigLoader] Failed to read config file. Using default values. Error: {e.Message}");
            }

            foreach (var setting in Settings)
            {
                if (fileValues.TryGetValue(setting.Key, out string rawValue))
                {
                    setting.SetValueFromString(rawValue);
                }
                else
                {
                    setting.SetToDefault();
                }
            }

            await SaveAsync();
        }

        /// <summary>
        /// Synchronously saves all settings to the configuration file.
        /// Ensures that the configuration file is updated with the latest values.
        /// </summary>
        private static void SaveSync()
        {
            string path = GetConfigPath();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Application Configuration File");
            sb.AppendLine($"# Last saved: {DateTime.Now}");

            var groupedSettings = Settings.GroupBy(_s => _s.GroupName).OrderBy(_g => _g.Key);

            foreach (var group in groupedSettings)
            {
                sb.AppendLine();
                sb.AppendLine($"#==================================================");
                sb.AppendLine($"# :: {group.Key} Settings");
                sb.AppendLine($"#==================================================");

                foreach (var setting in group.OrderBy(_s => _s.Key))
                {
                    if (!string.IsNullOrEmpty(setting.Description))
                    {
                        sb.AppendLine($"# {setting.Description}");
                    }
                    sb.AppendLine($"{setting.Key}={setting.GetValueAsString()}");
                }
            }

            try
            {
                byte[] encodedText = Encoding.UTF8.GetBytes(sb.ToString());
                lock (FileLock)
                {
                    File.WriteAllBytes(path, encodedText);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ConfigLoader] Failed to save config file! Error: {e.Message}");
            }
        }

        /// <summary>
        /// Saves all settings to the configuration file asynchronously.
        /// Ensures that the configuration file is updated with the latest values while handling I/O operations safely.
        /// </summary>
        /// <returns>
        /// A task representing the asynchronous save operation. The task completes once the write operation is finished.
        /// </returns>
        public static async Task SaveAsync()
        {
            string path = GetConfigPath();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Application Configuration File");
            sb.AppendLine($"# Last saved: {DateTime.Now}");

            var groupedSettings = Settings.GroupBy(_s => _s.GroupName).OrderBy(_g => _g.Key);

            foreach (var group in groupedSettings)
            {
                sb.AppendLine();
                sb.AppendLine($"#==================================================");
                sb.AppendLine($"# :: {group.Key} Settings");
                sb.AppendLine($"#==================================================");

                foreach (var setting in group.OrderBy(_s => _s.Key))
                {
                    if (!string.IsNullOrEmpty(setting.Description))
                    {
                        sb.AppendLine($"# {setting.Description}");
                    }
                    sb.AppendLine($"{setting.Key}={setting.GetValueAsString()}");
                }
            }

            try
            {
                byte[] encodedText = Encoding.UTF8.GetBytes(sb.ToString());
                await Task.Run(() =>
                {
                    lock (FileLock)
                    {
                        File.WriteAllBytes(path, encodedText);
                    }
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"[ConfigLoader] Failed to save config file! Error: {e.Message}");
            }
        }

        /// <summary>
        /// Resets all configuration settings to their default values as defined in the code
        /// and then saves these defaults to the config.txt file, overwriting its current content.
        /// </summary>
        /// <returns>A task representing the asynchronous reset and save operation.</returns>
        public static async Task ResetToDefaultsAsync()
        {
            // Ensure the loader is initialized so we know about all the settings.
            await InitializeAsync();

            Debug.Log("[ConfigLoader] Resetting all settings to their default values...");
            foreach (var setting in Settings)
            {
                setting.SetToDefault();
            }

            // Now, save these default values back to the file.
            await SaveAsync();
            Debug.Log("[ConfigLoader] All settings have been reset to defaults and saved to config.txt.");
        }

        #endregion

        #region File Watching

        /// <summary>
        /// Starts monitoring the configuration file for changes and enables live-reloading.
        /// This uses a file system watcher to detect modifications to the configuration file.
        /// Automatically stops watching when the application quits.
        /// </summary>
        public static void StartWatching()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (_Watcher != null) return;

            try
            {
                string path = GetConfigPath();
                _Watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(path));
                _Watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
                _Watcher.Changed += OnConfigFileChanged;
                _Watcher.EnableRaisingEvents = true;

                Application.quitting += StopWatching;

                Debug.Log("[ConfigLoader] Started watching config.txt for changes.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ConfigLoader] Failed to start file watcher. Live-reloading will be disabled. Error: {e.Message}");
                _Watcher?.Dispose();
                _Watcher = null;
            }
#endif
        }

        /// <summary>
        /// Stops monitoring the configuration file for changes.
        /// This method disables the file system watcher, unsubscribes from events,
        /// and releases associated resources. It should be called to halt automatic
        /// updates of configuration settings based on file modifications.
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
            Debug.Log("[ConfigLoader] Stopped watching config.txt.");
#endif
        }

        /// <summary>
        /// Handles the event triggered when the configuration file changes.
        /// Automatically reloads the settings from the file when a change is detected.
        /// </summary>
        /// <param name="_sender">The source of the event. Typically, the FileSystemWatcher.</param>
        /// <param name="_e">The event arguments containing information about the changed file.</param>
        [SuppressMessage("ReSharper", "AsyncVoidLambda")]
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private static async void OnConfigFileChanged(object _sender, FileSystemEventArgs _e)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            // This event can fire on a background thread. We need to switch to the main thread
            // to safely interact with Unity's systems and our static data.
            _MainThreadContext?.Post(async _ =>
            {
                Debug.Log("[ConfigLoader] File change detected. Reloading settings...");
                await LoadFromFileAsync();
            }, null);
        }

        #endregion

        #region Settings Discovery

        /// <summary>
        /// Scans all assemblies in the current application domain to find and register
        /// settings marked as applicable for configuration management. This method identifies
        /// static classes and performs type inspection to locate config entries.
        /// </summary>
        private static void DiscoverSettings()
        {
            Settings.Clear();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        if (type.IsClass && type.IsSealed && type.IsAbstract)
                        {
                            DiscoverSettingsInType(type, type.Name);
                        }
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // Ignore assemblies that fail to load types
                }
            }
        }

        /// <summary>
        /// Scans a provided type for static, readonly fields implementing the IConfigEntry interface.
        /// Discovers config settings defined within the given type or its nested types and registers them
        /// under a specified group name. Throw an exception if duplicate config keys are detected.
        /// </summary>
        /// <param name="_type">The type to scan for static configuration entry fields.</param>
        /// <param name="_groupName">The group name under which the discovered settings are categorized.</param>
        private static void DiscoverSettingsInType(Type _type, string _groupName)
        {
            var fields = _type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            foreach (var field in fields)
            {
                if (field.IsInitOnly && typeof(IConfigEntry).IsAssignableFrom(field.FieldType))
                {
                    var attribute = field.GetCustomAttribute<ConfigAttribute>();
                    if (attribute != null)
                    {
                        if (Settings.Any(_s => _s.Key == attribute.Key))
                        {
                            throw new Exception($"[ConfigLoader] Duplicate config key '{attribute.Key}' found on {_type.Name}.{field.Name}. Keys must be unique.");
                        }

                        var entry = (IConfigEntry)Activator.CreateInstance(field.FieldType, attribute, _groupName);
                        field.SetValue(null, entry);
                        Settings.Add(entry);

                        if (GetParserForType(entry.ValueType) == null && !IsPrimitiveOrEnum(entry.ValueType))
                        {
                            Debug.LogError($"[ConfigLoader] Error: The type '{entry.ValueType.Name}' for setting '{_groupName}.{field.Name}' is not supported. " +
                                           $"To add support, create a class that implements IConfigValueParser and register it with ConfigLoader.RegisterParser().");
                        }
                    }
                }
            }

            var nestedTypes = _type.GetNestedTypes(BindingFlags.Public | BindingFlags.Static);
            foreach (var nestedType in nestedTypes)
            {
                DiscoverSettingsInType(nestedType, $"{_groupName}.{nestedType.Name}");
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Determines whether the specified type is a primitive type, a string, or an enum.
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

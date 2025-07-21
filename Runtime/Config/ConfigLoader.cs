// =================================================================================
// File: ConfigLoader.cs
// Author: MizoreRainy
// Description: The core engine for loading, parsing, and saving configuration.
//              Supports both INI (.txt) and YAML (.yaml) formats conditionally.
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

        /// <summary>
        /// A flag to prevent re-entrant calls to the file-changed event handler, which can cause infinite loops.
        /// </summary>
        private static volatile bool _IsReloading;

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
                if (!(e is InvalidOperationException))
                {
                    Debug.LogError($"[ConfigLoader] Synchronous initialization failed: {e.Message}");
                }
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
                if (!(e is InvalidOperationException))
                {
                    Debug.LogError($"[ConfigLoader] Asynchronous initialization failed: {e.Message}");
                }
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
            return Parsers.FirstOrDefault(_p => _p.CanParse(_type));
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
        /// Asynchronously loads application settings from the configuration file.
        /// If the file does not exist, it creates a new configuration file with default values.
        /// Invalid or missing entries in the file are replaced with default values for the corresponding settings.
        /// </summary>
        /// <returns>
        /// A Task that represents the asynchronous operation of loading the configuration file.
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
        /// Synchronously saves all settings to the configuration file.
        /// Ensures that the configuration file is updated with the latest values.
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
        /// Saves all settings to the configuration file asynchronously.
        /// Ensures that the configuration file is updated with the latest values while handling I/O operations safely.
        /// </summary>
        /// <returns>
        /// A task representing the asynchronous save operation. The task completes once the write operation is finished.
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
        /// Resets all configuration settings to their default values as defined in the code
        /// and then saves these defaults to the config file, overwriting its current content.
        /// </summary>
        /// <returns>A task representing the asynchronous reset and save operation.</returns>
        public static async Task ResetToDefaultsAsync()
        {
            await InitializeAsync();
            Debug.Log("[ConfigLoader] Resetting all settings to their default values...");
            foreach (var setting in Settings)
            {
                setting.SetToDefault();
            }
            await SaveAsync();
            Debug.Log("[ConfigLoader] All settings have been reset to defaults and saved.");
        }

        #endregion

        #region INI Parsing
        private static void LoadFromIniSync(string _path)
        {
            var fileValues = new Dictionary<string, string>();
            try
            {
                string[] lines;
                lock (FileLock) { lines = File.ReadAllLines(_path); }
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.Trim().StartsWith("#")) continue;
                    var equalsIndex = line.IndexOf('=');
                    if (equalsIndex > 0)
                    {
                        fileValues[line.Substring(0, equalsIndex).Trim()] = line.Substring(equalsIndex + 1);
                    }
                }
            }
            catch (Exception e) { Debug.LogError($"[ConfigLoader] Failed to read INI config. Error: {e.Message}"); }

            foreach (var setting in Settings)
            {
                if (fileValues.TryGetValue(setting.Key, out var rawValue)) setting.SetValueFromString(rawValue);
                else setting.SetToDefault();
            }
        }
        // ReSharper disable once UnusedMember.Local
        private static Task LoadFromIniAsync(string _path) { LoadFromIniSync(_path); return Task.CompletedTask; }

        // ReSharper disable once UnusedMember.Local
        private static void SaveToIniSync(string _path)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Last saved: {DateTime.Now}");
            var groupedSettings = Settings.GroupBy(_s => _s.GroupName).OrderBy(_g => _g.Key);
            foreach (var group in groupedSettings)
            {
                sb.AppendLine($"\n#==================================================");
                sb.AppendLine($"# :: {group.Key} Settings");
                sb.AppendLine($"#==================================================");
                foreach (var setting in group.OrderBy(_s => _s.Key))
                {
                    if (!string.IsNullOrEmpty(setting.Description)) sb.AppendLine($"# {setting.Description}");
                    sb.AppendLine($"{setting.Key}={setting.GetValueAsString()}");
                }
            }
            try
            {
                var encodedText = Encoding.UTF8.GetBytes(sb.ToString());
                lock (FileLock) { File.WriteAllBytes(_path, encodedText); }
            }
            catch (Exception e) { Debug.LogError($"[ConfigLoader] Failed to save INI config! Error: {e.Message}"); }
        }
        // ReSharper disable once UnusedMember.Local
        private static async Task SaveToIniAsync(string _path)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Last saved: {DateTime.Now}");
            var groupedSettings = Settings.GroupBy(_s => _s.GroupName).OrderBy(_g => _g.Key);
            foreach (var group in groupedSettings)
            {
                sb.AppendLine($"\n#==================================================");
                sb.AppendLine($"# :: {group.Key} Settings");
                sb.AppendLine($"#==================================================");
                foreach (var setting in group.OrderBy(_s => _s.Key))
                {
                    if (!string.IsNullOrEmpty(setting.Description)) sb.AppendLine($"# {setting.Description}");
                    sb.AppendLine($"{setting.Key}={setting.GetValueAsString()}");
                }
            }
            try
            {
                var encodedText = Encoding.UTF8.GetBytes(sb.ToString());
                await Task.Run(() => { lock (FileLock) { File.WriteAllBytes(_path, encodedText); } });
            }
            catch (Exception e) { Debug.LogError($"[ConfigLoader] Failed to save INI config! Error: {e.Message}"); }
        }
        #endregion

        #region YAML Parsing
#if HAVE_VYAML
        private static void LoadFromYamlSync(string _path)
        {
            Dictionary<string, object> yamlData;
            try
            {
                byte[] yamlBytes;
                lock (FileLock) { yamlBytes = File.ReadAllBytes(_path); }
                yamlData = YamlSerializer.Deserialize<Dictionary<string, object>>(yamlBytes) ?? new Dictionary<string, object>();
            }
            catch (Exception e) { Debug.LogError($"[ConfigLoader] Failed to read YAML config. Error: {e.Message}"); yamlData = new Dictionary<string, object>(); }

            foreach (var setting in Settings)
            {
                // A more complex implementation could handle nesting based on GroupName
                if (yamlData.TryGetValue(setting.Key, out var value)) setting.SetValueFromString(value?.ToString() ?? "");
                else setting.SetToDefault();
            }
        }
        private static Task LoadFromYamlAsync(string _path) { LoadFromYamlSync(_path); return Task.CompletedTask; }

        private static void SaveToYamlSync(string _path)
        {
            var data = new Dictionary<string, object>();
            foreach (var setting in Settings)
            {
                // A more complex implementation could build a nested dictionary based on GroupName
                data[setting.Key] = setting.GetType().GetProperty("Value")?.GetValue(setting);
            }
            try
            {
                var yamlBytes = YamlSerializer.Serialize(data).ToArray();
                lock (FileLock) { File.WriteAllBytes(_path, yamlBytes); }
            }
            catch (Exception e) { Debug.LogError($"[ConfigLoader] Failed to save YAML config! Error: {e.Message}"); }
        }
        private static async Task SaveToYamlAsync(string _path)
        {
            var data = new Dictionary<string, object>();
            foreach (var setting in Settings)
            {
                data[setting.Key] = setting.GetType().GetProperty("Value")?.GetValue(setting);
            }
            try
            {
                var yamlBytes = YamlSerializer.Serialize(data).ToArray();
                await Task.Run(() => { lock (FileLock) { File.WriteAllBytes(_path, yamlBytes); } });
            }
            catch (Exception e) { Debug.LogError($"[ConfigLoader] Failed to save YAML config! Error: {e.Message}"); }
        }
#endif
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
            Debug.Log("[ConfigLoader] Stopped watching config file.");
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
                                    $"[ConfigLoader] Initialization failed due to an invalid default value in a [Config] attribute.\n\n" +
                                    $"<b>Setting:</b>\t<color=white>{_groupName}.{field.Name}</color>\n" +
                                    $"<b>Error:</b>\t\tThe provided default value has the wrong type.\n" +
                                    $"<b>Details:</b>\t{innerEx.Message.Split(new[] { '\r', '\n' })[0]}\n" +
                                    $"<b>Parameter:</b>\t<color=#FF6666>{argEx.ParamName}</color>";

                                Debug.LogError(richMessage);
                                throw new InvalidOperationException($"Initialization failed for setting '{_groupName}.{field.Name}'. Please check the console for details.", innerEx);
                            }
                            throw innerEx ?? ex;
                        }

                        if (GetParserForType(field.FieldType.GetGenericArguments()[0]) == null && !IsPrimitiveOrEnum(field.FieldType.GetGenericArguments()[0]))
                        {
                            Debug.LogError($"[ConfigLoader] Error: The type '{field.FieldType.GetGenericArguments()[0].Name}' for setting '{_groupName}.{field.Name}' is not supported. " +
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

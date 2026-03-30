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
	public static partial class ConfigLoader
	{
		#region Fields & Properties

		/// <summary>
		///     Gets or sets the active registry holding configuration settings and parsers.
		///     Can be swapped for unit testing isolated environments.
		/// </summary>
		public static ConfigRegistry Registry { get; set; } = new ConfigRegistry();

		// Indicates whether the ConfigLoader has completed its initialization process.
		private static bool _IsInitialized;

		// Tracks whether the configuration system is currently undergoing initialization.
		private static bool _IsInitializing;

		// Tracks the task representing the asynchronous initialization process.
		private static Task _InitializationTask;

		// Serves as a synchronization mechanism for thread-safe initialization.
		private static readonly object InitializationLock = new();

		// Holds the dynamically assigned file path to the application's configuration file.
		private static string _ConfigFilePath;

		// A file system watcher that observes changes to the configuration file during runtime.
		private static FileSystemWatcher _Watcher;

		// Serves as a synchronization object to manage concurrent access to file operations.
		private static readonly object FileLock = new();

		// Holds a reference to the synchronization context of the main thread.
		private static SynchronizationContext _MainThreadContext;

		// Denotes whether the configuration system is in the process of being reloaded.
		private static volatile bool _IsReloading;

		#endregion

	#region Unity Lifecycle & Initialization

#if CONFIG_LOADER_AUTO_INIT
		// Handles automatic initialization of the configuration system before the first scene loads.
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
		///     Initializes the configuration system synchronously.
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
				if (Registry.Settings.Count == 0)
				{
					PandoraLogger.LogConfig("No configuration settings discovered. Bypassing IO and Watcher initialization.");
					lock (InitializationLock)
					{
						_IsInitialized = true;
						_IsInitializing = false;
					}
					return;
				}

				LoadFromFileSync();

				lock (InitializationLock)
				{
					_IsInitialized = true;
					_IsInitializing = false;
				}

				PandoraLogger.LogConfig($"Synchronous initialization complete. {Registry.Settings.Count} settings loaded.");

				// Automatically start watching for changes in supported environments.
#if (UNITY_EDITOR || UNITY_STANDALONE) && !PANDORA_DISABLE_CONFIG_WATCHER
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
				if (Registry.Settings.Count == 0)
				{
					PandoraLogger.LogConfig("No configuration settings discovered. Bypassing IO and Watcher initialization.");
					lock (InitializationLock)
					{
						_IsInitialized = true;
						_IsInitializing = false;
					}
					return;
				}

				await LoadFromFileAsync();

				lock (InitializationLock)
				{
					_IsInitialized = true;
					_IsInitializing = false;
				}

				PandoraLogger.LogConfig($"Asynchronous initialization complete. {Registry.Settings.Count} settings loaded.");

				// Automatically start watching for changes in supported environments.
#if (UNITY_EDITOR || UNITY_STANDALONE) && !PANDORA_DISABLE_CONFIG_WATCHER
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
		///     Gets a value indicating whether the configuration system has been successfully initialized.
		///     Returns true if the initialization process, involving configuration discovery,
		///     loading, and runtime readiness, has been completed.
		///     Otherwise, returns false.
		/// </summary>
		public static bool IsInitialized => _IsInitialized;

		#endregion

		#region Public API

		/// <summary>
		///     Gets the absolute file path of the configuration file used by the system.
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


		#endregion





		#region Internal & Interface Implementations

		private static void DiscoverSettings()
		{
			Registry.Settings.Clear();
			Registry.GroupOrders.Clear();
			var processedTypes = new HashSet<Type>();

#if UNITY_EDITOR
			// Fast path for Editor using Unity's TypeCache (Zero reflection cost over assemblies)
			var fields = UnityEditor.TypeCache.GetFieldsWithAttribute<ConfigAttribute>();
			foreach (var field in fields)
			{
				var rootType = GetTopmostStaticDeclaringType(field.DeclaringType);
				if (rootType != null && !processedTypes.Contains(rootType))
				{
					if (rootType.IsClass && rootType.IsSealed && rootType.IsAbstract)
						DiscoverSettingsInType(rootType, rootType.Name, processedTypes);
				}
			}
#else
			// Fast path for runtime using pre-baked ScriptableObject
			var cache = Resources.Load<ConfigTypeCacheSO>("PandoraConfigCache");
			if (cache != null && cache.ConfigTypes != null && cache.ConfigTypes.Count > 0)
			{
				foreach (var typeName in cache.ConfigTypes)
				{
					var type = Type.GetType(typeName);
					if (type != null && !processedTypes.Contains(type))
					{
						if (type.IsClass && type.IsSealed && type.IsAbstract)
							DiscoverSettingsInType(type, type.Name, processedTypes);
					}
				}
			}
			else
			{
#if CONFIG_ALLOW_REFLECTION
				// Fallback to full assembly scan (slow path)
				PandoraLogger.LogConfigWarning("PandoraConfigCache not found! Falling back to slow assembly scan. Please run the Pandora Config Preprocessor before building.");
				var assemblies = AppDomain.CurrentDomain.GetAssemblies();
				foreach (var assembly in assemblies)
				{
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
					catch (ReflectionTypeLoadException) { }
				}
#else
				PandoraLogger.LogConfigWarning("PandoraConfigCache not found! Skipping config discovery. Ensure you added Configs and ran the Cache Generator.");
#endif
			}
#endif
			
			// Sort settings by initialization and display order using a stable sort
			var sortedSettings = System.Linq.Enumerable.ToList(System.Linq.Enumerable.OrderBy(Registry.Settings, a => a.Order));
			Registry.Settings.Clear();
			Registry.Settings.AddRange(sortedSettings);
		}

		private static Type GetTopmostStaticDeclaringType(Type type)
		{
			if (type == null) return null;
			Type topmostStatic = type.IsClass && type.IsSealed && type.IsAbstract ? type : null;

			Type current = type.DeclaringType;
			while (current != null)
			{
				if (current.IsClass && current.IsSealed && current.IsAbstract)
					topmostStatic = current;
				else
					break;

				current = current.DeclaringType;
			}
			return topmostStatic;
		}

		// Scans the specified type for static, readonly fields that implement the IConfigEntry interface and
		// performs discovery of configuration entries.
		// Handles recursive processing of nested types and groups the discovered settings under the provided group name.
		// Ensures each type is processed only once using the collection of processed types.
		private static void DiscoverSettingsInType(Type _type, string _groupName, ISet<Type> _processedTypes)
		{
			_processedTypes.Add(_type); // Mark this type as processed

			var groupAttr = _type.GetCustomAttribute<ConfigGroupAttribute>();
			if (groupAttr != null)
				Registry.GroupOrders[_groupName] = groupAttr.Order;

			var fields = _type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
			foreach (var field in fields)
				if (field.IsInitOnly && typeof(IConfigEntry).IsAssignableFrom(field.FieldType))
				{
					var attribute = field.GetCustomAttribute<ConfigAttribute>();
					if (attribute != null)
					{
						var existingSetting = Registry.Settings.FirstOrDefault(_s => _s.Key == attribute.Key);
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
							Registry.Settings.Add(entry);
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



		// Determines whether the specified type is a primitive type, a string, or an enum.
		private static bool IsPrimitiveOrEnum(Type _type)
		{
			return _type.IsPrimitive || _type == typeof(string) || _type.IsEnum;
		}


	}
}
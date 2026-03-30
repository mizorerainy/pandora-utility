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

		// Synchronously loads configuration settings from a file located on the disk.
		// If the configuration file is not found, a new file is automatically created containing the default settings.
		// Invalid or missing entries in the configuration file are replaced with default values.
		// Ensures consistency by saving the updated configuration back to the file after loading.
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
		///     Loads application settings from the configuration file asynchronously.
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

		// Synchronously saves all configuration settings to the designated file.
		// Temporarily halts the file watcher to prevent triggering loops during the save process.
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
			foreach (var setting in Registry.Settings) setting.SetToDefault();

			// Now, save these default values back to the file.
			await SaveAsync();
			PandoraLogger.LogConfig("All settings have been reset to defaults and saved.");
		}

		#endregion

		#region Initialization

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

		// Handles the event triggered when the configuration file is changed.
		// Ensures the updated configuration file is processed, reloading settings to maintain consistency.
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

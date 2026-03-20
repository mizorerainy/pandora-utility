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

		#region INI Processing
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
		#endregion
	}
}

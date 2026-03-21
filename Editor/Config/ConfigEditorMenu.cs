using MizoreRainy.Pandora;
// =================================================================================
// File: ConfigEditorMenu.cs
// Author: MizoreRainy
// Description: Adds a menu item to the Unity Editor to quickly open the config file.
//              This file must be placed in an "Editor" folder.
// =================================================================================

#if UNITY_EDITOR

using MizoreRainy.Pandora.ConfigUtility;
using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace MizoreRainy.Pandora.Editor.ConfigUtility.Editor
{
	public static class ConfigEditorMenu
	{
		#region Editor Menu Items

		/// <summary>
		/// Opens the config.ini file in the default external script editor.
		/// </summary>
		[MenuItem("Pandora/Config/Open Config File", priority = 110)]
		private static void OpenConfigFile()
		{
			// Get the path from the same ConfigLoader logic.
			var path = ConfigLoader.GetConfigPath();

			// This ensures the file is created if it doesn't exist yet.
			if (!System.IO.File.Exists(path))
			{
				ConfigLoader.EnsureInitialized();
			}

			// This opens the file in the code editor set in Unity's preferences.
			PandoraLogger.LogConfig($"Opening config file at '{path}'...");
			UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(path, 1);
		}

		/// <summary>
		/// Forces a reload of all settings from the config.ini file.
		/// </summary>
		[MenuItem("Pandora/Config/Reload Settings from File", priority = 111)]
		private static async void ReloadSettingsFromFile()
		{
			ConfigLoader.EnsureInitialized();
			PandoraLogger.LogConfig("Forcing a reload from config.ini...");
			await ConfigLoader.LoadFromFileAsync();
			PandoraLogger.LogConfig("Reload complete.");
		}

		/// <summary>
		/// Resets the config.ini file to the default values defined in the code.
		/// </summary>
		[MenuItem("Pandora/Config/Reset Config to Defaults", priority = 112)]
		private static async void ResetConfigToDefaults()
		{
			ConfigLoader.EnsureInitialized();
			if (EditorUtility.DisplayDialog("Reset Configuration to Defaults",
				"This will overwrite 'config.ini' with the default values defined in your code.\n\nThis action cannot be undone.",
				"Reset and Overwrite", "Cancel"))
			{
				PandoraLogger.LogConfig("Resetting all settings to default values...");
				await ConfigLoader.ResetToDefaultsAsync();
				PandoraLogger.LogConfig("Reset complete. config.ini has been updated.");
			}
		}

		#endregion
	}
}

#endif

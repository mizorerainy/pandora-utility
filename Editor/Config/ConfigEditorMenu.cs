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
		/// <summary>
		/// Opens the config.txt file in the default external script editor.
		/// </summary>
		[MenuItem("Pandora/Config/Open Config File")]
		private static void OpenConfigFile()
		{
			// Get the path from the same ConfigLoader logic.
			string path = ConfigLoader.GetConfigPath();

			// This ensures the file is created if it doesn't exist yet.
			if (!System.IO.File.Exists(path))
			{
				ConfigLoader.EnsureInitialized();
			}

			// This opens the file in the code editor set in Unity's preferences.
			CodeEditor.CurrentEditor.OpenProject(path, 1);
		}

		/// <summary>
        /// Forces a reload of all settings from the config.txt file.
        /// </summary>
        [MenuItem("Pandora/Config/Reload Settings from File")]
        private static void ReloadSettingsFromFile()
        {
            ConfigLoader.EnsureInitialized();
            Debug.Log("[ConfigLoader] Forcing a reload from config.txt...");
            ConfigLoader.LoadFromFileAsync().GetAwaiter().GetResult();
            Debug.Log("[ConfigLoader] Reload complete.");
        }

        /// <summary>
        /// Resets the config.txt file to the default values defined in the code.
        /// </summary>
        [MenuItem("Pandora/Config/Reset Config to Defaults")]
        private static void ResetConfigToDefaults()
        {
            ConfigLoader.EnsureInitialized();
            if (EditorUtility.DisplayDialog("Reset Configuration to Defaults",
                "This will overwrite 'config.txt' with the default values defined in your code.\n\nThis action cannot be undone.",
                "Reset and Overwrite", "Cancel"))
            {
                Debug.Log("[ConfigLoader] Resetting all settings to default values...");
                ConfigLoader.ResetToDefaultsAsync().GetAwaiter().GetResult();
                Debug.Log("[ConfigLoader] Reset complete. config.txt has been updated.");
            }
        }
	}
}

#endif

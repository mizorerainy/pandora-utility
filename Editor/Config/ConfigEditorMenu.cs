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
		[MenuItem("Pandora/Open Config File")]
		private static void OpenConfigFile()
		{
			// Get the path from the same ConfigLoader logic.
			string path = ConfigLoader.GetConfigPath();

			// This ensures the file is created if it doesn't exist yet.
			if (!System.IO.File.Exists(path))
			{
				// Safer approach: get the task once and wait for it
				var initTask = ConfigLoader.InitializeAsync();
				if (initTask.Status != System.Threading.Tasks.TaskStatus.RanToCompletion)
				{
					try
					{
						initTask.GetAwaiter().GetResult();
					}
					catch (System.Exception e)
					{
						Debug.LogError($"Failed to initialize ConfigLoader: {e.Message}");
						return;
					}
				}
			}

			// This opens the file in the code editor set in Unity's preferences.
			CodeEditor.CurrentEditor.OpenProject(path, 1);

		}
	}
}

#endif
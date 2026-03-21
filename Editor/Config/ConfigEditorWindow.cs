// =================================================================================
// File: ConfigEditorWindow.cs
// Author: MizoreRainy
// Description: A Unity Editor window to visually edit all discovered configuration settings.
//              This file must be placed in an "Editor" folder.
// =================================================================================

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using MizoreRainy.Pandora.ConfigUtility;
using MizoreRainy.Pandora.ConfigUtility.Editor;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace MizoreRainy.Pandora.Editor.ConfigUtility.Editor
{
	/// <summary>
	///     Provides a dedicated Unity Editor window for viewing, editing, and managing Pandora configuration settings.
	/// </summary>
	public partial class ConfigEditorWindow : EditorWindow
	{
		#region Fields

		/// <summary>
		///     Represents the root node in the hierarchical tree of configuration settings
		///     displayed in the Config Editor Window.
		///     This node serves as the entry point
		///     for managing and organizing configuration settings, containing child nodes
		///     and settings entries.
		/// </summary>
		private ConfigNode _RootNode;

		/// <summary>
		///     Stores the current scroll position within the configuration editor window.
		///     This variable is used to track the position of the scroll view when rendering
		///     configuration nodes and settings in the Unity Editor.
		///     It allows the user to
		///     maintain their position within the editor interface while navigating or editing
		///     configuration data.
		/// </summary>
		private Vector2 _ScrollPosition;

		/// <summary>
		///     Indicates whether any field within the configuration editor is invalid.
		/// </summary>
		/// <remarks>
		///     This boolean variable is used
		///     to track the validation state of the fields displayed in the configuration editor.
		///     It is set to <c>true</c> if one or more fields fail validation checks,
		///     and <c>false</c> when all fields are valid.
		///     The value is updated dynamically during the rendering process and through validation methods.
		/// </remarks>
		private bool _IsAnyFieldInvalid;

		/// <summary>
		///     Stores the current search query to filter the configuration settings.
		/// </summary>
		private string _SearchQuery = "";

		private enum SearchScope { Key, Value, Both }
		private SearchScope _SearchScope = SearchScope.Both;

		private Vector2 _GroupScrollPosition;
		private string _SelectedNodePath = "";
		private string _ScrollToTarget = null;
		private Dictionary<string, float> _GroupYPositions = new Dictionary<string, float>();
		private string _HighlightTargetPath = null;
		private double _HighlightStartTime = 0;
		private int _GroupRenderIndex = 0;
		private bool _ShowDescriptions = false;
		private Dictionary<string, UnityEditorInternal.ReorderableList> _ArrayLists = new Dictionary<string, UnityEditorInternal.ReorderableList>();
		private double _SaveHoldStartTime = 0;
		private double _DiscardHoldStartTime = 0;
		private double _SaveSuccessTime = 0;
		private double _DiscardSuccessTime = 0;

		#endregion

		#region Utility Methods

		private ConfigNode FindNodeByPath(string path, ConfigNode root)
		{
			if (string.IsNullOrEmpty(path)) return root;
			var parts = path.Split('.');
			var current = root;
			foreach (var part in parts)
			{
				if (current.Children.TryGetValue(part, out var child))
				{
					current = child;
				}
				else
				{
					return null;
				}
			}
			return current;
		}

		#endregion

		#region Public API

		/// <summary>
		///     Displays the Config Editor Window for modifying and managing configuration settings in the Unity Editor.
		/// </summary>
		[MenuItem("Pandora/Config/Edit Configuration", priority = 100)]
		public static void ShowWindow()
		{
			GetWindow<ConfigEditorWindow>("Pandora Config Editor");
		}

		#endregion

		#region Unity Lifecycle

		/// <summary>
		///     Called when the editor window is enabled.
		///     Initializes or refreshes the configuration settings to ensure
		///     that the editor displays the most up-to-date information.
		/// </summary>
		private void OnEnable()
		{
			RefreshSettings();
		}

		#endregion

		#region Domain Logic

		/// <summary>
		///     Reloads and reorganizes the configuration settings into a hierarchical structure.
		///     This process retrieves the current configuration state and updates the visual representation
		///     within the editor.
		///     If the configuration information cannot be found or loaded, an error
		///     message is logged and the settings display is disabled.
		/// </summary>
		private void RefreshSettings()
		{
			ConfigLoader.EnsureInitialized();
			_ArrayLists.Clear();
			var settings = ConfigLoader.Registry.Settings;
			if (settings == null)
			{
				_RootNode = null;
				Debug.LogError(
					"[ConfigEditorWindow] Could not find settings in ConfigLoader.Registry. Cannot display settings.");
				return;
			}
			_RootNode = new ConfigNode { Name = "Root" };

			// Build the tree structure from the flat list of settings
			foreach (var setting in settings)
			{
				var pathParts = setting.GroupName.Split('.');
				var currentNode = _RootNode;

				foreach (var part in pathParts)
				{
					if (!currentNode.Children.ContainsKey(part))
						currentNode.Children[part] = new ConfigNode { Name = part };
					currentNode = currentNode.Children[part];
				}

				var currentValue = setting.GetType().GetProperty("Value")?.GetValue(setting);
				
				bool hasCustomColor = false;
				Color customColor = Color.clear;
				if (!string.IsNullOrEmpty(setting.BackgroundColorHex) && ColorUtility.TryParseHtmlString(setting.BackgroundColorHex, out var parsedColor))
				{
					hasCustomColor = true;
					customColor = parsedColor;
				}

				currentNode.Settings.Add(new DisplaySetting
				{
					Entry = setting,
					CurrentValue = currentValue,
					RawValue = currentValue?.ToString() ?? "",
					HasCustomColor = hasCustomColor,
					CustomColor = customColor
				});
			}

			ValidateAllFields();
		}


		/// <summary>
		///     Saves the current configuration changes asynchronously
		///     and displays a success notification upon completion.
		/// </summary>
		/// <remarks>
		///     This method clears the current GUI control focus and attempts to save the updated configuration.
		///     Any exceptions during the saving process are silently ignored.
		/// </remarks>
		private async void SaveChangesAndNotify()
		{
			try
			{
				GUI.FocusControl(null);
				await ConfigLoader.SaveAsync();
			}
			catch (Exception)
			{
				/* Ignored */
			}
		}

		/// <summary>
		///     Resets all configuration settings to their default values
		///     and updates the editor UI to reflect the changes.
		///     Displays a notification dialog upon successful execution.
		/// </summary>
		private async void ResetToDefaultsAndNotify()
		{
			try
			{
				GUI.FocusControl(null);
				await ConfigLoader.ResetToDefaultsAsync();
				RefreshSettings();
				Repaint();
				EditorUtility.DisplayDialog("Success", "Configuration has been reset to defaults.", "OK");
			}
			catch (Exception)
			{
				/* Ignored */
			}
		}

		/// <summary>
		///     Discards all unsaved configuration changes in the editor,
		///     reloads the settings directly from the configuration file,
		///     and updates the editor UI to reflect the restored values.
		/// </summary>
		private async void DiscardChangesAndNotify()
		{
			try
			{
				GUI.FocusControl(null);
				// Awaiting LoadFromFileAsync effectively overwrites current in-memory settings.
				await ConfigLoader.LoadFromFileAsync();
				RefreshSettings();
				Repaint();
			}
			catch (Exception e)
			{
				Debug.LogError($"Failed to discard changes: {e.Message}");
			}
		}

		private void OpenScriptForSetting(string key)
		{
			string[] scriptGuids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" });
			string searchString = $"\"{key}\"";

			foreach (string guid in scriptGuids)
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				string[] lines = File.ReadAllLines(path);
				for (int i = 0; i < lines.Length; i++)
				{
					if (lines[i].Contains(searchString) && (lines[i].Contains("Config") || lines[i].Contains("ConfigAttribute")))
					{
						var scriptAsset = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
						AssetDatabase.OpenAsset(scriptAsset, i + 1);
						return;
					}
				}
			}
			Debug.LogWarning($"Could not find script definition for config key: {key}. It may use a dynamic key or constant.");
		}

		#endregion
	}
}

#endif
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
	public class ConfigEditorWindow : EditorWindow
	{
		#region Private Classes

		/// <summary>
		///     Represents the display settings for an individual configuration entry,
		///     including its validation state, current value, and associated metadata.
		/// </summary>
		private class DisplaySetting
		{
			/// <summary>
			///     Represents an implementation of the IConfigEntry interface, used within the
			///     ConfigEditorWindow for user-defined configuration management.
			/// </summary>
			/// <remarks>
			///     This variable provides access to the underlying configuration entry object,
			///     encapsulated by the IConfigEntry interface.
			///     It includes details about the
			///     entry's key, description, group, value type, and functionality for accessing
			///     and modifying its value.
			///     The Entry is critical for managing individual
			///     configuration items inside the editor.
			/// </remarks>
			public IConfigEntry Entry;

			/// <summary>
			///     Represents the current value of a configuration entry displayed within the ConfigEditorWindow.
			///     This value is used to reflect the state of the entry during the configuration process
			///     and may be modified through user interaction in the editor interface.
			///     The type of this value is dynamically determined based on the associated configuration entry.
			/// </summary>
			public object CurrentValue;

			/// <summary>
			///     Holds the current raw string value of a configuration entry as entered in the text field within the editor UI.
			///     This value is typically validated and parsed to derive the final value for the configuration entry.
			/// </summary>
			public string RawValue; // Holds the text field's current string, used for validation

			/// <summary>
			///     Indicates whether the current value of the setting is valid.
			///     If set to false, it usually means the input does not pass validation checks,
			///     and an error message is provided in the <c>ErrorMessage</c> property.
			/// </summary>
			public bool IsValid = true;

			/// <summary>
			///     Stores the error message associated with the validation status of a configuration setting.
			/// </summary>
			/// <remarks>
			///     The error message is displayed when the associated configuration value is deemed invalid.
			///     For example, this could contain messages such as "Value must be a valid integer" or
			///     "Value must be a valid floating-point number,"
			///     depending on the type of validation failure.
			/// </remarks>
			public string ErrorMessage = "";
		}

		/// <summary>
		///     Represents a node in the hierarchical configuration tree,
		///     allowing organization and storage of nested settings.
		///     Each node can contain a list of settings and a dictionary of child nodes.
		/// </summary>
		private class ConfigNode
		{
			/// <summary>
			///     The name of the configuration node.
			///     It serves as an identifier for the node within the hierarchical
			///     tree of settings, differentiating it from other nodes at the same level.
			/// </summary>
			public string Name;

			/// <summary>
			///     Indicates whether the foldout (expand/collapse)
			///     state of a configuration node in the hierarchy is expanded.
			///     This variable is used
			///     to track and manage the visibility of nested configuration elements within the Unity Editor window.
			/// </summary>
			public bool IsFoldout = true;

			/// <summary>
			///     Represents a collection of DisplaySettings associated with configuration nodes
			///     in the ConfigEditorWindow.
			///     These settings are used to manage and display
			///     hierarchical configuration data within the editor.
			/// </summary>
			public readonly List<DisplaySetting> Settings = new();

			/// <summary>
			///     A dictionary representing the child nodes of the current configuration node.
			///     Each key is the name of a child node, and the value is the associated <see cref="ConfigNode" />.
			///     This allows the configuration settings to be organized hierarchically.
			/// </summary>
			public readonly Dictionary<string, ConfigNode> Children = new();

			/// <summary>
			///     Checks if this node, its settings, or any of its children match the given search query.
			/// </summary>
			public bool MatchesSearch(string query, SearchScope scope)
			{
				if (string.IsNullOrEmpty(query)) return true;

				if (Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;

				foreach (var s in Settings)
				{
					bool matchKey = scope == SearchScope.Key || scope == SearchScope.Both;
					bool matchValue = scope == SearchScope.Value || scope == SearchScope.Both;

					if (matchKey)
					{
						if (s.Entry.Key.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;
						if (s.Entry.Description != null && s.Entry.Description.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;
					}

					if (matchValue)
					{
						if (s.RawValue != null && s.RawValue.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;
					}
				}

				foreach (var child in Children.Values)
				{
					if (child.MatchesSearch(query, scope)) return true;
				}

				return false;
			}
		}

		#endregion

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

		/// <summary>
		///     Displays the Config Editor Window for modifying and managing configuration settings in the Unity Editor.
		/// </summary>
		[MenuItem("Pandora/Config/Edit Configuration")]
		public static void ShowWindow()
		{
			GetWindow<ConfigEditorWindow>("Pandora Config Editor");
		}

		/// <summary>
		///     Called when the editor window is enabled.
		///     Initializes or refreshes the configuration settings to ensure
		///     that the editor displays the most up-to-date information.
		/// </summary>
		private void OnEnable()
		{
			RefreshSettings();
		}

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
			var settingsField = typeof(ConfigLoader).GetField("Settings", BindingFlags.NonPublic | BindingFlags.Static);
			if (settingsField == null)
			{
				_RootNode = null;
				Debug.LogError(
					"[ConfigEditorWindow] Could not find 'Settings' field in ConfigLoader. Cannot display settings.");
				return;
			}

			var settings = (List<IConfigEntry>)settingsField.GetValue(null);
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
				currentNode.Settings.Add(new DisplaySetting
				{
					Entry = setting,
					CurrentValue = currentValue,
					RawValue = currentValue?.ToString() ?? ""
				});
			}

			ValidateAllFields();
		}

		/// <summary>
		///     Handles the rendering and layout of the GUI for the configuration editor window.
		///     Includes functionality for refreshing settings, navigating the configuration hierarchy,
		///     and performing actions such as saving changes or resetting to defaults.
		/// </summary>
		private void OnGUI()
		{
			if (_RootNode == null)
			{
				EditorGUILayout.HelpBox("Could not load settings. Please check the console for errors.",
					MessageType.Error);
				if (GUILayout.Button("Retry")) RefreshSettings();
				return;
			}

			bool isNarrow = position.width < 500;

			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
			GUILayout.Label(EditorGUIUtility.IconContent("Search Icon"), GUIStyle.none, GUILayout.Width(20), GUILayout.Height(20));
			if (!isNarrow)
			{
				_SearchScope = (SearchScope)EditorGUILayout.EnumPopup(_SearchScope, EditorStyles.toolbarDropDown, GUILayout.Width(60));
			}
			_SearchQuery = EditorGUILayout.TextField("", _SearchQuery, GUI.skin.FindStyle("ToolbarSeachTextField") ?? EditorStyles.toolbarTextField, GUILayout.ExpandWidth(true), GUILayout.MinWidth(50));
			if (GUILayout.Button("x", EditorStyles.toolbarButton, GUILayout.Width(20)))
			{
				_SearchQuery = "";
				GUI.FocusControl(null);
			}

			GUILayout.Space(10);
			string descText = isNarrow ? "Desc" : "Show Descriptions";
			_ShowDescriptions = GUILayout.Toggle(_ShowDescriptions, descText, EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));
			if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false))) RefreshSettings();
			EditorGUILayout.EndHorizontal();

			EditorGUI.BeginDisabledGroup(EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode);

			if (EditorApplication.isCompiling)
			{
				EditorGUILayout.HelpBox("Compiling... Configuration editing is disabled.", MessageType.Warning);
			}

			EditorGUILayout.BeginHorizontal();

			// LEFT PANE (Dynamic width based on window size, max 250, min 120)
			float leftPaneWidth = Mathf.Clamp(position.width * 0.3f, 120f, 250f);
			EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(leftPaneWidth));
			_GroupScrollPosition = EditorGUILayout.BeginScrollView(_GroupScrollPosition, GUILayout.ExpandHeight(true));

			Action<ConfigNode, int, string> drawSidebarNodeRecursive = null;
			drawSidebarNodeRecursive = (node, indentLevel, parentPath) =>
			{
				string fullPath = string.IsNullOrEmpty(parentPath) ? node.Name : parentPath + "." + node.Name;

				bool isSelected = _SelectedNodePath == fullPath;
				var oldColor = GUI.backgroundColor;
				if (isSelected) GUI.backgroundColor = new Color(0.2f, 0.4f, 0.8f, 1f);

				EditorGUILayout.BeginHorizontal(GUI.skin.box);
				GUILayout.Space(indentLevel * 12); // Indent

				bool hasChildren = node.Children.Count > 0;

				GUIStyle style = new GUIStyle(EditorStyles.label);
				if (isSelected) style.normal.textColor = Color.white;

				if (hasChildren)
				{
					string foldoutIcon = node.IsFoldout ? "▼" : "▶";
					if (GUILayout.Button(foldoutIcon, style, GUILayout.Width(15)))
					{
						node.IsFoldout = !node.IsFoldout;
					}
				}
				else
				{
					GUILayout.Space(15);
				}

				if (GUILayout.Button(node.Name, style))
				{
					_SelectedNodePath = fullPath;
					_ScrollToTarget = fullPath;
					_HighlightTargetPath = fullPath;
					_HighlightStartTime = EditorApplication.timeSinceStartup;
					GUI.FocusControl(null);
				}
				EditorGUILayout.EndHorizontal();
				GUI.backgroundColor = oldColor;

				if (hasChildren && node.IsFoldout)
				{
					foreach (var child in node.Children.Values.OrderBy(n => n.Name))
					{
						drawSidebarNodeRecursive(child, indentLevel + 1, fullPath);
					}
				}
			};

			// Draw "All Settings"
			var oldBgAll = GUI.backgroundColor;
			if (string.IsNullOrEmpty(_SelectedNodePath)) GUI.backgroundColor = new Color(0.2f, 0.4f, 0.8f, 1f);
			EditorGUILayout.BeginHorizontal(GUI.skin.box);
			GUIStyle styleAll = new GUIStyle(EditorStyles.label);
			if (string.IsNullOrEmpty(_SelectedNodePath)) styleAll.normal.textColor = Color.white;
			if (GUILayout.Button("   All Settings", styleAll))
			{
				_SelectedNodePath = "";
				_ScrollToTarget = "";
				GUI.FocusControl(null);
			}
			EditorGUILayout.EndHorizontal();
			GUI.backgroundColor = oldBgAll;

			foreach (var grp in _RootNode.Children.Values.OrderBy(n => n.Name))
			{
				drawSidebarNodeRecursive(grp, 0, "");
			}
			EditorGUILayout.EndScrollView();
			EditorGUILayout.EndVertical();

			// RIGHT PANE
			EditorGUILayout.BeginVertical();
			EditorGUILayout.Space(2); // small top margin

			if (Event.current.type == EventType.Layout && _ScrollToTarget != null)
			{
				if (string.IsNullOrEmpty(_ScrollToTarget))
				{
					_ScrollPosition.y = 0;
					_ScrollToTarget = null;
				}
				else if (_GroupYPositions.TryGetValue(_ScrollToTarget, out float targetY))
				{
					_ScrollPosition.y = targetY;
					_ScrollToTarget = null;
				}
			}

			_ScrollPosition = EditorGUILayout.BeginScrollView(_ScrollPosition);

			_IsAnyFieldInvalid = false; // Reset before redraw
			_GroupRenderIndex = 0;

			foreach (var node in _RootNode.Children.Values.OrderBy(_n => _n.Name))
			{
				if (string.IsNullOrEmpty(_SearchQuery) || node.MatchesSearch(_SearchQuery, _SearchScope))
				{
					DrawNode(node, _SearchQuery, _SearchScope, "");
				}
			}

			EditorGUILayout.EndScrollView();

			EditorGUILayout.BeginHorizontal();

			EditorGUI.BeginDisabledGroup(_IsAnyFieldInvalid);
			if (GUILayout.Button("Save Changes", GUILayout.Height(30))) SaveChangesAndNotify();
			EditorGUI.EndDisabledGroup();

			if (GUILayout.Button("Reset to Defaults", GUILayout.Height(30)))
			{
				var configFileName = Path.GetFileName(ConfigLoader.GetConfigPath());
				if (EditorUtility.DisplayDialog("Reset All to Defaults?",
						$"This will overwrite '{configFileName}' with the default values defined in your code.\n\nThis action cannot be undone.",
						"Reset", "Cancel")) ResetToDefaultsAndNotify();
			}

			EditorGUILayout.EndHorizontal();
			EditorGUILayout.EndVertical(); // End Right Pane
			EditorGUILayout.EndHorizontal(); // End Main Area
			EditorGUI.EndDisabledGroup();
		}

		/// <summary>
		///     Recursively draws a hierarchical configuration node,
		///     including its settings and child nodes, in the Unity editor window.
		/// </summary>
		/// <param name="_node">The configuration node to be drawn, including its settings and child nodes.</param>
		/// <param name="_searchQuery">The search query to filter settings and nodes.</param>
		private void DrawNode(ConfigNode _node, string _searchQuery = "", SearchScope _searchScope = SearchScope.Both, string currentPath = "")
		{
			string fullPath = string.IsNullOrEmpty(currentPath) ? _node.Name : currentPath + "." + _node.Name;

			bool isSearching = !string.IsNullOrEmpty(_searchQuery);
			bool nodeMatches = !isSearching || _node.Name.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0;

			EditorGUILayout.Space(10);

			var oldBg = GUI.backgroundColor;
			float tint = (_GroupRenderIndex % 2 == 0) ? 1.05f : 0.95f;
			Color baseColor = new Color(oldBg.r * tint, oldBg.g * tint, oldBg.b * tint, oldBg.a);

			if (_HighlightTargetPath == fullPath)
			{
				double elapsed = EditorApplication.timeSinceStartup - _HighlightStartTime;
				if (elapsed < 1.0)
				{
					float t = (float)elapsed; // 0 to 1
					var highlightColor = new Color(0.3f, 0.6f, 1.0f, 1f);
					baseColor = Color.Lerp(highlightColor, baseColor, t);
					Repaint();
				}
				else
				{
					_HighlightTargetPath = null;
				}
			}

			_GroupRenderIndex++;

			GUI.backgroundColor = baseColor;
			EditorGUILayout.BeginVertical(GUI.skin.box);
			GUI.backgroundColor = oldBg;

			var headerStyle = new GUIStyle(EditorStyles.largeLabel) { fontStyle = FontStyle.Bold };
			EditorGUILayout.LabelField(_node.Name, headerStyle);

			if (Event.current.type == EventType.Repaint)
			{
				_GroupYPositions[fullPath] = GUILayoutUtility.GetLastRect().y;
			}

			Rect r = EditorGUILayout.GetControlRect(false, 1);
			EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.3f));
			EditorGUILayout.Space(10);

			// Draw settings at this level
			foreach (var setting in _node.Settings.OrderBy(_s => _s.Entry.Key))
			{
				bool settingMatches = !isSearching || nodeMatches;

				if (!settingMatches)
				{
					bool matchKey = _searchScope == SearchScope.Key || _searchScope == SearchScope.Both;
					bool matchValue = _searchScope == SearchScope.Value || _searchScope == SearchScope.Both;

					if (matchKey && (setting.Entry.Key.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
									 (setting.Entry.Description != null && setting.Entry.Description.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0)))
					{
						settingMatches = true;
					}

					if (!settingMatches && matchValue && setting.RawValue != null && setting.RawValue.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0)
					{
						settingMatches = true;
					}
				}

				if (settingMatches)
				{
					DrawSetting(setting);
					if (!setting.IsValid) _IsAnyFieldInvalid = true;
				}
			}

			// Recursively draw child nodes
			foreach (var childNode in _node.Children.Values.OrderBy(_n => _n.Name))
			{
				if (!isSearching || nodeMatches || childNode.MatchesSearch(_searchQuery, _searchScope))
				{
					EditorGUI.indentLevel++;
					DrawNode(childNode, nodeMatches ? "" : _searchQuery, _searchScope, fullPath);
					EditorGUI.indentLevel--;
				}
			}

			EditorGUILayout.EndVertical();
		}

		/// <summary>
		///     Saves the current configuration changes asynchronously
		///     and displays a success notification upon completion.
		/// </summary>
		/// <remarks>
		///     This method clears the current GUI control focus and attempts to save the updated configuration.
		///     If the operation is successful, a dialog is displayed to confirm the save.
		///     Any exceptions during the saving process are silently ignored.
		/// </remarks>
		private async void SaveChangesAndNotify()
		{
			try
			{
				GUI.FocusControl(null);
				await ConfigLoader.SaveAsync();
				var configFileName = Path.GetFileName(ConfigLoader.GetConfigPath());
				EditorUtility.DisplayDialog("Success", $"Configuration saved successfully to {configFileName}.", "OK");
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
		///     Validates all fields within the configuration hierarchy
		///     by checking each node and its associated settings for validity.
		///     Updates the invalid state flag if any invalid fields are detected.
		/// </summary>
		private void ValidateAllFields()
		{
			if (_RootNode == null) return;
			// Recursively check all nodes for invalid settings
			_IsAnyFieldInvalid = CheckNodeValidity(_RootNode);
		}

		/// <summary>
		///     Checks the validity of all settings within a given configuration node and its children.
		/// </summary>
		/// <param name="_node">
		///     The configuration node to validate,
		///     including its nested settings and child nodes.
		/// </param>
		/// <returns>
		///     A boolean value
		///     indicating whether any setting within the node or its children is invalid.
		/// </returns>
		private bool CheckNodeValidity(ConfigNode _node)
		{
			if (_node.Settings.Any(_s => !_s.IsValid)) return true;
			return _node.Children.Values.Any(CheckNodeValidity);
		}

		/// <summary>
		///     Renders a user interface for an individual configuration entry,
		///     allowing the user to view and modify its value.
		/// </summary>
		/// <param name="_setting">
		///     The display settings containing the configuration entry and related metadata,
		///     including current value, validation state, and error message.
		/// </param>
		private void DrawSetting(DisplaySetting _setting)
		{
			var entry = _setting.Entry;

			var originalColor = GUI.backgroundColor;
			if (!_setting.IsValid) GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);

			EditorGUILayout.BeginVertical();
			EditorGUILayout.Space(4);

			bool isBool = entry.ValueType == typeof(bool);

			// 1. Setting Name (with tooltip)
			EditorGUILayout.LabelField(new GUIContent(entry.Key, entry.Description), EditorStyles.boldLabel);

			if (isBool)
			{
				// Booleans: [ ] Description
				EditorGUILayout.BeginHorizontal();
				float prevLabelWidth = EditorGUIUtility.labelWidth;
				EditorGUIUtility.labelWidth = 1;
				GUILayout.BeginVertical(GUILayout.Width(20));
				DrawFieldForType(_setting, GUIContent.none);
				GUILayout.EndVertical();
				EditorGUIUtility.labelWidth = prevLabelWidth;

				if (_ShowDescriptions)
				{
					if (!string.IsNullOrEmpty(entry.Description))
					{
						var oldColor = GUI.contentColor;
						GUI.contentColor = new Color(0.7f, 0.7f, 0.7f);
						EditorGUILayout.LabelField(entry.Description, EditorStyles.wordWrappedMiniLabel);
						GUI.contentColor = oldColor;
					}
					else
					{
						GUILayout.FlexibleSpace();
					}

					if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("cs Script Icon").image, "Open definition in script"), EditorStyles.iconButton, GUILayout.Width(20), GUILayout.Height(20)))
					{
						OpenScriptForSetting(entry.Key);
					}
				}
				else
				{
					GUILayout.FlexibleSpace();
				}
				EditorGUILayout.EndHorizontal();
			}
			else
			{
				// 2. Description & Code Button
				if (_ShowDescriptions)
				{
					EditorGUILayout.BeginHorizontal();
					if (!string.IsNullOrEmpty(entry.Description))
					{
						var oldColor = GUI.contentColor;
						GUI.contentColor = new Color(0.7f, 0.7f, 0.7f); // Soft gray
						EditorGUILayout.LabelField(entry.Description, EditorStyles.wordWrappedMiniLabel);
						GUI.contentColor = oldColor;
					}
					else
					{
						GUILayout.FlexibleSpace();
					}

					if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("cs Script Icon").image, "Open definition in script"), EditorStyles.iconButton, GUILayout.Width(20), GUILayout.Height(20)))
					{
						OpenScriptForSetting(entry.Key);
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.Space(2);
				}

				// 3. Input Field (Constrained width)
				GUILayout.BeginVertical(GUILayout.MaxWidth(400));
				float prevLabelWidth = EditorGUIUtility.labelWidth;
				EditorGUIUtility.labelWidth = 1;

				DrawFieldForType(_setting, GUIContent.none);

				EditorGUIUtility.labelWidth = prevLabelWidth;
				GUILayout.EndVertical();
			}

			if (!_setting.IsValid) EditorGUILayout.Space(2);
			if (!_setting.IsValid) EditorGUILayout.HelpBox(_setting.ErrorMessage, MessageType.Error);

			EditorGUILayout.Space(6);
			Rect r = EditorGUILayout.GetControlRect(false, 1);
			EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.15f));

			EditorGUILayout.EndVertical();

			GUI.backgroundColor = originalColor;
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

		/// <summary>
		///     Draws the corresponding input field in the Editor GUI for the given configuration setting,
		///     based on its data type, and optionally validates the input value.
		/// </summary>
		/// <param name="_setting">
		///     The display setting
		///     containing the configuration entry and its associated state.
		/// </param>
		/// <param name="_label">
		///     The label to display for the field,
		///     including the key and description of the configuration entry.
		/// </param>
		private void DrawFieldForType(DisplaySetting _setting, GUIContent _label)
		{
			var entry = _setting.Entry;
			object newValue = null;

			EditorGUI.BeginChangeCheck();

			var parser = ConfigLoader.GetParserForType(entry.ValueType);
			if (parser is IConfigEditorParser editorParser)
			{
				newValue = editorParser.DrawEditorGui(_label, _setting.CurrentValue);
				_setting.IsValid = true;
			}
			else
			{
				if (entry.ValueType == typeof(string))
				{
					var newRawValue = EditorGUILayout.TextField(_label, _setting.RawValue);
					if (newRawValue != _setting.RawValue)
					{
						_setting.RawValue = newRawValue;
						newValue = newRawValue;
					}

					_setting.IsValid = true;
				}
				else if (entry.ValueType == typeof(bool))
				{
					newValue = EditorGUILayout.Toggle(_label, (bool)_setting.CurrentValue);
				}
				else if (entry.ValueType == typeof(int))
				{
					var newRawValue = EditorGUILayout.TextField(_label, _setting.RawValue);
					if (!string.Equals(newRawValue, _setting.RawValue, StringComparison.CurrentCulture))
						_setting.RawValue = newRawValue;

					if (int.TryParse(_setting.RawValue, out var parsedInt))
					{
						if (!parsedInt.Equals(_setting.CurrentValue)) newValue = parsedInt;
						_setting.IsValid = true;
					}
					else
					{
						_setting.IsValid = false;
						_setting.ErrorMessage = "Value must be a valid integer.";
					}
				}
				else if (entry.ValueType == typeof(float))
				{
					var newRawValue = EditorGUILayout.TextField(_label, _setting.RawValue);
					if (!string.Equals(newRawValue, _setting.RawValue, StringComparison.CurrentCulture))
						_setting.RawValue = newRawValue;

					if (float.TryParse(_setting.RawValue, NumberStyles.Float, CultureInfo.InvariantCulture,
							out var parsedFloat))
					{
						if (!parsedFloat.Equals(_setting.CurrentValue)) newValue = parsedFloat;
						_setting.IsValid = true;
					}
					else
					{
						_setting.IsValid = false;
						_setting.ErrorMessage = "Value must be a valid floating-point number.";
					}
				}
				else if (entry.ValueType == typeof(Vector3))
				{
					newValue = EditorGUILayout.Vector3Field(_label, (Vector3)_setting.CurrentValue);
				}
				else if (entry.ValueType == typeof(Color))
				{
					newValue = EditorGUILayout.ColorField(_label, (Color)_setting.CurrentValue);
				}
				else if (entry.ValueType.IsEnum)
				{
					newValue = EditorGUILayout.EnumPopup(_label, (Enum)_setting.CurrentValue);
				}
				else
				{
					EditorGUILayout.LabelField(_label,
						new GUIContent($"Unsupported Type: {entry.ValueType.Name}",
							"To edit this type, implement IConfigEditorParser on its parser."));
				}
			}

			if (EditorGUI.EndChangeCheck() && newValue != null) _setting.IsValid = true;

			if (newValue != null && _setting.IsValid)
			{
				var setValueMethod = entry.GetType().GetMethod("SetValue");
				setValueMethod?.Invoke(entry, new[] { newValue });
				_setting.CurrentValue = newValue;
				if (entry.ValueType != typeof(Vector3) && entry.ValueType != typeof(Color))
					_setting.RawValue = newValue.ToString();
			}
		}
	}
}

#endif
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

			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
			GUILayout.Label("Pandora Configuration Settings", EditorStyles.boldLabel);
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Refresh", EditorStyles.toolbarButton)) RefreshSettings();
			EditorGUILayout.EndHorizontal();

			_ScrollPosition = EditorGUILayout.BeginScrollView(_ScrollPosition);

			_IsAnyFieldInvalid = false; // Reset before redraw

			// Recursively draw all nodes starting from the root's children
			foreach (var node in _RootNode.Children.Values.OrderBy(_n => _n.Name)) DrawNode(node);

			EditorGUILayout.EndScrollView();

			EditorGUILayout.BeginHorizontal();

			EditorGUI.BeginDisabledGroup(_IsAnyFieldInvalid);
			if (GUILayout.Button("Save Changes")) SaveChangesAndNotify();
			EditorGUI.EndDisabledGroup();

			if (GUILayout.Button("Reset to Defaults"))
			{
				var configFileName = Path.GetFileName(ConfigLoader.GetConfigPath());
				if (EditorUtility.DisplayDialog("Reset All to Defaults?",
					    $"This will overwrite '{configFileName}' with the default values defined in your code.\n\nThis action cannot be undone.",
					    "Reset", "Cancel")) ResetToDefaultsAndNotify();
			}

			EditorGUILayout.EndHorizontal();
		}

		/// <summary>
		///     Recursively draws a hierarchical configuration node,
		///     including its settings and child nodes, in the Unity editor window.
		/// </summary>
		/// <param name="_node">The configuration node to be drawn, including its settings and child nodes.</param>
		private void DrawNode(ConfigNode _node)
		{
			_node.IsFoldout = EditorGUILayout.Foldout(_node.IsFoldout, _node.Name, true, EditorStyles.foldoutHeader);
			if (_node.IsFoldout)
			{
				EditorGUI.indentLevel++;

				// Draw settings at this level
				foreach (var setting in _node.Settings.OrderBy(_s => _s.Entry.Key))
				{
					DrawSetting(setting);
					if (!setting.IsValid) _IsAnyFieldInvalid = true;
				}

				// Recursively draw child nodes
				foreach (var childNode in _node.Children.Values.OrderBy(_n => _n.Name)) DrawNode(childNode);

				EditorGUI.indentLevel--;
			}
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
			var label = new GUIContent(entry.Key, entry.Description);

			var originalColor = GUI.backgroundColor;
			if (!_setting.IsValid) GUI.backgroundColor = new Color(1f, 0.6f, 0.6f); // Reddish highlight

			DrawFieldForType(_setting, label);

			GUI.backgroundColor = originalColor;

			if (!_setting.IsValid) EditorGUILayout.HelpBox(_setting.ErrorMessage, MessageType.Error);
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
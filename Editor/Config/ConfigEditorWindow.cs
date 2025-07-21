// =================================================================================
// File: ConfigEditorWindow.cs
// Author: MizoreRainy
// Description: A Unity Editor window to visually edit all discovered configuration settings.
//              This file must be placed in an "Editor" folder.
// =================================================================================

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using MizoreRainy.Pandora.ConfigUtility;
using MizoreRainy.Pandora.ConfigUtility.Editor;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace MizoreRainy.Pandora.Editor.ConfigUtility.Editor
{
    /// <summary>
    /// Represents an editor window for modifying and managing configuration settings in the Unity Editor.
    /// </summary>
    public class ConfigEditorWindow : EditorWindow
    {
        /// <summary>
        /// Represents the display settings for an individual configuration entry within the ConfigEditorWindow.
        /// </summary>
        /// <remarks>
        /// This class is used internally by the ConfigEditorWindow to manage and display the state and validation
        /// of configuration entries. Each setting corresponds to a single configuration entry within the editor.
        /// </remarks>
        private class DisplaySetting
        {
            /// <summary>
            /// Represents an instance of a configuration entry implementing the IConfigEntry interface.
            /// Provides access to configuration metadata such as key, description, group name, and value type.
            /// This variable is commonly used to interact with configuration settings within the editor,
            /// allowing values to be displayed, parsed, and updated.
            /// </summary>
            public IConfigEntry Entry;

            /// <summary>
            /// Represents the current value of the configuration entry being displayed
            /// in the editor. This value is dynamically determined and can vary based
            /// on the configuration entry type.
            /// </summary>
            public object CurrentValue;

            /// <summary>
            /// Stores the raw string value of a configuration field entered by the user.
            /// Primarily used for validation and conversion to the expected data type for the configuration entry.
            /// </summary>
            public string RawValue; // Holds the text field's current string, used for validation

            /// <summary>
            /// Indicates whether the current field value is valid based on the validation rules.
            /// </summary>
            public bool IsValid = true;

            /// <summary>
            /// Stores the error message to be displayed when the corresponding configuration value is invalid.
            /// Typically used for communicating validation issues in the editor interface.
            /// </summary>
            public string ErrorMessage = "";
        }

        /// <summary>
        /// Holds a collection of configuration settings grouped by category names.
        /// </summary>
        /// <remarks>
        /// The dictionary uses the group name as the key and a list of <c>DisplaySetting</c> objects as the value.
        /// Each group corresponds to a category of settings stored in <c>ConfigLoader</c>.
        /// The grouped structure aids in organizing and displaying configuration settings within the editor window.
        /// </remarks>
        private Dictionary<string, List<DisplaySetting>> _GroupedSettings;

        /// <summary>
        /// A dictionary used in the ConfigEditorWindow to track the foldout states of configuration groups.
        /// Each key represents a group name, while the corresponding boolean value indicates whether the group's foldout is expanded (true) or collapsed (false).
        /// </summary>
        private readonly Dictionary<string, bool> _GroupFoldouts = new();

        /// <summary>
        /// Stores the current scroll position of the ConfigEditorWindow.
        /// Used to manage vertical scrolling within the GUI when displaying grouped configuration settings.
        /// </summary>
        private Vector2 _ScrollPosition;

        /// <summary>
        /// A private boolean field that indicates whether any field within the configuration editor window
        /// is invalid based on the validation rules associated with the text fields.
        /// </summary>
        /// <remarks>
        /// This field is used to determine if the "Save Changes" button within the ConfigEditorWindow
        /// should be enabled or disabled. A value of <c>true</c> disables the button, while <c>false</c>
        /// enables it. The validation state of all fields is updated by the <c>ValidateAllFields</c> method,
        /// which checks the validity of each field within the grouped settings.
        /// </remarks>
        private bool _IsAnyFieldInvalid;

        /// <summary>
        /// Displays the ConfigEditorWindow in the Unity Editor.
        /// This method is associated with the "Pandora/Config/Edit Configuration" menu item
        /// and opens the "Pandora Config Editor" window.
        /// </summary>
        [MenuItem("Pandora/Config/Edit Configuration")]
        public static void ShowWindow()
        {
            GetWindow<ConfigEditorWindow>("Pandora Config Editor");
        }

        /// <summary>
        /// Called when the ConfigEditorWindow is enabled in the Unity Editor.
        /// This method refreshes the configuration settings by populating the grouped settings
        /// with their current values and initializing foldout states for groups. If the settings
        /// cannot be retrieved, an error message is logged in the console.
        /// </summary>
        private void OnEnable()
        {
            RefreshSettings();
        }

        /// <summary>
        /// Refreshes the configuration settings by reloading data from the configuration source and rebuilding the internal
        /// structure used to display configurable entries in the editor window.
        /// </summary>
        /// <remarks>
        /// This method ensures that the configuration settings are up to date by fetching the latest data from the
        /// `ConfigLoader`. It categorizes the settings into groups, preparing them for display.
        /// If an error occurs (e.g., failure to locate expected configuration fields), logs an error and initializes an empty settings structure.
        /// Validation is also performed on all configuration fields after loading.
        /// </remarks>
        private void RefreshSettings()
        {
            ConfigLoader.EnsureInitialized();
            var settingsField = typeof(ConfigLoader).GetField("Settings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (settingsField == null)
            {
                _GroupedSettings = new Dictionary<string, List<DisplaySetting>>();
                Debug.LogError("[ConfigEditorWindow] Could not find 'Settings' field in ConfigLoader. Cannot display settings.");
                return;
            }

            var settings = (List<IConfigEntry>)settingsField.GetValue(null);

            _GroupedSettings = settings
                .GroupBy(_s => _s.GroupName)
                .OrderBy(_g => _g.Key)
                .ToDictionary(
                    _g => _g.Key,
                    _g => _g.Select(_s =>
                    {
                        var currentValue = _s.GetType().GetProperty("Value")?.GetValue(_s);
                        return new DisplaySetting
                        {
                            Entry = _s,
                            CurrentValue = currentValue,
                            RawValue = currentValue?.ToString() ?? ""
                        };
                    }).ToList()
                );

            foreach (var groupKey in _GroupedSettings.Keys)
            {
                _GroupFoldouts.TryAdd(groupKey, true);
            }
            ValidateAllFields();
        }

        /// <summary>
        /// Renders and manages the GUI for the Config Editor Window in the Unity Editor.
        /// </summary>
        /// <remarks>
        /// Unity automatically calls this method to draw the editor window's graphical user interface.
        /// It interactively displays grouped configuration settings, allowing users to view and edit them.
        /// Includes functionality for refreshing, saving, and resetting configurations to their default values.
        /// </remarks>
        private void OnGUI()
        {
            if (_GroupedSettings == null)
            {
                EditorGUILayout.HelpBox("Could not load settings. Please check the console for errors.", MessageType.Error);
                if (GUILayout.Button("Retry")) RefreshSettings();
                return;
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Pandora Configuration Settings", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton)) RefreshSettings();
            EditorGUILayout.EndHorizontal();

            _ScrollPosition = EditorGUILayout.BeginScrollView(_ScrollPosition);

            foreach (var group in _GroupedSettings)
            {
                _GroupFoldouts[group.Key] = EditorGUILayout.Foldout(_GroupFoldouts[group.Key], group.Key, true, EditorStyles.foldoutHeader);
                if (_GroupFoldouts[group.Key])
                {
                    EditorGUI.indentLevel++;
                    foreach (var setting in group.Value)
                    {
                        DrawSetting(setting);
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();

            // Disable the Save button if any field is invalid
            EditorGUI.BeginDisabledGroup(_IsAnyFieldInvalid);
            if (GUILayout.Button("Save Changes"))
            {
                SaveChangesAndNotify();
            }
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Reset to Defaults"))
            {
                if (EditorUtility.DisplayDialog("Reset All to Defaults?", "This will overwrite config.txt with the default values defined in your code. This cannot be undone.", "Reset", "Cancel"))
                {
                    ResetToDefaultsAndNotify();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Saves the current configuration settings asynchronously and displays a notification upon success.
        /// If an error occurs during the save process, it is silently ignored.
        /// </summary>
        /// <remarks>
        /// This method uses the <c>ConfigLoader.SaveAsync</c> method to persist changes to the configuration file.
        /// After saving, a success dialog box is displayed to inform the user of the successful operation.
        /// Any exceptions encountered during this process are caught and ignored.
        /// </remarks>
        private async void SaveChangesAndNotify()
        {
            try
            {
                await ConfigLoader.SaveAsync();
                EditorUtility.DisplayDialog("Success", "Configuration saved successfully to config.txt.", "OK");
            }
            catch (Exception)
            {
                // Ignore
            }
        }

        /// <summary>
        /// Resets all configuration settings to their default values and notifies the user upon completion.
        /// This method calls the ResetToDefaultsAsync function from the ConfigLoader to reset the settings,
        /// refreshes the settings displayed in the editor, and repaints the editor window. If the reset
        /// is successful, a success dialog is displayed; otherwise, errors are silently ignored.
        /// </summary>
        private async void ResetToDefaultsAndNotify()
        {
            try
            {
                await ConfigLoader.ResetToDefaultsAsync();
                RefreshSettings();
                Repaint();
                EditorUtility.DisplayDialog("Success", "Configuration has been reset to defaults.", "OK");
            }
            catch (Exception)
            {
                // Ignore
            }
        }

        /// <summary>
        /// Validates all configuration fields within the editor window.
        /// </summary>
        /// <remarks>
        /// This method iterates over all grouped settings and checks if any field is invalid.
        /// The result is stored in a private boolean field to determine if any settings require correction.
        /// </remarks>
        private void ValidateAllFields()
        {
            _IsAnyFieldInvalid = _GroupedSettings.Values.SelectMany(_list => _list).Any(_s => !_s.IsValid);
        }

        /// <summary>
        /// Draws a single configuration setting in the editor interface, including validation and error handling.
        /// </summary>
        /// <param name="_setting">The configuration setting to be displayed and modified, including its metadata, current value, and validation state.</param>
        private void DrawSetting(DisplaySetting _setting)
        {
            var entry = _setting.Entry;
            var label = new GUIContent(entry.Key, entry.Description);

            EditorGUI.BeginChangeCheck();

            var originalColor = GUI.backgroundColor;
            if (!_setting.IsValid)
            {
                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f); // Reddish highlight
            }

            // --- Draw appropriate field ---
            DrawFieldForType(_setting, label);

            GUI.backgroundColor = originalColor;

            if (EditorGUI.EndChangeCheck())
            {
                // If a change occurred, re-validate all fields to update the save button state
                ValidateAllFields();
            }

            if (!_setting.IsValid)
            {
                EditorGUILayout.HelpBox(_setting.ErrorMessage, MessageType.Error);
            }
        }

        /// <summary>
        /// Draws the appropriate UI field for a configuration entry based on its type.
        /// </summary>
        /// <param name="_setting">The setting containing the configuration entry to be drawn and its current state.</param>
        /// <param name="_label">The label used for the UI field, typically displaying the key and description of the configuration entry.</param>
        private void DrawFieldForType(DisplaySetting _setting, GUIContent _label)
        {
            var entry = _setting.Entry;
            object newValue = null;

            var parser = ConfigLoader.GetParserForType(entry.ValueType);
            if (parser is IConfigEditorParser editorParser)
            {
                newValue = editorParser.DrawEditorGui(_label, _setting.CurrentValue);
            }
            else
            {
                if (entry.ValueType == typeof(string))
                {
                    _setting.RawValue = EditorGUILayout.TextField(_label, _setting.RawValue);
                    newValue = _setting.RawValue;
                    _setting.IsValid = true;
                }
                else if (entry.ValueType == typeof(bool))
                {
                    newValue = EditorGUILayout.Toggle(_label, (bool)_setting.CurrentValue);
                    _setting.IsValid = true;
                }
                else if (entry.ValueType == typeof(int))
                {
                    _setting.RawValue = EditorGUILayout.TextField(_label, _setting.RawValue);
                    if (int.TryParse(_setting.RawValue, out int parsedInt))
                    {
                        newValue = parsedInt;
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
                    _setting.RawValue = EditorGUILayout.TextField(_label, _setting.RawValue);
                    if (float.TryParse(_setting.RawValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsedFloat))
                    {
                        newValue = parsedFloat;
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
                    _setting.IsValid = true;
                }
                else if (entry.ValueType == typeof(Color))
                {
                    newValue = EditorGUILayout.ColorField(_label, (Color)_setting.CurrentValue);
                    _setting.IsValid = true;
                }
                else if (entry.ValueType.IsEnum)
                {
                    newValue = EditorGUILayout.EnumPopup(_label, (Enum)_setting.CurrentValue);
                    _setting.IsValid = true;
                }
                else
                {
                    EditorGUILayout.LabelField(_label, new GUIContent($"Unsupported Type: {entry.ValueType.Name}", "To edit this type, implement IConfigEditorParser on its parser."));
                    _setting.IsValid = true; // Assume valid if we can't edit it.
                }
            }

            if (newValue != null && _setting.IsValid)
            {
                var setValueMethod = entry.GetType().GetMethod("SetValue");
                setValueMethod?.Invoke(entry, new[] { newValue });
                _setting.CurrentValue = newValue;
                _setting.RawValue = newValue.ToString();
            }
        }
    }
}

#endif

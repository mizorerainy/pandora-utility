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
		#region Unity Lifecycle

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
			if (GUILayout.Button("Reset to Defaults", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false))) 
			{
				var configFileName = Path.GetFileName(ConfigLoader.GetConfigPath());
				if (EditorUtility.DisplayDialog("Reset All to Defaults?",
						$"This will overwrite '{configFileName}' with the default values defined in your code.\n\nThis action cannot be undone.",
						"Reset", "Cancel")) ResetToDefaultsAndNotify();
			}
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

			GUILayout.Space(5);

			EditorGUI.BeginDisabledGroup(_IsAnyFieldInvalid);
			EditorGUI.BeginDisabledGroup(_IsAnyFieldInvalid);
			Rect saveRect = GUILayoutUtility.GetRect(new GUIContent("Save Changes"), GUI.skin.button, GUILayout.Height(20));
			if (DrawHoldButton(saveRect, "Hold to Save", new Color(0.4f, 0.8f, 0.4f, 1f), ref _SaveHoldStartTime, ref _SaveSuccessTime))
			{
				SaveChangesAndNotify();
			}
			EditorGUI.EndDisabledGroup();

			GUILayout.Space(2);

			Rect discardRect = GUILayoutUtility.GetRect(new GUIContent("Discard Changes"), GUI.skin.button, GUILayout.Height(20));
			if (DrawHoldButton(discardRect, "Hold to Discard", new Color(0.8f, 0.8f, 0.2f, 1f), ref _DiscardHoldStartTime, ref _DiscardSuccessTime))
			{
				DiscardChangesAndNotify();
			}

			GUILayout.Space(5);

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

			EditorGUILayout.EndVertical(); // End Right Pane
			EditorGUILayout.EndHorizontal(); // End Main Area
			EditorGUI.EndDisabledGroup();
		}

		#endregion


		#region UI Rendering

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
			if (_setting.HasCustomColor) GUI.backgroundColor = _setting.CustomColor;
			if (!_setting.IsValid) GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);

			if (_setting.HasCustomColor || !_setting.IsValid)
			{
				EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			}
			else
			{
				EditorGUILayout.BeginVertical();
				EditorGUILayout.Space(4);
			}

			// Restore immediately so inner controls (labels, input fields, inner arrays) aren't tinted
			GUI.backgroundColor = originalColor;

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
				else if (entry.ValueType.IsArray && entry.ValueType.GetArrayRank() == 1)
				{
					newValue = DrawArrayField(_setting, _label);
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
				if (entry.ValueType != typeof(Vector3) && entry.ValueType != typeof(Color) && !entry.ValueType.IsArray)
					_setting.RawValue = newValue.ToString();
			}
		}

		private object DrawArrayField(DisplaySetting _setting, GUIContent _label)
		{
			var entry = _setting.Entry;
			Type elementType = entry.ValueType.GetElementType();

			if (!_ArrayLists.TryGetValue(entry.Key, out var rList))
			{
				var list = new List<object>();
				if (_setting.CurrentValue is Array arr)
				{
					foreach (var item in arr) list.Add(item);
				}

				rList = new UnityEditorInternal.ReorderableList(list, typeof(object), true, true, true, true);
				
				rList.elementHeightCallback = (int index) => {
					return EditorGUIUtility.singleLineHeight + 4;
				};

				rList.drawHeaderCallback = (Rect rect) => {
					EditorGUI.LabelField(rect, _label);
				};

				rList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
					rect.y += 2;
					rect.height = EditorGUIUtility.singleLineHeight;
					
					var currentList = (List<object>)rList.list;
					var elementValue = currentList[index];
					
					EditorGUI.BeginChangeCheck();
					object newElementValue = DrawElementField(rect, elementValue, elementType);
					if (EditorGUI.EndChangeCheck())
					{
						currentList[index] = newElementValue;
						GUI.changed = true;
					}
				};

				rList.onAddCallback = (UnityEditorInternal.ReorderableList l) => {
					object def = elementType.IsValueType ? Activator.CreateInstance(elementType) : (elementType == typeof(string) ? "" : null);
					l.list.Add(def);
					GUI.changed = true;
				};

				rList.onRemoveCallback = (UnityEditorInternal.ReorderableList l) => {
					UnityEditorInternal.ReorderableList.defaultBehaviours.DoRemoveButton(l);
					GUI.changed = true;
				};

				rList.onReorderCallbackWithDetails = (UnityEditorInternal.ReorderableList l, int oldIndex, int newIndex) => {
					GUI.changed = true;
				};

				_ArrayLists[entry.Key] = rList;
			}
			else
			{
				var list = (List<object>)rList.list;
				if (_setting.CurrentValue is Array arr && arr.Length != list.Count)
				{
					list.Clear();
					foreach (var item in arr) list.Add(item);
				}
			}

			bool wasChanged = GUI.changed;
			GUI.changed = false;

			rList.DoLayoutList();

			bool isChangedNow = GUI.changed;
			GUI.changed = wasChanged || isChangedNow;

			if (isChangedNow)
			{
				var currentList = (List<object>)rList.list;
				Array newArray = Array.CreateInstance(elementType, currentList.Count);
				for (int i = 0; i < currentList.Count; i++)
				{
					newArray.SetValue(currentList[i], i);
				}
				
				var parser = ConfigLoader.GetParserForType(entry.ValueType);
				if (parser != null)
				{
					_setting.RawValue = parser.ToString(newArray);
				}
				_setting.IsValid = true;
				return newArray;
			}

			return null;
		}

		private object DrawElementField(Rect rect, object value, Type type)
		{
			if (type == typeof(string)) return EditorGUI.TextField(rect, value as string ?? "");
			if (type == typeof(bool)) return EditorGUI.Toggle(rect, value != null && (bool)value);
			if (type == typeof(int)) return EditorGUI.IntField(rect, value != null ? (int)value : 0);
			if (type == typeof(float)) return EditorGUI.FloatField(rect, value != null ? (float)value : 0f);
			if (type == typeof(Vector3)) return EditorGUI.Vector3Field(rect, GUIContent.none, value != null ? (Vector3)value : Vector3.zero);
			if (type == typeof(Color)) return EditorGUI.ColorField(rect, GUIContent.none, value != null ? (Color)value : Color.white);
			if (type.IsEnum) return EditorGUI.EnumPopup(rect, value != null ? (Enum)value : (Enum)Activator.CreateInstance(type));
			
			EditorGUI.LabelField(rect, $"Unsupported Element: {type.Name}");
			return value;
		}

		private bool DrawHoldButton(Rect rect, string text, Color baseColor, ref double holdStartTime, ref double successTime)
		{
			bool triggered = false;
			float holdDurationRequired = 0.5f; // 0.5 seconds to trigger
			float progress = 0f;

			float timeSinceSuccess = (float)(EditorApplication.timeSinceStartup - successTime);
			bool isSuccessFlash = timeSinceSuccess < 0.4f;
			Color finalBaseColor = baseColor * 0.5f;

			if (isSuccessFlash)
			{
				float flashT = Mathf.PingPong(timeSinceSuccess * 6f, 1f);
				finalBaseColor = Color.Lerp(Color.white, baseColor, flashT);
				Repaint();
			}

			int controlID = GUIUtility.GetControlID(FocusType.Passive);
			Event e = Event.current;
			bool isHovered = rect.Contains(e.mousePosition);

			if (e.type == EventType.MouseDown && e.button == 0 && isHovered)
			{
				GUIUtility.hotControl = controlID;
				holdStartTime = EditorApplication.timeSinceStartup;
				e.Use();
			}

			if (GUIUtility.hotControl == controlID)
			{
				Repaint(); 
				if (e.type == EventType.MouseUp || (!isHovered && e.type == EventType.MouseDrag))
				{
					GUIUtility.hotControl = 0;
					holdStartTime = 0;
					if (e.type == EventType.MouseUp) e.Use();
				}
				else if (e.type == EventType.Repaint)
				{
					float durationHeld = (float)(EditorApplication.timeSinceStartup - holdStartTime);
					progress = Mathf.Clamp01(durationHeld / holdDurationRequired);

					if (progress >= 1f)
					{
						GUIUtility.hotControl = 0;
						holdStartTime = 0;
						successTime = EditorApplication.timeSinceStartup;
						triggered = true;
						EditorApplication.Beep();
					}
				}
			}

			EditorGUI.DrawRect(rect, finalBaseColor);

			if (GUIUtility.hotControl == controlID && progress > 0f)
			{
				Rect fillRect = new Rect(rect.x, rect.y, rect.width * progress, rect.height);
				EditorGUI.DrawRect(fillRect, baseColor);
			}

			if (isHovered && GUIUtility.hotControl == 0) 
			{
				EditorGUI.DrawRect(rect, new Color(1,1,1,0.1f));
			}

			var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
			style.normal.textColor = Color.white;
			GUI.Label(rect, text, style);

			return triggered;
		}

		#endregion
	}
}

#endif

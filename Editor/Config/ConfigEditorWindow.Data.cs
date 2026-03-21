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
		#region Nested Types

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

			public bool HasCustomColor;
			public Color CustomColor;

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
	}
}

#endif

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
			// The underlying configuration entry object encapsulating key, type, and value logic.
			public IConfigEntry Entry;

			// The current value of the configuration entry as it exists in the editor.
			public object CurrentValue;

			// Holds the text field's current string, used for validation
			public string RawValue;

			public bool HasCustomColor;
			public Color CustomColor;

			// Indicates whether the current value of the setting is valid.
			public bool IsValid = true;

			// Stores the error message associated with the validation status.
			public string ErrorMessage = "";
		}


		/// <summary>
		///     Represents a node in the hierarchical configuration tree,
		///     allowing organization and storage of nested settings.
		///     Each node can contain a list of settings and a dictionary of child nodes.
		/// </summary>
		private class ConfigNode
		{
			// The name of the configuration node.
			public string Name;

			// The rendering order of this node, determined by the ConfigGroupAttribute.
			public int Order;

			// Indicates whether the foldout (expand/collapse) state is expanded.
			public bool IsFoldout = true;

			// The settings collection associated with this node.
			public readonly List<DisplaySetting> Settings = new();

			// A dictionary representing the child nodes.
			public readonly Dictionary<string, ConfigNode> Children = new();

			// Checks if this node, its settings, or any of its children match the given search query.
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

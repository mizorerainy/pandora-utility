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
		#region Internal & Interface Implementations

		// Validates all fields within the configuration hierarchy.
		private void ValidateAllFields()
		{
			if (_RootNode == null) return;
			// Recursively check all nodes for invalid settings
			_IsAnyFieldInvalid = CheckNodeValidity(_RootNode);
		}


		// Checks the validity of all settings within a given configuration node and its children.
		private bool CheckNodeValidity(ConfigNode _node)
		{
			if (_node.Settings.Any(_s => !_s.IsValid)) return true;
			return _node.Children.Values.Any(CheckNodeValidity);
		}


		#endregion
	}
}

#endif

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
		#region Validation

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


		#endregion
	}
}

#endif

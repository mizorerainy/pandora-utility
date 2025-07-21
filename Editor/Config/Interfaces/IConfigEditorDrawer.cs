// =================================================================================
// File: IConfigEditorDrawer.cs
// Author: MizoreRainy
// Description: An optional interface for custom parsers to provide a custom GUI
//              in the Pandora Config Editor window.
//              This system is dependency-free.
// =================================================================================

#if UNITY_EDITOR

using UnityEngine;

// ReSharper disable once CheckNamespace
namespace MizoreRainy.Pandora.ConfigUtility.Editor
{
	/// <summary>
	/// Defines a contract for providing a custom GUI drawer for a configuration
	/// setting within the Pandora Config Editor window.
	/// </summary>
	public interface IConfigEditorDrawer
	{
		/// <summary>
		/// Draws the custom GUI for a configuration setting.
		/// </summary>
		/// <param name="_label">The label to display for the setting, which includes the tooltip.</param>
		/// <param name="_currentValue">The current value of the setting.</param>
		/// <returns>The new value for the setting if it was changed by the user.</returns>
		object DrawEditorGui(GUIContent _label, object _currentValue);
	}
}

#endif
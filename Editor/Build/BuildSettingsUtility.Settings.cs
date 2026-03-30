#if UNITY_6000_0_OR_NEWER
using UnityEditor.Build.Profile;
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using MizoreRainy.Pandora;

// ReSharper disable once CheckNamespace
namespace MizoreRainy.Pandora.BuildUtility
{
	public partial class BuildSettingsUtility
	{
		#region Internal & Interface Implementations

		// Renders the Global Settings section within the Unity Editor window.
		// This method is responsible for displaying the user interface for configuring global build settings,
		// including settings for the bundle version and the output folder path. It ensures users can input
		// valid version numbers and select appropriate paths for build outputs, applying validation checks
		// where necessary.
		private void DrawGlobalSettingsSection()
		{
			GUILayout.Label("Global Settings", EditorStyles.boldLabel);
			EditorGUILayout.BeginVertical("box");

			// --- Version Editor ---
			EditorGUILayout.LabelField("Bundle Version", EditorStyles.boldLabel);

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Current Version:", GUILayout.Width(100));

			if (_IsEditingVersion)
			{
				// Editing mode - show text field with the current version
				var originalColor = GUI.backgroundColor;
				if (!_IsNewVersionValid) GUI.backgroundColor = Color.red;

				EditorGUI.BeginChangeCheck();
				_NewVersion = EditorGUILayout.TextField(_NewVersion);
				if (EditorGUI.EndChangeCheck()) ValidateVersionString();

				GUI.backgroundColor = originalColor;

				GUILayout.FlexibleSpace();

				// Save button - enabled only if a version is valid and different
				var isSameVersion = _NewVersion.Equals(PlayerSettings.bundleVersion);
				GUI.enabled = _IsNewVersionValid && !isSameVersion;
				GUI.backgroundColor = Color.green;
				if (GUILayout.Button("Save", GUILayout.Width(60)))
				{
					PlayerSettings.bundleVersion = _NewVersion;
					PandoraLogger.LogBuild($"Bundle version updated to: {_NewVersion}");
					_IsEditingVersion = false;
				}

				GUI.backgroundColor = Color.white;
				GUI.enabled = true;

				// Cancel button
				GUI.backgroundColor = Color.red;
				if (GUILayout.Button("Cancel", GUILayout.Width(60)))
				{
					_IsEditingVersion = false;
					_NewVersion = PlayerSettings.bundleVersion; // Reset to original
					_IsNewVersionValid = true;
					GUI.FocusControl(null); // Clear focus
				}

				GUI.backgroundColor = Color.white;
			}
			else
			{
				// Display mode - show the current version as a label
				EditorGUILayout.LabelField(PlayerSettings.bundleVersion, EditorStyles.boldLabel);

				GUILayout.FlexibleSpace();

				// Change Version button
				GUI.backgroundColor = Color.cyan;
				if (GUILayout.Button("Change Version", GUILayout.Width(100)))
				{
					_IsEditingVersion = true;
					_NewVersion = PlayerSettings.bundleVersion; // Initialize with the current version
					_IsNewVersionValid = true;
				}

				GUI.backgroundColor = Color.white;
			}

			EditorGUILayout.EndHorizontal();

			// Show validation warning only when editing
			if (_IsEditingVersion && !_IsNewVersionValid)
				EditorGUILayout.HelpBox("Version must be in 'major.minor.patch' format (e.g., 1.0.0).",
					MessageType.Warning);

			GUILayout.Space(10);

			// --- Build Folder Path ---
			EditorGUILayout.LabelField("Build Output", EditorStyles.boldLabel);
			EditorGUILayout.BeginHorizontal();
			_BuildSettingsData.BuildFolderPath =
				EditorGUILayout.TextField("Build Folder Path", _BuildSettingsData.BuildFolderPath);
			if (GUILayout.Button("Browse", GUILayout.Width(60)))
			{
				var selectedPath = EditorUtility.OpenFolderPanel("Select Root Build Folder", "", "");
				if (!string.IsNullOrEmpty(selectedPath)) _BuildSettingsData.BuildFolderPath = selectedPath;
			}

			EditorGUILayout.EndHorizontal();

			EditorGUILayout.EndVertical();
			GUILayout.Space(10);
		}


		#endregion
	}
}

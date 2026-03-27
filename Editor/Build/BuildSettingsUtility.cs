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
	/// <summary>
	/// BuildSettingsUtility is a utility class for managing build settings in Unity.
	/// This class extends the EditorWindow to provide a user interface for handling build profiles.
	/// It is accessible through the Unity Editor via the menu item "Pandora/Managed Build Utility".
	/// </summary>
	public partial class BuildSettingsUtility : EditorWindow
	{
		#region Fields

		/// <summary>
		/// The relative file path within the Unity project where the Managed Build Settings asset
		/// is located or intended to be created. This path is used for accessing or saving
		/// the build settings data used by the Managed Build Utility.
		/// </summary>
		private const string _SETTING_FILE_PATH = "Assets/Editor/ManagedBuildSettings.asset";



		/// <summary>
		/// A private instance of the BuildSettingsData ScriptableObject used within
		/// the BuildSettingsUtility editor window. This variable holds and manages
		/// global build configurations and settings necessary for the custom build
		/// utility workflow.
		/// </summary>
		private BuildSettingsData _BuildSettingsData;

		/// <summary>
		/// The SerializedObject representation of the BuildSettingsData asset.
		/// Used for drawing fields like SerializeReference lists natively in the editor window.
		/// </summary>
		private SerializedObject _SerializedSettings;

		/// <summary>
		/// Represents the current scroll position within the editor window's scroll view.
		/// This variable is used to preserve or restore the scroll state during navigation and interaction
		/// with the graphical user interface elements in the Build Utility editor window.
		/// </summary>
		private Vector2 _ScrollPosition;

		/// <summary>
		/// Represents the new version string for the application. This private field is associated with
		/// the editable version value displayed within the Unity Editor and mirrors the bundle version in
		/// Player Settings. It supports validation to ensure adherence to standard versioning practices such
		/// as 'major.minor.patch'.
		/// </summary>
		/// <remarks>
		/// Changes to this property trigger validation to ensure the version is well-formed. When saved
		/// through the Build Utility, it updates the Unity PlayerSettings.bundleVersion.
		/// </remarks>
		private string _NewVersion;

		/// <summary>
		/// Represents the validity state of the new version string input.
		/// </summary>
		/// <remarks>
		/// This field determines whether the current version string entered
		/// adheres to the expected format ('major.minor.patch', e.g., 1.0.0).
		/// It is primarily used within the context of editing the bundle version
		/// to control user feedback and the enabling or disabling of related controls.
		/// </remarks>
		private bool _IsNewVersionValid = true;

		/// <summary>
		/// Indicates whether the version editing mode is currently active in the Build Settings Utility.
		/// This variable controls the state of the UI, determining whether the user is allowed
		/// to edit the bundle version or view it as a read-only field.
		/// </summary>
		private bool _IsEditingVersion;

		/// <summary>
		/// A dictionary that tracks the expanded or collapsed states of foldout UI elements.
		/// Each key corresponds to a unique identifier, typically associated with a specific
		/// UI section, and the value determines whether the section is expanded (true) or
		/// collapsed (false). Used primarily for managing the state of expandable sections
		/// in the editor interface.
		/// </summary>
		private readonly Dictionary<string, bool> _FoldoutStates = new();

		/// <summary>
		/// A dictionary used to maintain the foldout state for scene lists in the UI.
		/// Keyed by unique identifiers (e.g., profile-specific keys), this helps track whether
		/// the scene list for a particular build profile is expanded or collapsed.
		/// </summary>
		private readonly Dictionary<string, bool> _SceneListFoldouts = new();

		#endregion

		#region Menu Item

		/// <summary>
		/// Displays the Build Settings Utility window in the Unity Editor.
		/// </summary>
		/// <remarks>
		/// Opens a custom editor window titled "Managed Builds" when invoked on Unity versions 6000.0 or newer.
		/// If the Unity version is older, a dialog appears to inform the user that the feature is unavailable.
		/// </remarks>
		[MenuItem("Pandora/Managed Build Utility")]
		public static void ShowWindow()
		{
#if UNITY_6000_0_OR_NEWER
			GetWindow<BuildSettingsUtility>("Managed Builds");
#else
            EditorUtility.DisplayDialog("Feature Not Available",
                "This utility requires Unity 6000.0 or newer for Build Profiles support.\n\nCurrent version: " + Application.unityVersion, "OK");
#endif
		}

		#endregion

		#region Unity Lifecycle

		/// <summary>
		/// Unity callback method that is invoked when the script instance is enabled.
		/// Prepares and initializes the editor window, sets the current build version from PlayerSettings,
		/// validates the version string, and subscribes to the editor update event for continuous updates.
		/// </summary>
		/// <remarks>
		/// This method ensures that build settings are loaded or created and properly initializes
		/// any required configurations for managing the build settings in the Unity Editor.
		/// </remarks>
		private void OnEnable()
		{
			LoadOrCreateSettings();
			if (_BuildSettingsData != null) _SerializedSettings = new SerializedObject(_BuildSettingsData);
			_NewVersion = PlayerSettings.bundleVersion;
			ValidateVersionString(true);
			EditorApplication.update += OnEditorUpdate;
		}

		/// <summary>
		/// Called when the editor window is disabled or closed.
		/// This method unsubscribes the <c>OnEditorUpdate</c> callback from the
		/// <c>EditorApplication.update</c> event to clean up resources and prevent
		/// unintended updates when the window is not in use.
		/// </summary>
		private void OnDisable()
		{
			EditorApplication.update -= OnEditorUpdate;
		}

		/// <summary>
		/// Handles routine updates within the Unity Editor while the custom editor window is active.
		/// </summary>
		/// <remarks>
		/// This method is invoked as part of the Unity Editor's update cycle and is primarily used to ensure the
		/// custom editor window remains responsive to changes, such as ongoing compilation or updates to editor states.
		/// When Unity starts compiling, it triggers a repaint of the editor window to reflect potential interface updates.
		/// </remarks>
		private void OnEditorUpdate()
		{
			if (EditorApplication.isCompiling) Repaint();
		}

		/// <summary>
		/// Handles the rendering and interaction logic for the custom Build Settings Utility window.
		/// </summary>
		/// <remarks>
		/// - Dynamically adjusts the user interface based on the current Unity Editor state,
		/// including compilation status and the presence of required assets.
		/// - Provides warnings and helpful messages if essential assets are missing or if the
		/// Unity version is unsupported.
		/// - Supports enabling user interactions to create new BuildSettingsData assets and manage
		/// build profiles through scrollable sections.
		/// - Ensures changes to the asset are marked as dirty to preserve user modifications.
		/// - Disables interaction while Unity is compiling to prevent conflicts.
		/// </remarks>
		private void OnGUI()
		{
#if !UNITY_6000_0_OR_NEWER
            EditorGUILayout.HelpBox("This utility requires Unity 6000.0 or newer.", MessageType.Error);
            return;
#else
			// Wrap the entire UI in a disabled group when compiling
			EditorGUI.BeginDisabledGroup(EditorApplication.isCompiling);

			if (EditorApplication.isCompiling)
				EditorGUILayout.HelpBox("Please wait for compilation to complete.", MessageType.Info);

			if (_BuildSettingsData == null)
			{
				EditorGUILayout.HelpBox("Managed Build Settings Asset not found.", MessageType.Warning);
				if (GUILayout.Button("Create New Managed Build Settings Asset")) CreateSettingsAsset();
			}
			else
			{
				EditorGUILayout.ObjectField("Settings Asset", _BuildSettingsData, typeof(BuildSettingsData), false);

				_ScrollPosition = EditorGUILayout.BeginScrollView(_ScrollPosition);

				if (_SerializedSettings != null) _SerializedSettings.Update();

				DrawGlobalSettingsSection();
				DrawManagedProfilesSection();

				if (_SerializedSettings != null) _SerializedSettings.ApplyModifiedProperties();

				EditorGUILayout.EndScrollView();

				if (GUI.changed) EditorUtility.SetDirty(_BuildSettingsData);
			}

			EditorGUI.EndDisabledGroup();
#endif
		}

		#endregion

	}
}

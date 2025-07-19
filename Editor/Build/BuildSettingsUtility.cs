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
using Debug = UnityEngine.Debug;

// ReSharper disable once CheckNamespace
namespace MizoreRainy.Pandora.BuildUtility
{
	/// <summary>
	/// BuildSettingsUtility is a utility class for managing build settings in Unity.
	/// This class extends the EditorWindow to provide a user interface for handling build profiles.
	/// It is accessible through the Unity Editor via the menu item "Pandora/Managed Build Utility".
	/// </summary>
	public class BuildSettingsUtility : EditorWindow
	{
		#region Const Fields

		/// <summary>
		/// The relative file path within the Unity project where the Managed Build Settings asset
		/// is located or intended to be created. This path is used for accessing or saving
		/// the build settings data used by the Managed Build Utility.
		/// </summary>
		private const string _SETTING_FILE_PATH = "Assets/Editor/ManagedBuildSettings.asset";

		#endregion

		#region Private Fields

		/// <summary>
		/// A private instance of the BuildSettingsData ScriptableObject used within
		/// the BuildSettingsUtility editor window. This variable holds and manages
		/// global build configurations and settings necessary for the custom build
		/// utility workflow.
		/// </summary>
		private BuildSettingsData _BuildSettingsData;

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

		#region Unity Events

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

				DrawGlobalSettingsSection();
				DrawManagedProfilesSection();

				EditorGUILayout.EndScrollView();

				if (GUI.changed) EditorUtility.SetDirty(_BuildSettingsData);
			}

			EditorGUI.EndDisabledGroup();
#endif
		}

		#endregion

		#region UI Drawing Methods

		/// <summary>
		/// Renders the Global Settings section within the Unity Editor window.
		/// </summary>
		/// <remarks>
		/// This method is responsible for displaying the user interface for configuring global build settings,
		/// including settings for the bundle version and the output folder path. It ensures users can input
		/// valid version numbers and select appropriate paths for build outputs, applying validation checks
		/// where necessary.
		/// </remarks>
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
					Debug.Log($"Bundle version updated to: {_NewVersion}");
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


		/// <summary>
		/// Renders the section for managing a list of build profiles in the Unity Editor window interface.
		/// </summary>
		/// <remarks>
		/// This method is part of a utility that allows users to interact with managed build profiles within a customizable UI
		/// in the Unity Editor. Users can view a list of profiles and perform actions such as adding, removing, or collapsing
		/// and expanding profile details for better management. Each profile is displayed with relevant controls and options.
		/// Key features include:
		/// - Displaying registered build profiles with clear labels.
		/// - Providing functionality to collapse or expand individual profile sections.
		/// - Enabling addition of new profiles through a dedicated action button.
		/// - Allowing removal of profiles with a confirmation step to prevent accidental deletion.
		/// - Highlighting profiles that are actively in use when supported by the current Unity version.
		/// </remarks>
		private void DrawManagedProfilesSection()
		{
			GUILayout.Label("Managed Profiles", EditorStyles.boldLabel);

			for (var i = 0; i < _BuildSettingsData.ManagedProfiles.Count; i++)
			{
				var managedProfile = _BuildSettingsData.ManagedProfiles[i];
				var foldoutKey = $"config_{i}";
				_FoldoutStates.TryAdd(foldoutKey, true);

#if UNITY_6000_0_OR_NEWER
				var isActive = managedProfile.TargetProfile != null &&
				               BuildProfile.GetActiveBuildProfile() == managedProfile.TargetProfile;
				var originalBgColor = GUI.backgroundColor;
				if (isActive) GUI.backgroundColor = new Color(.2f, .2f, .5f, 1f); // Light green
#endif

				EditorGUILayout.BeginVertical("box");
#if UNITY_6000_0_OR_NEWER
				GUI.backgroundColor = originalBgColor;
#endif

				// --- Clean Profile Header ---
				EditorGUILayout.BeginHorizontal();

				var displayName = string.IsNullOrEmpty(managedProfile.Name) ? "Unnamed Profile" : managedProfile.Name;
				_FoldoutStates[foldoutKey] = EditorGUILayout.Foldout(_FoldoutStates[foldoutKey], displayName, true);

				GUILayout.FlexibleSpace();

				if (GUILayout.Button(new GUIContent("X", "Remove Managed Profile"), GUILayout.Width(25)))
					if (EditorUtility.DisplayDialog("Remove Profile",
						    $"Are you sure you want to remove '{managedProfile.Name}'?", "Yes", "No"))
					{
						_BuildSettingsData.ManagedProfiles.RemoveAt(i);
						i--;
						continue;
					}

				EditorGUILayout.EndHorizontal();

				if (_FoldoutStates[foldoutKey]) DrawManagedProfileDetails(managedProfile);

				EditorGUILayout.EndVertical();
				GUILayout.Space(5);
			}

			// Draw horizontal link
			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();

			if (GUILayout.Button("Add New Managed Profile"))
				_BuildSettingsData.ManagedProfiles.Add(new ManagedBuildProfile());

			EditorGUILayout.EndHorizontal();
		}

		/// <summary>
		/// Renders the detailed user interface for a specified managed build profile, allowing the user to view
		/// and edit its properties such as name, target profile, and additional settings.
		/// </summary>
		/// <param name="_managedProfile">
		/// The <see cref="ManagedBuildProfile" /> instance representing the managed profile
		/// whose details are to be displayed and modified.
		/// </param>
		private void DrawManagedProfileDetails(ManagedBuildProfile _managedProfile)
		{
#if UNITY_6000_0_OR_NEWER
			EditorGUILayout.BeginVertical();

			// Profile name input moved here for a cleaner UI
			_managedProfile.Name = EditorGUILayout.TextField("Profile Name", _managedProfile.Name);

			// --- Main Two-Column Layout ---
			EditorGUILayout.BeginHorizontal();

			// --- Left Column: Settings (expandable horizontally only) ---
			EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
			_managedProfile.TargetProfile = (BuildProfile)EditorGUILayout.ObjectField("Unity Build Profile",
				_managedProfile.TargetProfile, typeof(BuildProfile), false);

			if (_managedProfile.TargetProfile == null)
			{
				EditorGUILayout.HelpBox("Please assign a Unity Build Profile.", MessageType.Warning);
			}
			else
			{
				_managedProfile.ProductNameOverride = EditorGUILayout.TextField(
					new GUIContent("Product Name Override", "Leave empty to use the name from the linked profile."),
					_managedProfile.ProductNameOverride);
				_managedProfile.BuildSuffix = EditorGUILayout.TextField(
					new GUIContent("Build Suffix", "e.g., 'prd', 'dev', 'test'"), _managedProfile.BuildSuffix);
			}

			EditorGUILayout.EndVertical();

			// --- Right Column: Icon (compact, minimal vertical space) ---
			if (_managedProfile.TargetProfile != null)
			{
				EditorGUILayout.BeginVertical(GUILayout.Width(80), GUILayout.ExpandWidth(false));

				var icon = GetIconForProfile(_managedProfile.TargetProfile);
				var iconContent = icon != null
					? new GUIContent(icon)
					: new GUIContent("No Icon",
						"Set the 'Default Icon' in the Player Settings Overrides of the linked Unity Build Profile.");

				// Icon with minimal vertical padding
				GUILayout.Box(iconContent, GUILayout.Width(64), GUILayout.Height(64));

				EditorGUILayout.EndVertical();
			}

			EditorGUILayout.EndHorizontal();

			// --- Scene List ---
			if (_managedProfile.TargetProfile != null)
			{
				var sceneFoldoutKey = $"scenes_{_managedProfile.GetHashCode()}";
				_SceneListFoldouts.TryAdd(sceneFoldoutKey, false);

				_SceneListFoldouts[sceneFoldoutKey] =
					EditorGUILayout.Foldout(_SceneListFoldouts[sceneFoldoutKey], "Scenes in Profile", true);
				if (_SceneListFoldouts[sceneFoldoutKey])
				{
					EditorGUI.indentLevel++;
					var scenes = GetScenesFromProfile(_managedProfile.TargetProfile);
					if (scenes.Count > 0)
						foreach (var sceneAsset in scenes)
						{
							EditorGUILayout.BeginHorizontal();

							// Add the "Open" button at the front
							if (GUILayout.Button("Open", GUILayout.Width(50)))
								if (sceneAsset != null)
								{
									var scenePath = AssetDatabase.GetAssetPath(sceneAsset);
									EditorSceneManager.OpenScene(scenePath);
								}

							// Make the object field readonly (non-editable)
							GUI.enabled = false;
							EditorGUILayout.ObjectField("", sceneAsset, typeof(SceneAsset), false);
							GUI.enabled = true;

							EditorGUILayout.EndHorizontal();
						}
					else
						EditorGUILayout.LabelField("No enabled scenes in this profile.");

					EditorGUI.indentLevel--;
				}
			}

			// --- Action Buttons ---
			if (_managedProfile.TargetProfile != null)
			{
				var isActive = BuildProfile.GetActiveBuildProfile() == _managedProfile.TargetProfile;

				GUILayout.Space(5);
				EditorGUILayout.BeginHorizontal();

				// Set Active Button
				GUI.enabled = !isActive;
				if (GUILayout.Button("Set Active")) BuildProfile.SetActiveBuildProfile(_managedProfile.TargetProfile);
				GUI.enabled = true;

				EditorGUILayout.EndHorizontal();

				// --- Build Button (Full Width) ---
				GUILayout.Space(5);
				GUI.enabled = isActive;
				GUI.backgroundColor = isActive ? Color.cyan : Color.white;
				if (GUILayout.Button($"Build '{_managedProfile.Name}'", GUILayout.Height(25)))
					BuildProject(_managedProfile);
				GUI.backgroundColor = Color.white;
				GUI.enabled = true;
			}

			EditorGUILayout.EndVertical();
#endif
		}

		#endregion

		#region Build Methods

		/// <summary>
		/// Builds a Unity project based on the specified managed build profile.
		/// </summary>
		/// <param name="_managedProfile">
		/// The managed build profile containing settings and overrides for the build process.
		/// </param>
		private void BuildProject(ManagedBuildProfile _managedProfile)
		{
#if UNITY_6000_0_OR_NEWER
			// Now that the profile is active and settings are applied, create build options
			var buildOptions = new BuildPlayerOptions
			{
				scenes = EditorBuildSettings.scenes.Where(_s => _s.enabled).Select(_s => _s.path).ToArray(),
				target = EditorUserBuildSettings.activeBuildTarget,
				options = BuildOptions.None // Start with default options
			};

			// Apply development/debugging flags from the now-active settings
			if (EditorUserBuildSettings.development) buildOptions.options |= BuildOptions.Development;
			if (EditorUserBuildSettings.allowDebugging) buildOptions.options |= BuildOptions.AllowDebugging;

			// --- Apply Overrides ---
			var productName = string.IsNullOrEmpty(_managedProfile.ProductNameOverride)
				? PlayerSettings.productName
				: _managedProfile.ProductNameOverride;

			// --- Construct Final Build Path ---
			buildOptions.locationPathName = ConstructBuildPath(_managedProfile, buildOptions.target, productName);

			if (string.IsNullOrEmpty(buildOptions.locationPathName))
			{
				EditorUtility.DisplayDialog("Build Error",
					"Failed to construct a valid build path. Check console for errors.", "OK");
				return;
			}

			// Log current settings being used
			var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildOptions.target);
			var currentDefines =
				PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup));
			Debug.Log($"Building with scripting define symbols: {currentDefines}");

			// --- Execute Build ---
			Debug.Log($"Starting build for '{_managedProfile.Name}'");
			Debug.Log($"Product Name: {productName}");
			Debug.Log($"Output Path: {buildOptions.locationPathName}");

			var report = BuildPipeline.BuildPlayer(buildOptions);

			if (report.summary.result == BuildResult.Succeeded)
			{
				Debug.Log(
					$"Build SUCCEEDED: {report.summary.outputPath} ({report.summary.totalSize / 1024 / 1024} MB)");
				Process.Start(Path.GetDirectoryName(report.summary.outputPath) ?? string.Empty);
			}
			else
			{
				var errorMessage = $"Build FAILED: {report.summary.result}. Errors: {report.summary.totalErrors}.";
				Debug.LogError(errorMessage);
				EditorUtility.DisplayDialog("Build Failed", errorMessage, "OK");
			}
#endif
		}


		/// <summary>
		/// Constructs a complete build path for the given build profile, target platform, and product name.
		/// </summary>
		/// <remarks>
		/// This method generates a structured and organized build directory path based on the provided settings and platform.
		/// The path includes directories and filename formatting such as root path, platform-specific folder, profile name,
		/// versioning, and timestamp. If necessary, it also creates the corresponding directories in the filesystem.
		/// </remarks>
		/// <param name="_managedProfile">The managed build profile containing relevant settings, such as name and suffix.</param>
		/// <param name="_target">The build target specifying the target platform (e.g., Windows, iOS, Android).</param>
		/// <param name="_productName">The name of the product used as part of the resulting build artifact name.</param>
		/// <returns>
		/// The full path string to the build artifact, including directories and file extension for the target platform.
		/// Returns null if an error occurs.
		/// </returns>
		private string ConstructBuildPath(ManagedBuildProfile _managedProfile, BuildTarget _target, string _productName)
		{
			try
			{
				var rootPath = _BuildSettingsData.BuildFolderPath;
				var platformFolder = GetPlatformFolder(_target);
				var profileNameFolder = _managedProfile.Name;
				var versionString = PlayerSettings.bundleVersion.Replace('.', '-');
				var timestamp = DateTime.Now.ToString("yyMMdd-HHmm");

				// Format: [Product Name(lower case)]-[Suffix]-v[Bundle Version]-[Timestamp]
				var artifactName =
					$"{_productName.ToLower()}-{_managedProfile.BuildSuffix}-v{versionString}-{timestamp}";

				var finalDir = Path.Combine(rootPath, platformFolder, profileNameFolder, artifactName);

				if (_target is BuildTarget.StandaloneWindows or BuildTarget.StandaloneWindows64
				    or BuildTarget.StandaloneOSX or BuildTarget.iOS)
				{
					Directory.CreateDirectory(finalDir);
					if (_target is BuildTarget.iOS) return finalDir; // For iOS, the path is the folder
					return Path.Combine(finalDir, _productName + GetBuildExtension(_target));
				}

				// For Android and other file-based builds
				var parentDir = Path.GetDirectoryName(finalDir);
				if (parentDir != null) Directory.CreateDirectory(parentDir);
				return finalDir + GetBuildExtension(_target);
			}
			catch (Exception ex)
			{
				Debug.LogError($"Error constructing build path: {ex.Message}");
				return null;
			}
		}

		#endregion

		#region Settings Management

		/// <summary>
		/// Loads the Managed Build Settings asset from the predefined file path or assigns it, allowing for build profile
		/// management within the utility.
		/// </summary>
		/// <remarks>
		/// This method attempts to find an asset of the type BuildSettingsData located at the specified file path. If the
		/// asset is unavailable or has not been previously created, its absence is expected in scenarios where
		/// configuration may be initialized differently. This assignment ensures the utility has an accessible instance of
		/// the build settings.
		/// </remarks>
		private void LoadOrCreateSettings()
		{
			_BuildSettingsData = AssetDatabase.LoadAssetAtPath<BuildSettingsData>(_SETTING_FILE_PATH);
		}

		/// <summary>
		/// Creates a new ManagedBuildSettings asset file in a specified path
		/// if it does not already exist.
		/// </summary>
		/// <remarks>
		/// This method ensures the existence of the directory structure required
		/// for storing the asset. If the directory does not exist, it is created.
		/// The created asset file becomes active in the Unity Editor, and the Project
		/// window is focused to highlight it. The path for the asset is determined
		/// by a predefined constant within the utility.
		/// </remarks>
		private void CreateSettingsAsset()
		{
			var directory = Path.GetDirectoryName(_SETTING_FILE_PATH);
			if (directory != null && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

			var newSettings = CreateInstance<BuildSettingsData>();
			AssetDatabase.CreateAsset(newSettings, _SETTING_FILE_PATH);
			AssetDatabase.SaveAssets();
			_BuildSettingsData = newSettings;
			EditorUtility.FocusProjectWindow();
			Selection.activeObject = newSettings;
			Debug.Log($"Created new ManagedBuildSettings asset at: {_SETTING_FILE_PATH}");
		}

		#endregion

		#region Helper Methods

		/// <summary>
		/// Validates the version string format and updates the internal validity state.
		/// The version string is expected to adhere to the "major.minor.patch" format.
		/// </summary>
		/// <param name="_forceUpdate">
		/// Indicates whether to force a re-evaluation of the validity state,
		/// regardless of potential changes to the current version string.
		/// </param>
		private void ValidateVersionString(bool _forceUpdate = false)
		{
			var isValid = IsValidVersionFormat(_NewVersion);
			if (_IsNewVersionValid != isValid || _forceUpdate)
			{
				_IsNewVersionValid = isValid;
				Repaint();
			}
		}

		/// <summary>
		/// Validates whether the given version string follows the major.minor.patch format (e.g., "1.0.0").
		/// </summary>
		/// <param name="_version">The version string to validate.</param>
		/// <returns>True if the version string matches the format; otherwise, false.</returns>
		private bool IsValidVersionFormat(string _version)
		{
			if (string.IsNullOrEmpty(_version)) return false;
			// Regex for major.minor.patch format
			return Regex.IsMatch(_version, @"^\d+\.\d+\.\d+$");
		}

		/// <summary>
		/// Determines the folder name for the specified build target.
		/// </summary>
		/// <param name="_target">The build target for which to retrieve the platform folder name.</param>
		/// <returns>A string containing the name of the folder associated with the specified build target.</returns>
		private string GetPlatformFolder(BuildTarget _target)
		{
			return _target switch
			{
				BuildTarget.StandaloneWindows or BuildTarget.StandaloneWindows64 or BuildTarget.StandaloneOSX => "PC",
				BuildTarget.Android => "Android",
				BuildTarget.iOS => "iOS",
				_ => "Other"
			};
		}

		/// <summary>
		/// Retrieves the default icon associated with a given BuildProfile.
		/// </summary>
		/// <remarks>
		/// This method attempts to fetch the icon defined in the Player Settings YAML overrides
		/// of the specified build profile. If no icon is explicitly set, it falls back to the global
		/// settings associated with the target platform of the profile.
		/// </remarks>
		/// <param name="_buildProfile">The BuildProfile instance from which to retrieve the default icon.</param>
		/// <returns>
		/// A Texture2D object representing the profile's default icon,
		/// or null if the icon cannot be found or an error occurs during retrieval.
		/// </returns>
		private Texture2D GetIconForProfile(BuildProfile _buildProfile)
		{
#if UNITY_6000_0_OR_NEWER
			try
			{
				// Get the profile's PlayerSettings overrides
				var serializedProfile = new SerializedObject(_buildProfile);
				var playerSettingsYaml = serializedProfile.FindProperty("m_PlayerSettingsYaml");

				var settingsArray = playerSettingsYaml?.FindPropertyRelative("m_Settings");
				if (settingsArray is { isArray: true })
					// Look for icon settings in the YAML overrides
					for (var i = 0; i < settingsArray.arraySize; i++)
					{
						var setting = settingsArray.GetArrayElementAtIndex(i);
						var line = setting.FindPropertyRelative("line");
						if (line != null)
						{
							var lineValue = line.stringValue;

							// Look for the default icon line in build target icons
							if (lineValue.Contains("m_BuildTargetIcons:"))
							{
								// Found the start of icon definitions, look for the next few lines
								for (var j = i + 1; j < Mathf.Min(i + 10, settingsArray.arraySize); j++)
								{
									var iconLine = settingsArray.GetArrayElementAtIndex(j);
									var iconLineValue = iconLine.FindPropertyRelative("line")?.stringValue;

									if (iconLineValue != null && iconLineValue.Contains("m_Icon: {fileID:"))
									{
										// Extract the GUID from the line
										var guidMatch = Regex.Match(
											iconLineValue, @"guid: ([a-f0-9]+)");

										if (guidMatch.Success)
										{
											var guid = guidMatch.Groups[1].Value;
											var assetPath = AssetDatabase.GUIDToAssetPath(guid);
											if (!string.IsNullOrEmpty(assetPath))
											{
												var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
												if (icon != null) return icon;
											}
										}

										break;
									}
								}

								break;
							}
						}
					}

				// Fallback: try to get from global settings for the profile's target platform
				var buildTarget = GetBuildTargetFromProfile(_buildProfile);
				var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
				var icons = PlayerSettings.GetIcons(NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup),
					IconKind.Application);
				return icons?.FirstOrDefault(_i => _i != null);
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"Failed to get icon from Build Profile: {ex.Message}");
				return null;
			}
#else
            return null;
#endif
		}


		/// <summary>
		/// Retrieves the BuildTarget associated with the specified BuildProfile.
		/// </summary>
		/// <param name="_profile">The BuildProfile from which to extract platform data.</param>
		/// <returns>
		/// The BuildTarget corresponding to the platform defined in the BuildProfile.
		/// If the platform is not mapped, returns the active BuildTarget as a fallback.
		/// </returns>
		private BuildTarget GetBuildTargetFromProfile(BuildProfile _profile)
		{
#if UNITY_6000_0_OR_NEWER
			// Use reflection to get the build target from the profile
			var serializedProfile = new SerializedObject(_profile);
			var platformProperty = serializedProfile.FindProperty("m_Platform");

			if (platformProperty != null && platformProperty.objectReferenceValue != null)
			{
				var platformName = platformProperty.objectReferenceValue.name;
				// This is a simplified mapping. More platforms can be added as needed.
				return platformName switch
				{
					"StandaloneWindows64Platform" => BuildTarget.StandaloneWindows64,
					"StandaloneWindowsPlatform" => BuildTarget.StandaloneWindows,
					"StandaloneOSXPlatform" => BuildTarget.StandaloneOSX,
					"AndroidPlatform" => BuildTarget.Android,
					"iOSPlatform" => BuildTarget.iOS,
					"WebGLPlatform" => BuildTarget.WebGL,
					_ => EditorUserBuildSettings.activeBuildTarget // Fallback
				};
			}

			return EditorUserBuildSettings.activeBuildTarget; // Fallback
#else
            return BuildTarget.StandaloneWindows64;
#endif
		}


		/// <summary>
		/// Returns the file extension used for the build artifact based on the specified build target.
		/// </summary>
		/// <param name="_target">The build target for which the file extension is needed.</param>
		/// <returns>
		/// A string representing the file extension, such as ".exe" for Windows, ".app" for macOS,
		/// ".apk" for Android, or an empty string for unsupported targets.
		/// </returns>
		private string GetBuildExtension(BuildTarget _target)
		{
			return _target switch
			{
				BuildTarget.StandaloneWindows or BuildTarget.StandaloneWindows64 => ".exe",
				BuildTarget.StandaloneOSX => ".app",
				BuildTarget.Android => ".apk",
				_ => ""
			};
		}

		/// <summary>
		/// Retrieves a list of scene assets from the provided build profile.
		/// </summary>
		/// <param name="_profile">
		/// The build profile object from which the scenes are extracted. This profile contains settings
		/// such as platform and enabled scenes.
		/// </param>
		/// <returns>
		/// A list of <c>SceneAsset</c> objects corresponding to the scenes enabled in the provided build profile.
		/// If no scenes are enabled or the profile is null, it returns an empty list.
		/// </returns>
		private List<SceneAsset> GetScenesFromProfile(BuildProfile _profile)
		{
			var sceneAssets = new List<SceneAsset>();
#if UNITY_6000_0_OR_NEWER
			if (_profile == null) return sceneAssets;

			var scenes = _profile.scenes;
			if (scenes.Length > 0)
				sceneAssets.AddRange(scenes.Select(_scene => AssetDatabase.LoadAssetAtPath<SceneAsset>(_scene.path)));
#endif
			return sceneAssets;
		}

		#endregion
	}
}
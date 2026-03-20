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
		#region UI Rendering - Managed Profiles


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
				EditorGUI.BeginDisabledGroup(isActive);
				if (GUILayout.Button("Set Active")) BuildProfile.SetActiveBuildProfile(_managedProfile.TargetProfile);
				EditorGUI.EndDisabledGroup();

				EditorGUILayout.EndHorizontal();

				// --- Build Button (Full Width) ---
				GUILayout.Space(5);
				EditorGUI.BeginDisabledGroup(!isActive);
				var originalColorButton = GUI.backgroundColor;
				GUI.backgroundColor = isActive ? Color.cyan : Color.white;
				if (GUILayout.Button($"Build '{_managedProfile.Name}'", GUILayout.Height(25)))
					BuildProject(_managedProfile);
				GUI.backgroundColor = originalColorButton;
				EditorGUI.EndDisabledGroup();
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
			PandoraLogger.LogBuild($"Building with scripting define symbols: {currentDefines}");

			// --- Execute Build ---
			PandoraLogger.LogBuild($"Starting build for '{_managedProfile.Name}'");
			PandoraLogger.LogBuild($"Product Name: {productName}");
			PandoraLogger.LogBuild($"Output Path: {buildOptions.locationPathName}");

			var report = BuildPipeline.BuildPlayer(buildOptions);

			if (report.summary.result == BuildResult.Succeeded)
			{
				PandoraLogger.LogBuild(
					$"Build SUCCEEDED: {report.summary.outputPath} ({report.summary.totalSize / 1024 / 1024} MB)");
				Process.Start(Path.GetDirectoryName(report.summary.outputPath) ?? string.Empty);
			}
			else
			{
				var errorMessage = $"Build FAILED: {report.summary.result}. Errors: {report.summary.totalErrors}.";
				PandoraLogger.LogBuildError(errorMessage);
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
				PandoraLogger.LogBuildError($"Error constructing build path: {ex.Message}");
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
			PandoraLogger.LogBuild($"Created new ManagedBuildSettings asset at: {_SETTING_FILE_PATH}");
		}

		#endregion
	}
}

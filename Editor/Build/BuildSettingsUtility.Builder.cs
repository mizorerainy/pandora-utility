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
		#region Utility Methods

		#region Utility Methods

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
				PandoraLogger.LogBuildWarning($"Failed to get icon from Build Profile: {ex.Message}");
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

		#region Post-Build Actions

		/// <summary>
		/// Executes all configured post-build tasks for a given managed profile.
		/// </summary>
		private void ExecutePostBuildTasks(ManagedBuildProfile _profile, string _buildOutputPath)
		{
			if (_profile.PostBuildTasks == null || _profile.PostBuildTasks.Count == 0) return;

			foreach (var task in _profile.PostBuildTasks)
			{
				if (task == null || !task.IsEnabled) continue;

				PandoraLogger.LogBuild($"Executing post-build task: {task.GetType().Name}");
				try
				{
					task.Execute(_profile, _buildOutputPath);
				}
				catch (Exception ex)
				{
					PandoraLogger.LogBuildWarning($"Post-build task {task.GetType().Name} failed: {ex.Message}\n{ex.StackTrace}");
				}
			}
		}

		#endregion

		#endregion
	}
}

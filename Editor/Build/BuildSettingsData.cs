using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_6000_0_OR_NEWER
using UnityEditor.Build.Profile;
#endif

// ReSharper disable once CheckNamespace
namespace MizoreRainy.Pandora.BuildUtility
{
	/// <summary>
	/// A wrapper that links a Unity Build Profile to a set of build-specific overrides.
	/// This provides a streamlined, one-click build experience on top of Unity's core profile system.
	/// </summary>
	[Serializable]
	public class ManagedBuildProfile
	{
		[Tooltip("A descriptive name for this managed profile (e.g., 'Windows - Production').")]
		public string Name = "New Managed Profile";

		[Tooltip("The base Unity Build Profile to use for settings like platform, scenes, etc.")]
#if UNITY_6000_0_OR_NEWER
		public BuildProfile TargetProfile;
#endif

		[Tooltip("(Optional) Override the product name set in PlayerSettings or the linked profile.")]
		public string ProductNameOverride = "";

		[Tooltip("A suffix to append to the build artifact name (e.g., 'prd', 'dev').")]
		public string BuildSuffix = "";

		[Tooltip("List of post-build actions to execute after a successful build. Tasks run in order.")]
		[SerializeReference]
		public List<ManagedPostBuildTask> PostBuildTasks = new();
	}

	/// <summary>
	/// A ScriptableObject that stores all the managed build configurations for the project.
	/// It acts as the central database for the custom build utility UI.
	/// </summary>
	[CreateAssetMenu(fileName = "ManagedBuildSettings", menuName = "Pandora/Managed Build Settings", order = 2)]
	public class BuildSettingsData : ScriptableObject
	{
		[Header("Global Build Settings")]
		[Tooltip("The root folder where all builds will be saved.")]
		public string BuildFolderPath = "Builds";

		[Header("Managed Profiles")]
		[Tooltip("The list of profiles that add one-click build functionality to Unity's Build Profiles.")]
		public List<ManagedBuildProfile> ManagedProfiles = new();
	}
}
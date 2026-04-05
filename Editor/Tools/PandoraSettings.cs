#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MizoreRainy.Pandora.Editor.Tools
{
	/// <summary>
	///     Project-wide configuration settings for Pandora.
	///     Saved as an asset so it can be shared via version control.
	/// </summary>
	public class PandoraSettings : ScriptableObject
	{
		public bool ConfigLoaderAutoInit = false;
		public bool ConfigLoaderAsyncInit = false;
		public bool DisableConfigWatcher = false;
		public bool AllowRuntimeReflection = false;
		public bool UseYaml = false;
		public bool SetupComplete = false;
		public bool SetupCompleteShown = false;

		private const string DefaultSettingsPath = "Assets/Editor/Pandora/PandoraSettings.asset";
		private const string CachedPathKey = "PandoraSettings.CachedPath";

		public static PandoraSettings GetOrCreateSettings()
		{
			string cachedPath = EditorPrefs.GetString(CachedPathKey, string.Empty);
			if (!string.IsNullOrEmpty(cachedPath))
			{
				var cachedSettings = AssetDatabase.LoadAssetAtPath<PandoraSettings>(cachedPath);
				if (cachedSettings != null)
				{
					return cachedSettings;
				}
			}

			string[] guids = AssetDatabase.FindAssets("t:PandoraSettings");
			if (guids.Length > 0)
			{
				string path = AssetDatabase.GUIDToAssetPath(guids[0]);
				if (guids.Length > 1)
				{
					Debug.LogWarning($"Multiple PandoraSettings assets found in the project. Using the first one at: {path}");
				}
				EditorPrefs.SetString(CachedPathKey, path);
				return AssetDatabase.LoadAssetAtPath<PandoraSettings>(path);
			}

			var settings = CreateInstance<PandoraSettings>();

			if (!AssetDatabase.IsValidFolder("Assets/Editor"))
			{
				AssetDatabase.CreateFolder("Assets", "Editor");
			}
			if (!AssetDatabase.IsValidFolder("Assets/Editor/Pandora"))
			{
				AssetDatabase.CreateFolder("Assets/Editor", "Pandora");
			}

			AssetDatabase.CreateAsset(settings, DefaultSettingsPath);
			AssetDatabase.SaveAssets();

			EditorPrefs.SetString(CachedPathKey, DefaultSettingsPath);
			return settings;
		}

		public void Save()
		{
			EditorUtility.SetDirty(this);
			AssetDatabase.SaveAssetIfDirty(this);
		}
	}
}
#endif

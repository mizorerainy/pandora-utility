#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor.Build;

// ReSharper disable once CheckNamespace
namespace MizoreRainy.Pandora.Editor.Tools
{
    // A new window for managing Pandora package settings.
    public class PandoraSettingsWindow : EditorWindow
    {
        // Config Loader Settings
        private const string _CONFIG_AUTO_INIT_KEY = "Pandora.ConfigLoader.AutoInitEnabled";
        private const string _CONFIG_LOADER_AUTO_INIT_SYMBOL = "CONFIG_LOADER_AUTO_INIT";
        private bool _configLoaderAutoInit;

        [MenuItem("Window/Pandora/Settings")]
        public static void ShowWindow()
        {
            GetWindow<PandoraSettingsWindow>("Pandora Settings");
        }

        private void OnEnable()
        {
            // Load saved settings
            _configLoaderAutoInit = EditorPrefs.GetBool(_CONFIG_AUTO_INIT_KEY, false); // Default to false (manual init)
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Pandora Module Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // --- Config Loader Section ---
            EditorGUILayout.LabelField("Config Loader", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _configLoaderAutoInit = EditorGUILayout.Toggle(new GUIContent("Enable Auto-Initialization", "If enabled, the ConfigLoader will initialize automatically before the first scene loads. If disabled, you must call ConfigLoader.InitializeAsync() manually."), _configLoaderAutoInit);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(_CONFIG_AUTO_INIT_KEY, _configLoaderAutoInit);
                PackageSetup.UpdateScriptingDefines();
                Debug.Log($"Config Loader Auto-Initialization set to: {_configLoaderAutoInit}");
            }
            EditorGUILayout.HelpBox("Auto-Initialization is recommended for most projects. Disable this only if you need full control over the startup sequence.", MessageType.Info);
        }
    }


    [InitializeOnLoad]
    public static class PackageSetup
    {
        // UniTask constants
        private const string _UNITASK_PACKAGE_ID = "com.cysharp.unitask";
        private const string _UNITASK_GIT_URL = "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask";
        private const string _UNITASK_DEFINE_SYMBOL = "HAVE_CYSHARP_UNITASK";
        
        // Config Loader constants
        private const string _CONFIG_AUTO_INIT_KEY = "Pandora.ConfigLoader.AutoInitEnabled";
        private const string _CONFIG_LOADER_AUTO_INIT_SYMBOL = "CONFIG_LOADER_AUTO_INIT";

        // Setup tracking
        private const string _SETUP_COMPLETE_KEY = "PandoraNetworkUtility.SetupComplete";

        static PackageSetup()
        {
            EditorApplication.delayCall += CheckSetup;
        }

        private static void CheckSetup()
        {
            UpdateScriptingDefines();

            if (EditorPrefs.GetBool(_SETUP_COMPLETE_KEY, false))
            {
                if (IsUniTaskInstalled())
                {
                    ShowCompletionDialog();
                }
                return;
            }

            if (!IsUniTaskInstalled())
            {
                ShowSetupDialog();
            }
            else
            {
                EditorPrefs.SetBool(_SETUP_COMPLETE_KEY, true);
                ShowCompletionDialog();
            }
        }

        // Made this public so the settings window can call it.
        public static void UpdateScriptingDefines()
        {
            var target = EditorUserBuildSettings.selectedBuildTargetGroup;
            var definesString = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);
            var definesList = new HashSet<string>(definesString.Split(';'));

            bool definesChanged = false;

            // Handle UniTask define
            definesChanged |= SetDefine(ref definesList, _UNITASK_DEFINE_SYMBOL, IsUniTaskInstalled());
            
            // Handle Config Loader define
            definesChanged |= SetDefine(ref definesList, _CONFIG_LOADER_AUTO_INIT_SYMBOL, EditorPrefs.GetBool(_CONFIG_AUTO_INIT_KEY, false));

            if (definesChanged)
            {
                PlayerSettings.SetScriptingDefineSymbolsForGroup(target, string.Join(";", definesList));
                Debug.Log("Pandora package scripting defines updated.");
            }
        }

        private static bool SetDefine(ref HashSet<string> defines, string define, bool shouldExist)
        {
            bool hasDefine = defines.Contains(define);
            if (shouldExist && !hasDefine)
            {
                defines.Add(define);
                return true;
            }
            if (!shouldExist && hasDefine)
            {
                defines.Remove(define);
                return true;
            }
            return false;
        }

        private static void ShowSetupDialog()
        {
            var message = "Pandora Network Utility Setup\n\n" +
                         "This package requires UniTask to function properly.\n\n" +
                         "Would you like to install UniTask automatically?";

            var result = EditorUtility.DisplayDialogComplex(
                "Pandora Network Utility Setup",
                message,
                "Install UniTask",
                "Skip (Install Manually)",
                "Cancel"
            );

            switch (result)
            {
                case 0: // Install UniTask
                    InstallUniTask();
                    break;
                case 1: // Skip
                    EditorPrefs.SetBool(_SETUP_COMPLETE_KEY, true);
                    Debug.Log("UniTask installation skipped. Please install it manually: " + _UNITASK_GIT_URL);
                    break;
                case 2: // Cancel
                    Debug.Log("Setup cancelled. You can run setup again from: Window > Pandora > Package Setup");
                    break;
            }
        }

        private static void ShowCompletionDialog()
        {
            if (!EditorPrefs.GetBool(_SETUP_COMPLETE_KEY + "_shown", false))
            {
                var message = "Pandora Package Setup Complete!\n\n" +
                             "✅ UniTask - Installed and configured\n";

                EditorUtility.DisplayDialog("Setup Complete", message, "OK");
                EditorPrefs.SetBool(_SETUP_COMPLETE_KEY + "_shown", true);
            }
        }

        private static void InstallUniTask()
        {
            try
            {
                var manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");

                if (!File.Exists(manifestPath))
                {
                    Debug.LogError("manifest.json file not found at expected location: " + manifestPath);
                    return;
                }

                var manifestJson = File.ReadAllText(manifestPath);
                JObject manifest = JObject.Parse(manifestJson);
                
                if (manifest["dependencies"]?[_UNITASK_PACKAGE_ID] != null)
                {
                    Debug.Log("UniTask is already present in manifest.json");
                    EditorPrefs.SetBool(_SETUP_COMPLETE_KEY, true);
                    return;
                }

                manifest["dependencies"] ??= new JObject();

                if (manifest["dependencies"] is JObject dependencies)
                    dependencies[_UNITASK_PACKAGE_ID] = _UNITASK_GIT_URL;

                File.WriteAllText(manifestPath, manifest.ToString(Formatting.Indented));
                EditorPrefs.SetBool(_SETUP_COMPLETE_KEY, true);
                UnityEditor.PackageManager.Client.Resolve();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to install UniTask: {ex.Message}");
            }
        }

        private static bool IsUniTaskInstalled()
        {
            // This is a simplified check. For a robust solution, consider parsing manifest.json
            // or using PackageManager API correctly. The original code had a synchronous block which is not ideal.
            // A simple check for the define symbol might be sufficient if the setup process is reliable.
            var manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
            if(File.Exists(manifestPath))
            {
                var manifestText = File.ReadAllText(manifestPath);
                return manifestText.Contains(_UNITASK_PACKAGE_ID);
            }
            return false;
        }

        [MenuItem("Window/Pandora/Package Setup")]
        public static void ShowSetupMenu()
        {
            EditorPrefs.SetBool(_SETUP_COMPLETE_KEY + "_shown", false);
            EditorPrefs.SetBool(_SETUP_COMPLETE_KEY, false);
            CheckSetup();
        }
    }
}
#endif

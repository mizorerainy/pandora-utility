
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor.Build;

// ReSharper disable once CheckNamespace
namespace MizoreRainy.Pandora.Editor.Tools
{
    [InitializeOnLoad]
    public static class PackageSetup
    {
        // UniTask constants
        private const string _UNITASK_PACKAGE_ID = "com.cysharp.unitask";
        private const string _UNITASK_GIT_URL = "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask";
        private const string _UNITASK_DEFINE_SYMBOL = "HAVE_CYSHARP_UNITASK";

        // Setup tracking
        private const string _SETUP_COMPLETE_KEY = "PandoraNetworkUtility.SetupComplete";

        static PackageSetup()
        {
            EditorApplication.delayCall += CheckSetup;
        }

        private static void CheckSetup()
        {
            // Update always defines based on the current state
            var isUniTaskInstalled = IsUniTaskInstalled();
            UpdateScriptDefines(isUniTaskInstalled);

            // Check if setup was already completed
            if (EditorPrefs.GetBool(_SETUP_COMPLETE_KEY, false))
            {
                // Still show a completion dialog if everything is ready
                if (isUniTaskInstalled)
                {
                    ShowCompletionDialog();
                }
                return;
            }

            // Show the setup dialog only if UniTask is missing
            if (!isUniTaskInstalled)
            {
                ShowSetupDialog();
            }
            else
            {
                // Mark setup as complete if UniTask is already installed
                EditorPrefs.SetBool(_SETUP_COMPLETE_KEY, true);
                ShowCompletionDialog();
            }
        }

        private static void UpdateScriptDefines(bool _hasUniTask)
        {
            var target = EditorUserBuildSettings.selectedBuildTargetGroup;
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(target));
            var definesList = defines.Split(';').ToList();

            // Handle UniTask define
            var hasUniTaskDefine = definesList.Contains(_UNITASK_DEFINE_SYMBOL);

            if (_hasUniTask && !hasUniTaskDefine)
            {
                definesList.Add(_UNITASK_DEFINE_SYMBOL);
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(target), string.Join(";", definesList));
                Debug.Log($"Added script define: {_UNITASK_DEFINE_SYMBOL}");
            }
            else if (!_hasUniTask && hasUniTaskDefine)
            {
                definesList.Remove(_UNITASK_DEFINE_SYMBOL);
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(target), string.Join(";", definesList));
                Debug.LogWarning($"Removed script define: {_UNITASK_DEFINE_SYMBOL} - UniTask package not found!");
            }
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
            // Only show this once when the setup is first completed
            if (!EditorPrefs.GetBool(_SETUP_COMPLETE_KEY + "_shown", false))
            {
                var message = "Pandora Package Setup Complete!\n\n" +
                             "✅ UniTask - Installed and configured\n";

#if UNITY_6000_0_OR_NEWER
                message += "✅ Unity 6+ detected - Build Utility available\n";
#else
                message += "⚠️ Unity 6+ required for Build Utility\n";
#endif

                message += "\nAll features are now ready to use!";

                EditorUtility.DisplayDialog("Setup Complete", message, "OK");
                EditorPrefs.SetBool(_SETUP_COMPLETE_KEY + "_shown", true);
            }
        }

        private static void InstallUniTask()
        {
            try
            {
                // Read current manifest
                var manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");

                if (!File.Exists(manifestPath))
                {
                    Debug.LogError("manifest.json file not found at expected location: " + manifestPath);
                    EditorUtility.DisplayDialog("Installation Failed",
                        "manifest.json file not found. This might indicate a corrupted Unity project.\n\n" +
                        $"Please install UniTask manually: {_UNITASK_GIT_URL}",
                        "OK");
                    return;
                }

                var manifestJson = File.ReadAllText(manifestPath);

                if (string.IsNullOrWhiteSpace(manifestJson))
                {
                    Debug.LogError("manifest.json is empty or contains only whitespace");
                    EditorUtility.DisplayDialog("Installation Failed",
                        "manifest.json file is empty or corrupted. Cannot modify package dependencies.\n\n" +
                        $"Please restore your manifest.json file and install UniTask manually: {_UNITASK_GIT_URL}",
                        "OK");
                    return;
                }

                JObject manifest;
                try
                {
                    manifest = JObject.Parse(manifestJson);
                }
                catch (JsonReaderException ex)
                {
                    Debug.LogError($"manifest.json contains invalid JSON: {ex.Message}");
                    EditorUtility.DisplayDialog("Installation Failed",
                        "manifest.json contains invalid JSON and cannot be parsed.\n\n" +
                        $"Please fix your manifest.json file and install UniTask manually: {_UNITASK_GIT_URL}",
                        "OK");
                    return;
                }

                // Check if UniTask is already present
                if (manifest["dependencies"]?[_UNITASK_PACKAGE_ID] != null)
                {
                    Debug.Log("UniTask is already present in manifest.json");
                    EditorPrefs.SetBool(_SETUP_COMPLETE_KEY, true);
                    return;
                }

                // Ensure dependencies object exists
                manifest["dependencies"] ??= new JObject();

                // Add UniTask dependency
                if (manifest["dependencies"] is JObject dependencies)
                    dependencies[_UNITASK_PACKAGE_ID] = _UNITASK_GIT_URL;

                // Convert back to JSON string with proper formatting
                var updatedJson = manifest.ToString(Formatting.Indented);

                if (string.IsNullOrWhiteSpace(updatedJson))
                {
                    Debug.LogError("Generated JSON is empty after adding UniTask dependency");
                    EditorUtility.DisplayDialog("Installation Failed",
                        "Failed to generate valid JSON after adding UniTask dependency.\n\n" +
                        $"Please install UniTask manually: {_UNITASK_GIT_URL}",
                        "OK");
                    return;
                }

                // Create backup of original manifest before writing
                var backupPath = manifestPath + ".backup";
                File.Copy(manifestPath, backupPath, true);
                Debug.Log($"Created backup of manifest.json at: {backupPath}");

                // Write back to manifest
                File.WriteAllText(manifestPath, updatedJson);

                // Mark setup as complete
                EditorPrefs.SetBool(_SETUP_COMPLETE_KEY, true);

                Debug.Log("UniTask installation added to manifest. Unity will refresh packages automatically.");
                Debug.Log("If something goes wrong, you can restore from: " + backupPath);

                // Force package refresh
                UnityEditor.PackageManager.Client.Resolve();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to install UniTask: {ex.Message}");
                EditorUtility.DisplayDialog("Installation Failed",
                    $"Failed to install UniTask automatically.\n\nError: {ex.Message}\n\nPlease install manually: {_UNITASK_GIT_URL}",
                    "OK");
            }
        }

        private static bool IsUniTaskInstalled()
        {
            var request = UnityEditor.PackageManager.Client.List();
            while (!request.IsCompleted)
            {
                System.Threading.Thread.Sleep(10);
            }

            if (request.Status == UnityEditor.PackageManager.StatusCode.Success)
            {
                foreach (var package in request.Result)
                {
                    if (package.name == _UNITASK_PACKAGE_ID)
                        return true;
                }
            }

            return false;
        }

        [MenuItem("Window/Pandora/Package Setup")]
        public static void ShowSetupMenu()
        {
            // Reset a completion dialog flag so the user can see status again
            EditorPrefs.SetBool(_SETUP_COMPLETE_KEY + "_shown", false);

            // Reset setup and recheck
            EditorPrefs.SetBool(_SETUP_COMPLETE_KEY, false);
            CheckSetup();
        }
    }
}
#endif
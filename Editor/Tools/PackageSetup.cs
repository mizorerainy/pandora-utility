#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor.Build;

// ReSharper disable once CheckNamespace
namespace MizoreRainy.Pandora.Editor.Tools
{
    // A new window for managing Pandora package settings.
    /// <summary>
    /// Provides a custom Editor Window to manage Pandora package settings within the Unity Editor.
    /// </summary>
    /// <remarks>
    /// This window is accessible via the Unity Editor menu under "Window/Pandora/Settings".
    /// It allows developers to handle various configurations and settings for the Pandora package.
    /// </remarks>
    public class PandoraSettingsWindow : EditorWindow
    {
        // Config Loader Settings
        /// <summary>
        /// Key used to store and retrieve the configuration setting for enabling or disabling
        /// the auto-initialization of Pandora's ConfigLoader system.This key is used
        /// in the Unity Editor preferences to persist the user's choice across sessions.
        /// If the value associated with this key is set to <c>true</c>, the ConfigLoader
        /// will automatically initialize before the first scene loads.
        /// Otherwise, manual
        /// initialization via <c>ConfigLoader.InitializeAsync()</c> is required.
        /// Default value is <c>false</c>, indicating manual initialization.
        /// </summary>
        private const string _CONFIG_AUTO_INIT_KEY = "Pandora.ConfigLoader.AutoInitEnabled";

        /// <summary>
        /// Represents the auto-initialization state of the Config Loader in the Pandora package.
        /// When enabled, the Config Loader will automatically initialize before the first scene loads.
        /// When disabled, manual initialization is required via ConfigLoader.InitializeAsync().
        /// The value of this variable is persisted using Unity's EditorPrefs system and can be modified
        /// through the Pandora Settings window in the Unity editor.
        /// </summary>
        private bool _ConfigLoaderAutoInit;

        /// <summary>
        /// Displays the Pandora Settings window in the Unity Editor.
        /// </summary>
        /// <remarks>
        /// This method opens a custom EditorWindow named "Pandora Settings" that can be used
        /// to manage settings specific to the Pandora package.
        /// The window can be accessed
        /// from the Unity menu under "Window/Pandora/Settings".
        /// </remarks>
        [MenuItem("Window/Pandora/Settings")]
        public static void ShowWindow()
        {
            GetWindow<PandoraSettingsWindow>("Pandora Settings");
        }

        /// <summary>
        /// Callback method invoked when the editor window is enabled or opened.
        /// Used to initialize or load required settings for the Pandora Settings Window.
        /// </summary>
        private void OnEnable()
        {
            // Load saved settings
            _ConfigLoaderAutoInit = EditorPrefs.GetBool(_CONFIG_AUTO_INIT_KEY, false); // Default to false (manual init)
        }

        /// <summary>
        /// Called to render and handle GUI events inside the custom EditorWindow for Pandora settings.
        /// This method is responsible for drawing the user interface and updating settings based on user inputs.
        /// The GUI includes a section for configuring the Config Loader module, allowing the user to toggle
        /// automatic initialization.
        /// Changes to the settings are persisted using EditorPrefs and trigger
        /// updates to scripting define symbols.
        /// Displays informational and user-friendly labels, toggles, and messages to assist in configuring
        /// Pandora settings.
        /// </summary>
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Pandora Module Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // --- Config Loader Section ---
            EditorGUILayout.LabelField("Config Loader", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _ConfigLoaderAutoInit = EditorGUILayout.Toggle(new GUIContent("Enable Auto-Initialization", "If enabled, the ConfigLoader will initialize automatically before the first scene loads. If disabled, you must call ConfigLoader.InitializeAsync() manually."), _ConfigLoaderAutoInit);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(_CONFIG_AUTO_INIT_KEY, _ConfigLoaderAutoInit);
                PackageSetup.UpdateScriptingDefines();
                Debug.Log($"Config Loader Auto-Initialization set to: {_ConfigLoaderAutoInit}");
            }
            EditorGUILayout.HelpBox("Auto-Initialization is recommended for most projects. Disable this only if you need full control over the startup sequence.", MessageType.Info);
        }
    }


    /// <summary>
    /// The PackageSetup class provides tools for managing and updating Pandora-specific package configurations
    /// and scripting define symbols. It ensures the proper integration of required dependencies and settings
    /// for Pandora modules within a Unity project.
    /// </summary>
    /// <remarks>
    /// This class is initialized automatically when the Unity Editor loads.
    /// It provides utilities to handle
    /// scripting define symbols, ensure compatibility with external libraries such as UniTask, and manage
    /// settings like the auto-initialization of the Config Loader.
    /// </remarks>
    /// <example>
    /// Use the "Pandora/Package Setup" menu item in the Unity Editor to invoke necessary setup processes
    /// for the Pandora package.
    /// </example>
    /// <see>
    ///     <cref>UnityEditor.PlayerSettings.GetScriptingDefineSymbolsForGroup</cref>
    /// </see>
    /// <see>
    ///     <cref>UnityEditor.PlayerSettings.SetScriptingDefineSymbolsForGroup</cref>
    /// </see>
    [InitializeOnLoad]
    public static class PackageSetup
    {
        // UniTask constants
        /// <summary>
        /// Represents the unique identifier for the UniTask package in Unity's Package Manager.
        /// This constant is used to specify the package ID for UniTask when configuring
        /// or modifying the project's package dependencies.
        /// </summary>
        private const string _UNITASK_PACKAGE_ID = "com.cysharp.unitask";

        /// <summary>
        /// Constant string representing the Git URL of the UniTask package repository.
        /// This URL is used for automatic UniTask installation during the setup process when configuring
        /// the Pandora Network Utility package.
        /// The URL includes the specific path to the UniTask source
        /// within the repository.
        /// </summary>
        private const string _UNITASK_GIT_URL = "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask";

        /// <summary>
        /// Represents a scripting define symbol used to identify
        /// whether the Cysharp UniTask package is installed and available.
        /// </summary>
        /// <remarks>
        /// The symbol is used during build configuration
        /// to conditionally include or exclude code that depends on the UniTask package.
        /// It is specifically managed within the context of Pandora package settings and setup.
        /// </remarks>
        private const string _UNITASK_DEFINE_SYMBOL = "HAVE_CYSHARP_UNITASK";
        
        // Config Loader constants
        /// <summary>
        /// Represents the key used for storing and retrieving the configuration setting
        /// that determines whether automatic initialization of the Pandora Config Loader
        /// is enabled or disabled.
        /// </summary>
        /// <remarks>
        /// This key is used in conjunction with Unity's EditorPrefs to manage the state
        /// of the "Auto Initialization" feature.
        /// If the associated value is set to true,
        /// the Pandora Config Loader will automatically initialize upon application startup.
        /// </remarks>
        private const string _CONFIG_AUTO_INIT_KEY = "Pandora.ConfigLoader.AutoInitEnabled";

        /// <summary>
        /// Represents the scripting define symbol for enabling or disabling the
        /// automatic initialization of the Config Loader in the Pandora package.
        /// This is used to dynamically set scripting define symbols based on
        /// editor preferences.
        /// </summary>
        private const string _CONFIG_LOADER_AUTO_INIT_SYMBOL = "CONFIG_LOADER_AUTO_INIT";

        // Setup tracking
        /// <summary>
        /// Constant string key used to track whether the Pandora Network Utility package setup
        /// process has been completed.
        /// The value is stored in the EditorPrefs to persist
        /// the setup state across Unity editor sessions.
        /// </summary>
        private const string _SETUP_COMPLETE_KEY = "PandoraNetworkUtility.SetupComplete";

        /// <summary>
        /// Provides functionality to manage the setup and configuration
        /// of the Pandora package within the Unity Editor environment.
        /// Handles scripting define symbols, dependency checks, and
        /// user setup prompts for proper package initialization.
        /// </summary>
        static PackageSetup()
        {
            EditorApplication.delayCall += CheckSetup;
        }

        /// <summary>
        /// Verifies the setup status of the Pandora package and ensures required dependencies and configurations
        /// are in place.
        /// This method is triggered during the editor's initialization process.
        /// If the setup is incomplete or certain dependencies (e.g., UniTask) are missing,
        /// prompts the relevant dialogs to guide the user through the setup process.
        /// Updates editor preferences to track completion status and modifies
        /// scripting define symbols as necessary.
        /// </summary>
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

        /// <summary>
        /// Updates the scripting define symbols for the currently selected build target group in Unity.
        /// This method ensures that scripting define symbols are synchronized based on the installed packages,
        /// configurations, or other relevant conditions.
        /// It is primarily used to conditionally include or exclude
        /// features or dependencies at compile time, based on project settings and environment.
        /// Specifically:
        /// - Adds or removes the symbol for UniTask support depending on its installation status.
        /// -
        /// Adds or removes the symbol for the Config Loader's auto-initialization
        /// depending on whether it is enabled.
        /// If changes are made to the scripting define symbols, they are persisted, and a debug message is logged
        /// to indicate the update.
        /// </summary>
        /// <remarks>
        /// This method considers both Unity versions older than 2023.1 and Unity 2023.1 or newer for
        /// compatibility with the scripting define symbols API.
        /// It does not update scripting defines if the selected build target group is unknown.
        /// </remarks>
        public static void UpdateScriptingDefines()
        {
            var target = EditorUserBuildSettings.selectedBuildTargetGroup;

            // Handle unknown/unsupported build targets
            if (target == BuildTargetGroup.Unknown)
            {
                Debug.LogWarning("Cannot update scripting defines for unknown build target.");
                return;
            }

#if UNITY_2023_1_OR_NEWER
            var namedTarget = NamedBuildTarget.FromBuildTargetGroup(target);
            var definesString = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
#else
            var definesString = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);
#endif

            var definesList = new HashSet<string>(
                string.IsNullOrEmpty(definesString)
                    ? Array.Empty<string>()
                    : definesString.Split(';', StringSplitOptions.RemoveEmptyEntries)
            );

            bool definesChanged = false;

            // Handle UniTask define
            definesChanged |= SetDefine(ref definesList, _UNITASK_DEFINE_SYMBOL, IsUniTaskInstalled());

            // Handle Config Loader define
            definesChanged |= SetDefine(ref definesList, _CONFIG_LOADER_AUTO_INIT_SYMBOL, EditorPrefs.GetBool(_CONFIG_AUTO_INIT_KEY, false));

            if (definesChanged)
            {
#if UNITY_2023_1_OR_NEWER
                PlayerSettings.SetScriptingDefineSymbols(namedTarget, string.Join(";", definesList));
#else
                PlayerSettings.SetScriptingDefineSymbolsForGroup(target, string.Join(";", definesList));
#endif
                Debug.Log($"Pandora package scripting defines updated for {target}.");
            }
        }

        /// Modifies the presence of a definition symbol in a collection of scripting define symbols
        /// based on whether the symbol should exist.
        /// <param name="_defines">A reference to the set of scripting defines symbols.</param>
        /// <param name="_define">The definition symbols to add or remove.</param>
        /// <param name="_shouldExist">Indicates whether the definition symbol should be added (true)
        /// or removed (false).</param>
        /// <returns>True if the collection of define symbols was modified; otherwise, false.</returns>
        private static bool SetDefine(ref HashSet<string> _defines, string _define, bool _shouldExist)
        {
            bool hasDefine = _defines.Contains(_define);
            if (_shouldExist && !hasDefine)
            {
                _defines.Add(_define);
                return true;
            }
            if (!_shouldExist && hasDefine)
            {
                _defines.Remove(_define);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Displays a setup dialog for the Pandora Network Utility package.
        /// This dialog informs the user about the requirements of the package
        /// and provides options to automate or manually complete the setup process,
        /// such as installing the UniTask dependency.
        /// </summary>
        /// <remarks>
        /// The dialog offers three options:
        /// - Install UniTask: Automatically installs the UniTask dependency.
        /// - Skip:
        /// Allows the user to skip the installation, with a reminder to complete the setup manually.
        /// - Cancel: Cancels the setup process and informs the user they can rerun the setup later.
        /// </remarks>
        /// <example>
        /// This method is called during the setup process
        /// if it is detected that the required UniTask dependency is not installed.
        /// </example>
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

        /// <summary>
        /// Displays a completion dialog to notify the user that the Pandora package setup is complete.
        /// The dialog provides confirmation that all required components,
        /// such as UniTask, are installed and configured.
        /// This dialog is only shown once unless reset manually via the setup menu.
        /// </summary>
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

        /// <summary>
        /// Installs the UniTask package by modifying the project's manifest.json file to include
        /// the UniTask package dependency from the UniTask Git URL.
        /// If UniTask is already listed
        /// as a dependency, the method ensures no duplicate installation occurs and skips the process.
        /// </summary>
        /// <remarks>
        /// This method is designed to streamline the setup process for the Pandora Network Utility,
        /// ensuring that the required UniTask library is available.
        /// It operates by directly editing the manifest.json file stored in the project's Packages folder.
        /// </remarks>
        /// <exception cref="System.Exception">Occurs if there is an error while reading,
        /// modifying, or writing the manifest.json file,
        /// or if the file cannot be located at the expected path.</exception>
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
            catch (Exception ex)
            {
                Debug.LogError($"Failed to install UniTask: {ex.Message}");
            }
        }

        /// Determines whether the UniTask package is installed by inspecting the manifest.json file.
        /// <returns>
        /// True if the UniTask package is found in the manifest.json file; otherwise, false.
        /// </returns>
        private static bool IsUniTaskInstalled()
        {
            // This is a simplified check. For a robust solution, consider parsing manifest.json
            // or using PackageManager API correctly. The original code had a synchronous block, which is not ideal.
            // A simple check for the definition symbol might be enough if the setup process is reliable.
            var manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
            if(File.Exists(manifestPath))
            {
                var manifestText = File.ReadAllText(manifestPath);
                return manifestText.Contains(_UNITASK_PACKAGE_ID);
            }
            return false;
        }

        /// <summary>
        /// Opens the Pandora Package Setup menu and initiates the setup process.
        /// </summary>
        /// <remarks>
        /// Resets the setup state by clearing relevant EditorPrefs keys and invokes the setup check to determine
        /// if necessary dependencies or configurations are missing.
        /// If additional input or action is required from the user,
        /// dialogs will be displayed accordingly.
        /// This method is accessible via the Unity Editor menu.
        /// </remarks>
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

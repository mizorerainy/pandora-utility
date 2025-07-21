#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace MizoreRainy.Pandora.Editor.Tools
{
	/// <summary>
	///     A custom Editor Window for managing the settings of the Pandora package within the Unity Editor.
	/// </summary>
	/// <remarks>
	///     This window provides an interface to configure and manage options related to the Pandora package.
	///     The window can be accessed via the Unity Editor menu at "Window/Pandora/Settings".
	/// </remarks>
	public class PandoraSettingsWindow : EditorWindow
	{
		/// <summary>
		///     A constant string key used
		///     to enable or disable the auto-initialization of the ConfigLoader in the Pandora package.
		/// </summary>
		/// <remarks>
		///     The value for the key is stored in Unity's EditorPrefs
		///     and determines whether the ConfigLoader automatically initializes
		///     before the first scene is loaded.
		///     This setting is recommended for most projects to streamline the configuration process.
		///     If disabled,
		///     manual initialization of the ConfigLoader using <c>ConfigLoader.InitializeAsync()</c>
		///     will be required.
		/// </remarks>
		/// <value>
		///     The key is named "Pandora.ConfigLoader.AutoInitEnabled"
		///     and is used internally to save and retrieve the auto-initialization state.
		/// </value>
		private const string _CONFIG_AUTO_INIT_KEY = "Pandora.ConfigLoader.AutoInitEnabled";

		/// <summary>
		///     Represents the state of the auto-initialization feature for the ConfigLoader.
		///     Determines whether the ConfigLoader will automatically initialize before the first scene loads.
		/// </summary>
		/// <remarks>
		///     By default,
		///     this value is initialized from the Editor Preferences using the key "Pandora.ConfigLoader.AutoInitEnabled".
		///     Enabling this feature is recommended for most projects,
		///     as it ensures that the ConfigLoader is ready when the application starts.
		///     Disabling it provides more control over the initialization process,
		///     requiring manual initialization via ConfigLoader.InitializeAsync().
		/// </remarks>
		private bool _ConfigLoaderAutoInit;

		/// <summary>
		///     Represents the editor preference key used to store and retrieve
		///     whether the YAML format is enabled for configuration files in the Pandora package.
		///     When enabled, the ConfigLoader uses "config.yaml" instead of "config.ini".
		///     Requires the installation of the VYaml package for proper functionality.
		/// </summary>
		private const string _CONFIG_USE_YAML_KEY = "Pandora.ConfigLoader.UseYaml";

		/// <summary>
		///     Indicates whether YAML format (.yaml) is enabled for the Pandora ConfigLoader.
		///     When set to true, the ConfigLoader uses "config.yaml" as the configuration file format.
		///     This requires the VYaml package to be installed.
		/// </summary>
		private bool _UseYaml;

		/// <summary>
		///     Indicates whether the VYaml package is installed in the project.
		///     Used to determine if YAML configuration format support is available.
		/// </summary>
		private bool _IsYamlInstalled;

		/// <summary>
		///     Displays the Pandora Settings window in the Unity Editor.
		///     This method opens a dedicated window for managing Pandora package settings.
		/// </summary>
		[MenuItem("Window/Pandora/Settings")]
		public static void ShowWindow()
		{
			GetWindow<PandoraSettingsWindow>("Pandora Settings");
		}

		/// <summary>
		///     Called when the window is enabled or opened in the Unity Editor.
		///     This method loads and applies previously saved settings related to
		///     the Pandora package configuration, including settings for the
		///     Config Loader and YAML package usage.
		/// </summary>
		private void OnEnable()
		{
			// Load saved settings
			_ConfigLoaderAutoInit = EditorPrefs.GetBool(_CONFIG_AUTO_INIT_KEY, false);
			_UseYaml = EditorPrefs.GetBool(_CONFIG_USE_YAML_KEY, false);

			_IsYamlInstalled = PackageSetup.IsPackageInstalled(PackageSetup.YAML_PACKAGE_ID);
		}

		/// <summary>
		///     Renders and manages the GUI elements for the Pandora Settings window in the Unity Editor.
		///     Unity calls this method automatically when the EditorWindow needs to be redrawn.
		/// </summary>
		/// <remarks>
		///     The GUI contains settings for Pandora's ConfigLoader, such as enabling or disabling
		///     auto-initialization and toggling YAML file support.
		///     It also provides installation options
		///     for required dependencies like the VYaml package.
		///     The method dynamically updates preferences and applies scripting define symbols when
		///     configuration changes are made.
		///     It also dynamically handles GUI states based on whether
		///     the editor is currently compiling or if required dependencies are installed.
		///     Ensure this method is used solely within the Unity Editor context.
		/// </remarks>
		private void OnGUI()
		{
			if (EditorApplication.isCompiling)
			{
				EditorGUILayout.HelpBox("Editor is compiling, please wait...", MessageType.Info);
				EditorGUI.BeginDisabledGroup(true);
			}

			EditorGUILayout.LabelField("Pandora Module Settings", EditorStyles.boldLabel);
			EditorGUILayout.Space();

			// --- Config Loader Section ---
			EditorGUILayout.LabelField("Config Loader", EditorStyles.boldLabel);
			EditorGUI.BeginChangeCheck();
			_ConfigLoaderAutoInit = EditorGUILayout.Toggle(
				new GUIContent("Enable Auto-Initialization",
					"If enabled, the ConfigLoader will initialize automatically before the first scene loads. If disabled, you must call ConfigLoader.InitializeAsync() manually."),
				_ConfigLoaderAutoInit);
			if (EditorGUI.EndChangeCheck())
			{
				EditorPrefs.SetBool(_CONFIG_AUTO_INIT_KEY, _ConfigLoaderAutoInit);
				PackageSetup.UpdateScriptingDefines();
				Debug.Log($"Config Loader Auto-Initialization set to: {_ConfigLoaderAutoInit}");
				AssetDatabase.Refresh();
			}

			EditorGUILayout.HelpBox(
				"Auto-Initialization is recommended for most projects. Disable this only if you need full control over the startup sequence.",
				MessageType.Info);

			EditorGUILayout.Space();

			// --- YAML Configuration Section ---
			EditorGUILayout.LabelField("Configuration Format", EditorStyles.boldLabel);

			if (!_IsYamlInstalled)
			{
				EditorGUILayout.HelpBox("Install the VYaml package to enable YAML configuration support.",
					MessageType.Info);
				if (GUILayout.Button("Install VYaml")) PackageSetup.InstallVYaml();
			}

			EditorGUI.BeginDisabledGroup(!_IsYamlInstalled);
			EditorGUI.BeginChangeCheck();
			_UseYaml = EditorGUILayout.Toggle(
				new GUIContent("Use YAML Format (.yaml)",
					"If enabled, the ConfigLoader will use 'config.yaml' instead of ' config.ini'. This requires the VYaml package."),
				_UseYaml);
			if (EditorGUI.EndChangeCheck())
			{
				EditorPrefs.SetBool(_CONFIG_USE_YAML_KEY, _UseYaml);
				PackageSetup.UpdateScriptingDefines();
				Debug.Log($"Use YAML Format set to: {_UseYaml}");
				AssetDatabase.Refresh();
			}

			EditorGUI.EndDisabledGroup();

			if (EditorApplication.isCompiling) EditorGUI.EndDisabledGroup();
		}
	}


	/// <summary>
	///     Provides tools for configuring and managing package settings for the Pandora project in Unity.
	/// </summary>
	[InitializeOnLoad]
	public static class PackageSetup
	{
		/// <summary>
		///     The constant identifier for the UniTask package used in the project.
		///     This string represents the package name "com.cysharp.unitask" in the Unity Package Manager.
		///     It is used to check for the installation status of the UniTask package
		///     and manage scripting define symbols accordingly.
		/// </summary>
		private const string _UNITASK_PACKAGE_ID = "com.cysharp.unitask";

		/// <summary>
		///     The Git URL for the UniTask package used within the Pandora utility setup.
		///     This URL points to the specific location of the UniTask source within the repository.
		/// </summary>
		private const string _UNITASK_GIT_URL =
			"https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask";

		/// <summary>
		///     Represents the package identifier for the VYaml Unity package.
		/// </summary>
		/// <remarks>
		///     This constant is used within the context of the Pandora package setup to verify
		///     whether the VYaml package (specified by its ID) is installed in the Unity project.
		///     VYaml is a Unity-compatible YAML parsing library.
		/// </remarks>
		internal const string YAML_PACKAGE_ID = "jp.hadashikick.vyaml";

		/// <summary>
		///     Represents the Git URL pointing to the VYaml package repository.
		///     This constant is used to fetch and install the VYaml package from the specified location.
		/// </summary>
		private const string _YAML_GIT_URL =
			"https://github.com/hadashiA/VYaml.git?path=VYaml.Unity/Assets/VYaml#0.27.1";

		/// <summary>
		///     A constant string key used for enabling or disabling the automated initialization
		///     of the Pandora configuration loader system in the Unity Editor.
		///     The configuration associated with this key is stored
		///     and managed via <see cref="UnityEditor.EditorPrefs" />.
		/// </summary>
		private const string _CONFIG_AUTO_INIT_KEY = "Pandora.ConfigLoader.AutoInitEnabled";

		/// <summary>
		///     A constant string representing the scripting defines the symbol for enabling automatic initialization
		///     of the Config Loader in the Pandora package.
		/// </summary>
		/// <remarks>
		///     This symbol is used during build or script compilation to toggle features related to automatic
		///     configuration loader initialization.
		///     Its value can be added or removed dynamically in the
		///     scripting define a symbol list, based on user preferences or package settings.
		/// </remarks>
		private const string _CONFIG_LOADER_AUTO_INIT_SYMBOL = "CONFIG_LOADER_AUTO_INIT";

		/// <summary>
		///     Specifies the EditorPrefs key used to determine whether YAML-based configuration loading
		///     is enabled in the Pandora ConfigLoader system.
		/// </summary>
		/// <remarks>
		///     This key is used in conjunction with the scripting define symbol "USE_YAML_CONFIG" to enable
		///     or disable YAML support dynamically during development.
		///     The value of this key is a boolean,
		///     stored in Unity's Editor Preferences,
		///     representing the state of YAML configuration usage.
		///     Changing this key effects how the Pandora package processes configuration settings.
		/// </remarks>
		private const string _CONFIG_USE_YAML_KEY = "Pandora.ConfigLoader.UseYaml";

		/// <summary>
		///     A constant that represents the scripting defines the symbol for enabling YAML configuration
		///     in Pandora Config Loader.
		///     When this symbol is defined, the system will use YAML
		///     for configuration-related processes if the appropriate YAML package is installed and
		///     configured.
		/// </summary>
		/// <remarks>
		///     This defines symbol is automatically managed based on the package installation state
		///     and user preferences for YAML usage in the configuration system.
		///     It helps control
		///     conditional compilation for features dependent on YAML functionality.
		/// </remarks>
		private const string _CONFIG_USE_YAML_SYMBOL = "USE_YAML_CONFIG";

		/// <summary>
		///     A constant string key used to track whether the package setup process
		///     for the Pandora Network Utility has been completed successfully.
		///     This key is used with Unity's `EditorPrefs` to store a boolean
		///     flag indicating the setup status and prevent redundant setup operations.
		/// </summary>
		private const string _SETUP_COMPLETE_KEY = "PandoraUtility.SetupComplete";

		/// <summary>
		///     This class is responsible for managing and automating setup tasks for the Pandora package in Unity.
		///     It includes handling package dependencies,
		///     updating scripting define symbols, and user interaction for setup confirmation.
		/// </summary>
		static PackageSetup()
		{
			EditorApplication.delayCall += CheckSetup;
		}

		/// <summary>
		///     Validates the setup of necessary packages and updates scripting define symbols as required.
		///     The method performs checks for specific package installations, such as UniTask,
		///     and displays a setup or completion dialog based on the installation status.
		///     It also ensures
		///     that relevant project settings are properly configured for the use of the Pandora Utility.
		/// </summary>
		private static void CheckSetup()
		{
			UpdateScriptingDefines();

			if (EditorPrefs.GetBool(_SETUP_COMPLETE_KEY, false))
			{
				if (IsPackageInstalled(_UNITASK_PACKAGE_ID)) ShowCompletionDialog();
				return;
			}

			if (!IsPackageInstalled(_UNITASK_PACKAGE_ID))
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
		///     Updates the scripting define symbols for the current build target group based on specific conditions
		///     such as installed packages and editor preferences.
		///     This function ensures that the correct definitions
		///     are added or removed as needed,
		///     allowing for conditional compilation in the Unity project.
		/// </summary>
		/// <remarks>
		///     This method modifies the scripting define symbols, which are preprocessor directives used during
		///     code compilation to include or exclude code based on conditions.
		///     The method checks for the presence
		///     of certain packaged read editor preferences
		///     to determine the required definition symbols.
		///     If any changes are detected in the required scripting define symbols, the method applies the updates
		///     and logs a message
		///     indicating the completion of the update for the currently selected build target group.
		/// </remarks>
		public static void UpdateScriptingDefines()
		{
			var target = EditorUserBuildSettings.selectedBuildTargetGroup;
			if (target == BuildTargetGroup.Unknown) return;

#if UNITY_2023_1_OR_NEWER
			var namedTarget = NamedBuildTarget.FromBuildTargetGroup(target);
			var definesString = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
#else
            var definesString = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);
#endif

			var definesList =
				new HashSet<string>(definesString.Split(';').Where(_s => !string.IsNullOrEmpty(_s)).ToList());
			var definesChanged = false;

			// FIX: Removed manual handling for package defines, as versionDefines in .asmdef is the correct approach.
			definesChanged |= SetDefine(ref definesList, _CONFIG_LOADER_AUTO_INIT_SYMBOL,
				EditorPrefs.GetBool(_CONFIG_AUTO_INIT_KEY, false));
			definesChanged |= SetDefine(ref definesList, _CONFIG_USE_YAML_SYMBOL,
				EditorPrefs.GetBool(_CONFIG_USE_YAML_KEY, false));

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

		/// Sets or removes a scripting define symbol in the provided HashSet of definitions based on a specified condition.
		/// <param name="_defines">
		///     A reference to the HashSet of strings representing currently defined scripting symbols.
		/// </param>
		/// <param name="_define">
		///     The scripting define symbol to be added or removed.
		/// </param>
		/// <param name="_shouldExist">
		///     A boolean indicating whether the scripting define symbol should exist in the HashSet.
		/// </param>
		/// <return>
		///     Returns a boolean indicating if the definitions were modified
		///     (true if the symbol was added or removed, otherwise false).
		/// </return>
		private static bool SetDefine(ref HashSet<string> _defines, string _define, bool _shouldExist)
		{
			var hasDefine = _defines.Contains(_define);
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
		///     Displays a setup dialog box for installing required Unity packages, specifically UniTask.
		///     This dialog prompts the user with the option to automatically install the UniTask package,
		///     skip the installation to handle it manually, or cancel the setup process altogether.
		///     If the user selects the installation option, the UniTask package will be installed asynchronously.
		///     If the user chooses to skip,
		///     the setup state will be marked as complete without performing the installation.
		/// </summary>
		private static void ShowSetupDialog()
		{
			var message =
				"Pandora Network Utility Setup\n\nThis package requires UniTask to function properly.\n\nWould you like to install UniTask automatically?";
			var result = EditorUtility.DisplayDialogComplex("Pandora Network Utility Setup", message, "Install UniTask",
				"Skip (Install Manually)", "Cancel");
			switch (result)
			{
				case 0: InstallPackageAsync(_UNITASK_PACKAGE_ID, _UNITASK_GIT_URL).GetAwaiter(); break;
				case 1: EditorPrefs.SetBool(_SETUP_COMPLETE_KEY, true); break;
			}
		}

		/// <summary>
		///     Displays a completion dialog to indicate that the Pandora Package setup has been successfully completed.
		///     Ensures that the message is shown only once per setup by leveraging EditorPrefs to track its state.
		/// </summary>
		private static void ShowCompletionDialog()
		{
			if (!EditorPrefs.GetBool(_SETUP_COMPLETE_KEY + "_shown", false))
			{
				EditorUtility.DisplayDialog("Setup Complete",
					"Pandora Package Setup Complete!\n\nAll required dependencies are installed.", "OK");
				EditorPrefs.SetBool(_SETUP_COMPLETE_KEY + "_shown", true);
			}
		}

		/// <summary>
		///     Installs the VYaml package required for enabling YAML configuration support within the Unity project.
		/// </summary>
		/// <remarks>
		///     This method uses the Unity Package Manager to install the specified version of the VYaml package
		///     from its Git URL.
		///     It displays a progress bar during the installation process and handles success
		///     or failure outcomes accordingly.
		///     The VYaml package enables support for advanced YAML-based
		///     configuration within the Pandora tools.
		/// </remarks>
		/// <seealso cref="PackageSetup.UpdateScriptingDefines" />
		internal static async void InstallVYaml()
		{
			try
			{
				await InstallPackageAsync(YAML_PACKAGE_ID, _YAML_GIT_URL);
			}
			catch (Exception)
			{
				// Ignored
			}
		}

		/// <summary>
		///     Installs a Unity package from a specified package ID and version or URL.
		///     Displays a progress bar during installation, provides a success or error dialog,
		///     and refreshes the Asset Database after installation.
		/// </summary>
		/// <param name="_packageId">The ID of the package to install.</param>
		/// <param name="_packageVersionOrUrl">
		///     The version or URL of the package to install.
		///     Use a Git URL for packages hosted on external repositories.
		/// </param>
		/// <returns>A task representing the asynchronous package installation operation.</returns>
		private static async Task InstallPackageAsync(string _packageId, string _packageVersionOrUrl)
		{
			EditorUtility.DisplayProgressBar("Package Installation", $"Installing {_packageId}...", 0f);
			var request = Client.Add(_packageVersionOrUrl);

			while (!request.IsCompleted) await Task.Delay(100);

			EditorUtility.ClearProgressBar();

			if (request.Status == StatusCode.Success)
			{
				Debug.Log($"Successfully installed package: {request.Result.name}");
				EditorUtility.DisplayDialog("Installation Success", $"Successfully installed {_packageId}.", "OK");
				UpdateScriptingDefines();
				AssetDatabase.Refresh();
			}
			else if (request.Status >= StatusCode.Failure)
			{
				Debug.LogError(
					$"Failed to install package {_packageId} from '{_packageVersionOrUrl}'. Error: {request.Error.message}");
				EditorUtility.DisplayDialog("Installation Failed",
					$"Failed to install package '{_packageId}'.\n\nError: {request.Error.message}\n\nPlease check the console for details.",
					"OK");
			}
		}

		/// <summary>
		///     Checks if a Unity package with the specified package ID is installed.
		/// </summary>
		/// <param name="_packageId">The unique identifier of the package to check.</param>
		/// <returns>True if the package is installed, otherwise false.</returns>
		internal static bool IsPackageInstalled(string _packageId)
		{
			var request = Client.List(true, false);
			while (!request.IsCompleted)
			{
			}

			if (request.Status == StatusCode.Success) return request.Result.Any(_p => _p.name == _packageId);
			return false;
		}

		/// <summary>
		///     Displays the Pandora package setup menu in the Unity Editor.
		///     Ensures that required packages are checked or installed and initializes package-specific
		///     setup tasks if necessary.
		///     Resets and reprocesses setup progress flags.
		/// </summary>
		[MenuItem("Window/Pandora/Package Setup")]
		public static void ShowSetupMenu()
		{
			EditorPrefs.SetBool(_SETUP_COMPLETE_KEY + "_shown", false);
			EditorPrefs.SetBool(_SETUP_COMPLETE_KEY, false);
			CheckSetup();
		}

		/// <summary>
		///     Forces the re-evaluation and re-compilation of Pandora scripting define symbols.
		///     This method verifies the current state of required packages, editor preferences, and settings,
		///     ensures that the appropriate scripting define symbols are added or removed accordingly,
		///     and triggers a refresh of the Asset Database.
		/// </summary>
		/// <remarks>
		///     Typically used to ensure that the project scripting define symbols are in sync
		///     with the installed packages and configuration settings.
		/// </remarks>
		[MenuItem("Window/Pandora/Force Recompile Defines")]
		public static void ForceRecompileDefines()
		{
			Debug.Log("Forcing a re-check and update of Pandora scripting define symbols...");
			UpdateScriptingDefines();
			AssetDatabase.Refresh();
			Debug.Log("Recompile defines complete.");
		}
	}
}
#endif
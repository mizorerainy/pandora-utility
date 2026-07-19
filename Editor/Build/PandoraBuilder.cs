#if UNITY_6000_0_OR_NEWER
using UnityEditor.Build.Profile;
#endif
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MizoreRainy.Pandora.BuildUtility
{
    /// <summary>
    /// Exposes public utility methods to trigger managed builds programmatically via C#
    /// without relying on EditorWindow GUI components. Ideal for CLI/Headless execution.
    /// </summary>
    public static class PandoraBuilder
    {
        private const string SETTINGS_FILE_PATH = "Assets/Editor/ManagedBuildSettings.asset";

        /// <summary>
        /// Attempts to execute a build using the named profile within the project's Pandora configurations.
        /// </summary>
        /// <param name="profileName">The exact string Name of the ManagedBuildProfile to build.</param>
        /// <param name="outputDirectoryOverride">(Optional) Override the final exact root output path if needed.</param>
        /// <returns>True if the build and all post-build steps succeeded.</returns>
        public static bool BuildProfileByName(string profileName, string outputDirectoryOverride = null)
        {
            var settingsData = AssetDatabase.LoadAssetAtPath<BuildSettingsData>(SETTINGS_FILE_PATH);
            if (settingsData == null)
            {
                var guids = AssetDatabase.FindAssets("t:BuildSettingsData");
                if (guids.Length > 0)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    if (guids.Length > 1)
                    {
                        PandoraLogger.LogBuildWarning($"Multiple BuildSettingsData assets found! Defaulting to the one located at: {path}");
                    }
                    settingsData = AssetDatabase.LoadAssetAtPath<BuildSettingsData>(path);
                }
            }

            if (settingsData == null)
            {
                PandoraLogger.LogBuildError($"Settings asset not found at {SETTINGS_FILE_PATH}. Please ensure Pandora is configured.");
                return false;
            }

            var profile = settingsData.ManagedProfiles.FirstOrDefault(p => p.Name == profileName);
            if (profile == null)
            {
                PandoraLogger.LogBuildError($"No ManagedBuildProfile found with the name '{profileName}'.");
                return false;
            }

#if UNITY_6000_0_OR_NEWER
            if (profile.TargetProfile == null)
            {
                PandoraLogger.LogBuildError($"The profile '{profileName}' does not have a linked Unity Build Profile.");
                return false;
            }

            // Prepare Build Options
            var buildOptions = new BuildPlayerWithProfileOptions
            {
                buildProfile = profile.TargetProfile,
                options = BuildOptions.None
            };
            
            var activeTarget = EditorUserBuildSettings.activeBuildTarget;

            if (EditorUserBuildSettings.development) buildOptions.options |= BuildOptions.Development;
            if (EditorUserBuildSettings.allowDebugging) buildOptions.options |= BuildOptions.AllowDebugging;

            var productName = string.IsNullOrEmpty(profile.ProductNameOverride) ? PlayerSettings.productName : profile.ProductNameOverride;
            
            // Construct Output Path
            string finalPath;
            if (!string.IsNullOrEmpty(outputDirectoryOverride))
            {
                Directory.CreateDirectory(outputDirectoryOverride);
                finalPath = outputDirectoryOverride;
                // Add extension if it's standalone, etc. We just use the override folder for iOS and Android
                if (activeTarget == BuildTarget.StandaloneWindows || activeTarget == BuildTarget.StandaloneWindows64)
                    finalPath = Path.Combine(outputDirectoryOverride, productName + ".exe");
                else if (activeTarget == BuildTarget.StandaloneOSX)
                    finalPath = Path.Combine(outputDirectoryOverride, productName + ".app");
                else if (activeTarget == BuildTarget.Android)
                    finalPath = Path.Combine(outputDirectoryOverride, productName + ".apk");
            }
            else
            {
                // Core ConstructBuildPath logic replicated
                var rootPath = settingsData.BuildFolderPath;
                var platformFolder = GetPlatformFolder(activeTarget);
                var versionString = PlayerSettings.bundleVersion.Replace('.', '-');
                var timestamp = DateTime.Now.ToString("yyMMdd-HHmm");
                var artifactName = $"{productName.ToLower()}-{profile.BuildSuffix}-v{versionString}-{timestamp}";
                
                var finalDir = Path.Combine(rootPath, platformFolder, profile.Name, artifactName);

                if (activeTarget == BuildTarget.StandaloneWindows || activeTarget == BuildTarget.StandaloneWindows64 || activeTarget == BuildTarget.StandaloneOSX || activeTarget == BuildTarget.iOS)
                {
                    Directory.CreateDirectory(finalDir);
                    if (activeTarget == BuildTarget.iOS) finalPath = finalDir;
                    else finalPath = Path.Combine(finalDir, productName + GetBuildExtension(activeTarget));
                }
                else
                {
                    var parentDir = Path.GetDirectoryName(finalDir);
                    if (parentDir != null) Directory.CreateDirectory(parentDir);
                    finalPath = finalDir + GetBuildExtension(activeTarget);
                }
            }
            
            buildOptions.locationPathName = finalPath;

            PandoraLogger.LogBuild($"Starting CLI build for '{profile.Name}'. Output Path: {finalPath}");

            bool isBuilding = true;
            System.Threading.Thread progressThread = new System.Threading.Thread(() =>
            {
                System.Console.Write("[PandoraBuilder] Compiling ");
                while (isBuilding)
                {
                    System.Console.Write(".");
                    System.Threading.Thread.Sleep(5000);
                }
                System.Console.WriteLine();
            });
            progressThread.IsBackground = true;
            progressThread.Start();

            UnityEditor.Build.Reporting.BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(buildOptions);
            }
            finally
            {
                isBuilding = false;
                progressThread.Join();
            }

            if (report.summary.result == BuildResult.Succeeded)
            {
                PandoraLogger.LogBuild($"Build SUCCEEDED: {report.summary.outputPath} ({report.summary.totalSize / 1024 / 1024} MB)");
                
                // Execute Post Build Tasks
                if (profile.PostBuildTasks != null)
                {
                    foreach (var task in profile.PostBuildTasks)
                    {
                        if (task == null || !task.IsEnabled) continue;
                        try 
                        {
                            PandoraLogger.LogBuild($"Executing post-build task: {task.GetType().Name}");
                            task.Execute(profile, report.summary.outputPath); 
                        } 
                        catch (Exception ex) 
                        { 
                            PandoraLogger.LogBuildWarning($"Post-build task {task.GetType().Name} failed: {ex.Message}");
                        }
                    }
                }
                return true;
            }
            else
            {
                PandoraLogger.LogBuildError($"Build FAILED: {report.summary.result}. Errors: {report.summary.totalErrors}.");
                return false;
            }
#else
            PandoraLogger.LogBuildError("CLI Build via Pandora requires Unity 6000.0 or newer.");
            return false;
#endif
        }

        private static string GetPlatformFolder(BuildTarget target)
        {
            return target switch
            {
                BuildTarget.StandaloneWindows or BuildTarget.StandaloneWindows64 or BuildTarget.StandaloneOSX => "PC",
                BuildTarget.Android => "Android",
                BuildTarget.iOS => "iOS",
                _ => "Other"
            };
        }

        private static string GetBuildExtension(BuildTarget target)
        {
            return target switch
            {
                BuildTarget.StandaloneWindows or BuildTarget.StandaloneWindows64 => ".exe",
                BuildTarget.StandaloneOSX => ".app",
                BuildTarget.Android => ".apk",
                _ => ""
            };
        }
    }
}

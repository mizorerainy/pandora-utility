using System;
using System.IO;
using UnityEngine;
using MizoreRainy.Pandora;

namespace MizoreRainy.Pandora.BuildUtility
{
    [Serializable]
    public class CopyFilesTask : ManagedPostBuildTask
    {
        [Tooltip("Source folder or file. Can be absolute or relative to the project root (e.g., '../TestFolder').")]
        public string SourcePath = "";

        [Tooltip("If true, allows specifying a custom destination path relative to the build root.")]
        public bool SpecifyDestination = false;

        [Tooltip("Destination directory relative to the build root folder. Leave empty to copy directly into the build root.")]
        public string DestinationRelativePath = "";

        public override void Execute(ManagedBuildProfile profile, string buildOutputPath)
        {
            if (string.IsNullOrEmpty(SourcePath)) return;

            var buildRoot = Path.GetDirectoryName(buildOutputPath);
            if (string.IsNullOrEmpty(buildRoot)) return;

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var sourceFullPath = SourcePath;

            if (!Path.IsPathRooted(sourceFullPath))
            {
                sourceFullPath = Path.GetFullPath(Path.Combine(projectRoot, SourcePath));
            }

            if (!File.Exists(sourceFullPath) && !Directory.Exists(sourceFullPath))
            {
                PandoraLogger.LogBuildWarning($"Post-Build Copy: Source path does not exist: {sourceFullPath}");
                return;
            }

            var destRelative = SpecifyDestination ? DestinationRelativePath : "";
            var destRoot = string.IsNullOrEmpty(destRelative) ? buildRoot : Path.Combine(buildRoot, destRelative);

            try
            {
                if (Directory.Exists(sourceFullPath))
                {
                    var dirName = new DirectoryInfo(sourceFullPath).Name;
                    var destDir = Path.Combine(destRoot, dirName);
                    CopyDirectory(sourceFullPath, destDir);
                    PandoraLogger.LogBuild($"Post-Build Copy: Copied directory {sourceFullPath} to {destDir}");
                }
                else if (File.Exists(sourceFullPath))
                {
                    if (!Directory.Exists(destRoot)) Directory.CreateDirectory(destRoot);
                    var fileName = Path.GetFileName(sourceFullPath);
                    var destFile = Path.Combine(destRoot, fileName);
                    File.Copy(sourceFullPath, destFile, true);
                    PandoraLogger.LogBuild($"Post-Build Copy: Copied file {sourceFullPath} to {destFile}");
                }
            }
            catch (Exception ex)
            {
                PandoraLogger.LogBuildWarning($"Post-Build Copy failed for '{SourcePath}': {ex.Message}");
            }
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists) return;

            Directory.CreateDirectory(destDir);

            foreach (var file in dir.GetFiles())
            {
                var targetFilePath = Path.Combine(destDir, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            foreach (var subDir in dir.GetDirectories())
            {
                var newDestDir = Path.Combine(destDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestDir);
            }
        }
    }
}

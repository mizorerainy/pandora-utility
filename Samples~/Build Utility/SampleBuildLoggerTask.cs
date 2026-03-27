using System;
using System.IO;
using System.Text;
using UnityEngine;
using MizoreRainy.Pandora;

namespace MizoreRainy.Pandora.BuildUtility.Samples
{
    /// <summary>
    /// A sample post-build task that logs a completion message and visualizes
    /// the build output directory as an ASCII tree in the Unity Console.
    /// </summary>
    [Serializable]
    public class SampleBuildLoggerTask : ManagedPostBuildTask
    {
        [Tooltip("Optional prefix message to print before the file tree.")]
        public string CustomMessage = "Build fully complete. Here is the output file tree:";

        public override void Execute(ManagedBuildProfile profile, string buildOutputPath)
        {
            var buildRoot = Path.GetDirectoryName(buildOutputPath);
            if (string.IsNullOrEmpty(buildRoot) || !Directory.Exists(buildRoot))
            {
                PandoraLogger.LogBuildWarning("Build root is invalid or does not exist.");
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"[SampleBuildLoggerTask] {CustomMessage}");
            sb.AppendLine();
            
            BuildDirectoryTree(new DirectoryInfo(buildRoot), "", true, sb);

            PandoraLogger.LogBuild(sb.ToString());
        }

        private void BuildDirectoryTree(DirectoryInfo dirInfo, string indent, bool isLast, StringBuilder sb)
        {
            string marker = string.IsNullOrEmpty(indent) ? "" : (isLast ? "└── " : "├── ");
            sb.AppendLine($"{indent}{marker}{dirInfo.Name}/");

            string newIndent = indent + (isLast ? "    " : "│   ");
            
            var files = dirInfo.GetFiles();
            var dirs = dirInfo.GetDirectories();
            
            for (int i = 0; i < files.Length; i++)
            {
                bool lastFile = (i == files.Length - 1) && (dirs.Length == 0);
                string fileMarker = lastFile ? "└── " : "├── ";
                sb.AppendLine($"{newIndent}{fileMarker}{files[i].Name}");
            }

            for (int i = 0; i < dirs.Length; i++)
            {
                bool lastDir = (i == dirs.Length - 1);
                BuildDirectoryTree(dirs[i], newIndent, lastDir, sb);
            }
        }
    }
}

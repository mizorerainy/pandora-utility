using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace MizoreRainy.Pandora.BuildUtility
{
    public class PostBuildTaskCreator
    {
        [MenuItem("Assets/Create/Pandora/Post Build Task Script", false, 80)]
        public static void CreatePostBuildTaskScript()
        {
            var icon = EditorGUIUtility.IconContent("cs Script Icon").image as Texture2D;
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
                0,
                ScriptableObject.CreateInstance<DoCreatePostBuildTaskScript>(),
                "NewPostBuildTask.cs",
                icon,
                null);
        }
    }

    class DoCreatePostBuildTaskScript : EndNameEditAction
    {
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            string className = Path.GetFileNameWithoutExtension(pathName);
            string template = 
@"using System;
using UnityEngine;
using MizoreRainy.Pandora.BuildUtility;
using MizoreRainy.Pandora;

namespace MizoreRainy.Pandora.CustomTasks
{
    /// <summary>
    /// Custom post-build task that executes automatically after the build completes.
    /// </summary>
    [Serializable]
    public class #SCRIPTNAME# : ManagedPostBuildTask
    {
        [Tooltip(""Example configurable field."")]
        public bool IsEnabledInLogs = true;

        public override void Execute(ManagedBuildProfile profile, string buildOutputPath)
        {
            // The buildRoot is the directory containing the build executable/artifacts
            var buildRoot = System.IO.Path.GetDirectoryName(buildOutputPath);
            
            if (IsEnabledInLogs)
            {
                PandoraLogger.LogBuild($""[#SCRIPTNAME#] Executing for profile {profile.Name} at: {buildRoot}"");
            }
            
            // TODO: Implement your custom build logic here.
            // Example tasks:
            // - Delete intermediate configuration files
            // - Run a shell script or custom process
            // - Zip the output folder
            // - Publish artifacts to a specific server
        }
    }
}";
            template = template.Replace("#SCRIPTNAME#", className);
            
            File.WriteAllText(pathName, template, Encoding.UTF8);
            AssetDatabase.ImportAsset(pathName);
            var asset = AssetDatabase.LoadAssetAtPath<MonoScript>(pathName);
            ProjectWindowUtil.ShowCreatedAsset(asset);
        }
    }
}

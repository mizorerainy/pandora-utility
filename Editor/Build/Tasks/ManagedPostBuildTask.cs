using System;

namespace MizoreRainy.Pandora.BuildUtility
{
    /// <summary>
    /// Base class for all post-build tasks executed by the Pandora Build Utility.
    /// Define custom fields and implement the Execute method.
    /// </summary>
    [Serializable]
    public abstract class ManagedPostBuildTask
    {
        [UnityEngine.Tooltip("If unchecked, this task will be skipped during the post-build phase.")]
        public bool IsEnabled = true;

        /// <summary>
        /// Executes the task logic.
        /// </summary>
        /// <param name="profile">The build profile that initiated the build.</param>
        /// <param name="buildOutputPath">The absolute path to the build output directory or file.</param>
        public abstract void Execute(ManagedBuildProfile profile, string buildOutputPath);
    }
}

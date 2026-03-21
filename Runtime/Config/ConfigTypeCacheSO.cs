using System.Collections.Generic;
using UnityEngine;

namespace MizoreRainy.Pandora.ConfigUtility
{
    /// <summary>
    ///     A ScriptableObject cache that stores the fully qualified assembly names of all classes 
    ///     containing configuration settings. Used to avoid expensive reflection scanning at runtime.
    ///     This is automatically generated during the pre-build process.
    /// </summary>
    public class ConfigTypeCacheSO : ScriptableObject
    {
        [Tooltip("List of AssemblyQualifiedNames for root classes that contain config fields.")]
        public List<string> ConfigTypes = new List<string>();
    }
}

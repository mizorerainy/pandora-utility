using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

namespace MizoreRainy.Pandora.ConfigUtility.Editor
{
    /// <summary>
    /// Evaluates assemblies at build time to discover any config entries and populates the cache
    /// replacing the need for reflection at runtime.
    /// </summary>
    public class ConfigPrebuildProcessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            GenerateConfigCache();
        }

        [MenuItem("Tools/Pandora/Generate Config Cache")]
        public static void GenerateConfigCache()
        {
            var fields = TypeCache.GetFieldsWithAttribute<ConfigAttribute>();
            var typeNames = new HashSet<string>();

            foreach (var field in fields)
            {
                Type topmostStatic = null;
                Type current = field.DeclaringType;
                while (current != null)
                {
                    if (current.IsClass && current.IsSealed && current.IsAbstract)
                        topmostStatic = current;
                    else
                        break;
                    
                    current = current.DeclaringType;
                }

                if (topmostStatic != null)
                {
                    typeNames.Add(topmostStatic.AssemblyQualifiedName);
                }
            }

            var cachePath = "Assets/Pandora/Resources/PandoraConfigCache.asset";
            
            // Ensure directory exists
            var dir = Path.GetDirectoryName(cachePath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var cache = AssetDatabase.LoadAssetAtPath<ConfigTypeCacheSO>(cachePath);
            if (cache == null)
            {
                cache = ScriptableObject.CreateInstance<ConfigTypeCacheSO>();
                AssetDatabase.CreateAsset(cache, cachePath);
            }

            cache.ConfigTypes.Clear();
            cache.ConfigTypes.AddRange(typeNames);
            
            EditorUtility.SetDirty(cache);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Pandora Config] Generated config cache with {cache.ConfigTypes.Count} root types.");
        }
    }
}

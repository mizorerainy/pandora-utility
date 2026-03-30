using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace MizoreRainy.Pandora.ConfigUtility.Editor
{
	/// <summary>
	/// Provides validation for all ConfigAttribute fields to guarantee type safety in the Unity Editor.
	/// Throws warnings when unsupported complex types are configured without known parsers.
	/// </summary>
	public static class ConfigValidator
	{
		#region Unity Lifecycle & Initialization
        [InitializeOnLoadMethod]
        public static void ValidateConfigTypes()
        {
            // Discover all available parsers
            var parserTypes = TypeCache.GetTypesDerivedFrom<IConfigValueParser>();
            var parsers = new List<IConfigValueParser>();
            
            foreach (var pt in parserTypes)
            {
                if (!pt.IsAbstract && !pt.IsInterface)
                {
                    try
                    {
                        var instance = (IConfigValueParser)Activator.CreateInstance(pt);
                        parsers.Add(instance);
                    }
                    catch { /* Ignore parsers that don't pass parameterless instantiation */ }
                }
            }

            var fields = TypeCache.GetFieldsWithAttribute<ConfigAttribute>();
            foreach (var field in fields)
            {
                var fieldType = field.FieldType;
                
                // Get the generic argument of ConfigEntry<T>
                Type valueType = null;
                if (fieldType.IsGenericType && fieldType.GetGenericArguments().Length == 1)
                {
                    valueType = fieldType.GetGenericArguments()[0];
                }
                
                if (valueType == null) continue;

                if (!IsSupportedType(valueType, parsers))
                {
                    string richMessage = 
                        $"<color=yellow>[Pandora ConfigValidator]</color> Issue configuring <color=white>{field.DeclaringType?.Name}.{field.Name}</color>.\n" +
                        $"<color=red><b>Warning:</b></color> Uses complex type <color=yellow>'{valueType.Name}'</color> but no parameterless <color=white>IConfigValueParser</color> was found in the project.\n" +
                        $"Unless parsed by an explicitly registered runtime parser or YAML native serialization, it may fail to save/load appropriately.";
                        
                    Debug.LogWarning(richMessage);
                }
            }
        }

		#endregion

		#region Internal & Interface Implementations

        private static bool IsSupportedType(Type type, List<IConfigValueParser> parsers)
        {
            if (type.IsPrimitive || type.IsEnum || type == typeof(string)) return true;

            foreach (var p in parsers)
            {
                if (p.CanParse(type)) return true;
            }

            return false;
        }

		#endregion
    }
}

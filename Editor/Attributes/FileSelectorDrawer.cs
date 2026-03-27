using UnityEditor;
using UnityEngine;
using MizoreRainy.Pandora.Editor.Attributes;

namespace MizoreRainy.Pandora.Editor.Drawers
{
    [CustomPropertyDrawer(typeof(FileSelectorAttribute))]
    public class FileSelectorDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.HelpBox(position, "[FileSelector] can only be used on string fields.", MessageType.Error);
                return;
            }

            var attr = attribute as FileSelectorAttribute;
            float buttonWidth = 30f;
            
            Rect textRect = new Rect(position.x, position.y, position.width - buttonWidth - 2f, position.height);
            Rect buttonRect = new Rect(position.x + position.width - buttonWidth, position.y, buttonWidth, position.height);

            // Draw standard string property field
            EditorGUI.PropertyField(textRect, property, label);

            // Draw File Picker Button
            if (GUI.Button(buttonRect, new GUIContent("📄", "Select File")))
            {
                string extensionFilter = attr != null ? attr.ExtensionFilter : "";
                string path = EditorUtility.OpenFilePanel("Select File", "", extensionFilter);
                if (!string.IsNullOrEmpty(path))
                {
                    property.stringValue = path;
                    property.serializedObject.ApplyModifiedProperties();
                    GUI.FocusControl(null); // Remove focus to force inspector update
                }
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}

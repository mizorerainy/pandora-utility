using UnityEditor;
using UnityEngine;
using MizoreRainy.Pandora.Editor.Attributes;

namespace MizoreRainy.Pandora.Editor.Drawers
{
    [CustomPropertyDrawer(typeof(PathSelectorAttribute))]
    public class PathSelectorDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.HelpBox(position, "[PathSelector] can only be used on string fields.", MessageType.Error);
                return;
            }

            float buttonWidth = 25f;
            
            // Adjust the width to fit two buttons side by side
            Rect textRect = new Rect(position.x, position.y, position.width - (buttonWidth * 2) - 4f, position.height);
            Rect folderBtnRect = new Rect(position.x + position.width - (buttonWidth * 2) - 2f, position.y, buttonWidth, position.height);
            Rect fileBtnRect = new Rect(position.x + position.width - buttonWidth, position.y, buttonWidth, position.height);

            // Draw standard string property field
            EditorGUI.PropertyField(textRect, property, label);

            // Draw Folder Picker Button
            if (GUI.Button(folderBtnRect, new GUIContent("📁", "Select Folder"), EditorStyles.miniButtonLeft))
            {
                string path = EditorUtility.OpenFolderPanel("Select Folder", "", "");
                if (!string.IsNullOrEmpty(path))
                {
                    property.stringValue = path;
                    property.serializedObject.ApplyModifiedProperties();
                    GUI.FocusControl(null); // Remove focus to force inspector update
                }
            }
            
            // Draw File Picker Button
            if (GUI.Button(fileBtnRect, new GUIContent("📄", "Select File"), EditorStyles.miniButtonRight))
            {
                string path = EditorUtility.OpenFilePanel("Select File", "", "");
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

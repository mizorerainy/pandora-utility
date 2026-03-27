using UnityEditor;
using UnityEngine;
using MizoreRainy.Pandora.Editor.Attributes;

namespace MizoreRainy.Pandora.Editor.Drawers
{
    [CustomPropertyDrawer(typeof(UrlButtonAttribute))]
    public class UrlButtonDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.HelpBox(position, "[UrlButton] can only be used on string fields.", MessageType.Error);
                return;
            }

            var attr = attribute as UrlButtonAttribute;
            float buttonWidth = 140f;
            
            Rect textRect = new Rect(position.x, position.y, position.width - buttonWidth - 2f, position.height);
            Rect buttonRect = new Rect(position.x + position.width - buttonWidth, position.y, buttonWidth, position.height);

            EditorGUI.PropertyField(textRect, property, label);

            if (GUI.Button(buttonRect, new GUIContent("🌐 " + attr.ButtonText)))
            {
                if (!string.IsNullOrEmpty(property.stringValue))
                {
                    string url = string.Format(attr.UrlFormat, property.stringValue);
                    Application.OpenURL(url);
                }
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}

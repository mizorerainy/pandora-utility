using System;
using System.Net;
using MizoreRainy.Pandora.ConfigUtility;

#if UNITY_EDITOR
using MizoreRainy.Pandora.ConfigUtility.Editor;
using UnityEditor;
using UnityEngine;
#endif

namespace MizoreRainy.Pandora.Samples.ConfigSystem
{
    /// <summary>
    /// A parser for System.Net.IPAddress to be used with the Pandora Configuration System.
    /// To use this, call ConfigLoader.RegisterParser(new IPAddressConfigParser()); before accessing configs.
    /// </summary>
    public class IPAddressConfigParser : IConfigValueParser
#if UNITY_EDITOR
        , IConfigEditorParser
#endif
    {
        public bool CanParse(Type type) => type == typeof(IPAddress);

        public object Parse(string value, Type type)
        {
            if (IPAddress.TryParse(value, out IPAddress ip))
                return ip;
            return IPAddress.Loopback;
        }

        public string ConvertToString(object value, Type type)
        {
            if (value is IPAddress ip)
                return ip.ToString();
            return string.Empty;
        }

#if UNITY_EDITOR
        public object DrawEditorGui(GUIContent label, object currentValue)
        {
            IPAddress currentIp = currentValue as IPAddress ?? IPAddress.Loopback;
            string input = EditorGUILayout.TextField(label, currentIp.ToString());
            if (IPAddress.TryParse(input, out IPAddress newIp))
                return newIp;
            return currentIp;
        }
#endif
    }
    
    /// <summary>
    /// A parser for System.Uri to be used with the Pandora Configuration System.
    /// </summary>
    public class UriConfigParser : IConfigValueParser
#if UNITY_EDITOR
        , IConfigEditorParser
#endif
    {
        public bool CanParse(Type type) => type == typeof(Uri);

        public object Parse(string value, Type type)
        {
            if (Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out Uri uri))
                return uri;
            return null;
        }

        public string ConvertToString(object value, Type type)
        {
            if (value is Uri uri)
                return uri.ToString();
            return string.Empty;
        }

#if UNITY_EDITOR
        public object DrawEditorGui(GUIContent label, object currentValue)
        {
            Uri currentUri = currentValue as Uri;
            string currentString = currentUri?.ToString() ?? string.Empty;
            string input = EditorGUILayout.TextField(label, currentString);
            
            if (Uri.TryCreate(input, UriKind.RelativeOrAbsolute, out Uri newUri))
                return newUri;
            
            return currentUri;
        }
#endif
    }
}

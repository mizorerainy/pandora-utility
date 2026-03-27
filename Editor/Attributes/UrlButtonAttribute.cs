using UnityEngine;

namespace MizoreRainy.Pandora.Editor.Attributes
{
    public class UrlButtonAttribute : PropertyAttribute
    {
        public string ButtonText { get; private set; }
        public string UrlFormat { get; private set; }

        public UrlButtonAttribute(string buttonText, string urlFormat)
        {
            ButtonText = buttonText;
            UrlFormat = urlFormat;
        }
    }
}

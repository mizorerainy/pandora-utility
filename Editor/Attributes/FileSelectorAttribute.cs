using UnityEngine;

namespace MizoreRainy.Pandora.Editor.Attributes
{
    /// <summary>
    /// Attribute to draw a string field as a file selection box in the Inspector.
    /// </summary>
    public class FileSelectorAttribute : PropertyAttribute
    {
        public string ExtensionFilter { get; private set; }

        public FileSelectorAttribute(string extensionFilter = "")
        {
            ExtensionFilter = extensionFilter;
        }
    }
}

#if UNITY_EDITOR

// ReSharper disable once CheckNamespace
namespace MizoreRainy.Pandora.ConfigUtility.Editor
{
	/// <summary>
	///     Represents a combined interface that integrates parsing capabilities with
	///     custom editor GUI functionality within the Pandora Config Editor framework.
	/// </summary>
	public interface IConfigEditorParser : IConfigValueParser, IConfigEditorDrawer
	{
	}
}

#endif
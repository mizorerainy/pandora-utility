using UnityEngine;

namespace MizoreRainy.Pandora
{
	/// <summary>
	/// Provides a centralized logging utility for the Pandora package to maintain consistent formatting and colors.
	/// </summary>
	public static class PandoraLogger
	{
		// Color code definitions
		private const string _COLOR_CONFIG     = "#E2C044"; // Yellow-ish
		private const string _COLOR_NETWORK    = "#44E2C0"; // Cyan-ish
		private const string _COLOR_BUILD      = "#C044E2"; // Purple-ish
		private const string _COLOR_EDITOR     = "#E24444"; // Red-ish
		private const string _COLOR_DEFAULT    = "#AAAAAA"; // Gray

		/// <summary>
		/// Formats a log message with a colored prefix.
		/// </summary>
		private static string FormatPrefix(string _system, string _colorHex)
		{
			return $"<color={_colorHex}>[{_system}]</color>";
		}

		// Config System Logging
		public static void LogConfig(string _message) => Debug.Log($"{FormatPrefix("ConfigLoader", _COLOR_CONFIG)} {_message}");
		public static void LogConfigWarning(string _message) => Debug.LogWarning($"{FormatPrefix("ConfigLoader", _COLOR_CONFIG)} {_message}");
		public static void LogConfigError(string _message) => Debug.LogError($"{FormatPrefix("ConfigLoader", _COLOR_CONFIG)} {_message}");

		public static void LogConfigParser(string _parserName, string _message) => Debug.Log($"{FormatPrefix(_parserName, _COLOR_CONFIG)} {_message}");
		public static void LogConfigParserWarning(string _parserName, string _message) => Debug.LogWarning($"{FormatPrefix(_parserName, _COLOR_CONFIG)} {_message}");
		public static void LogConfigParserError(string _parserName, string _message) => Debug.LogError($"{FormatPrefix(_parserName, _COLOR_CONFIG)} {_message}");

		// Network/AetherLink System Logging
		public static void LogNetwork(string _message) => Debug.Log($"{FormatPrefix("AetherLink", _COLOR_NETWORK)} {_message}");
		public static void LogNetworkWarning(string _message) => Debug.LogWarning($"{FormatPrefix("AetherLink", _COLOR_NETWORK)} {_message}");
		public static void LogNetworkError(string _message) => Debug.LogError($"{FormatPrefix("AetherLink", _COLOR_NETWORK)} {_message}");

		// Build System Logging
		public static void LogBuild(string _message) => Debug.Log($"{FormatPrefix("BuildSettings", _COLOR_BUILD)} {_message}");
		public static void LogBuildWarning(string _message) => Debug.LogWarning($"{FormatPrefix("BuildSettings", _COLOR_BUILD)} {_message}");
		public static void LogBuildError(string _message) => Debug.LogError($"{FormatPrefix("BuildSettings", _COLOR_BUILD)} {_message}");

		// Generic Logging
		public static void Log(string _system, string _message) => Debug.Log($"{FormatPrefix(_system, _COLOR_DEFAULT)} {_message}");
		public static void LogWarning(string _system, string _message) => Debug.LogWarning($"{FormatPrefix(_system, _COLOR_DEFAULT)} {_message}");
		public static void LogError(string _system, string _message) => Debug.LogError($"{FormatPrefix(_system, _COLOR_DEFAULT)} {_message}");
	}
}

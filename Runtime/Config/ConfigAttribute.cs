// =================================================================================
// File: ConfigAttribute.cs
// Author: MizoreRainy
// Description: Attribute used to mark static fields as configuration settings.
//              This system is dependency-free.
// =================================================================================

using System;
using System.Diagnostics.CodeAnalysis;

// ReSharper disable once CheckNamespace
namespace MizoreRainy.Pandora.ConfigUtility
{
	/// <summary>
	/// Specifies metadata for a configuration field, including its key, default value, and description.
	/// </summary>
	/// <remarks>
	/// This attribute is used to annotate public static readonly fields that represent configuration entries.
	/// </remarks>
	[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
	[SuppressMessage("ReSharper", "RedundantAttributeUsageProperty")]
	public sealed class ConfigAttribute : Attribute
	{
		#region Properties
		/// <summary>
		/// Gets the unique identifier for the configuration setting.
		/// This key is utilized to identify and associate the configuration
		/// entry with its respective value. It must remain unique across
		/// all configuration entries to avoid conflicts during initialization
		/// or lookup processes.
		/// </summary>
		public string Key { get; }

		/// <summary>
		/// Gets the default value associated with a configuration setting.
		/// This property holds the value that will be used if no explicit value is set for the configuration.
		/// </summary>
		public object DefaultValue { get; }

		/// <summary>
		/// Gets the description of the configuration setting.
		/// This provides additional context or information about the purpose or usage of the associated setting.
		/// </summary>
		public string Description { get; }

		/// <summary>
		/// Gets the optional custom background color hex code for displaying this setting in the Editor UI.
		/// </summary>
		public string BackgroundColorHex { get; }

		/// <summary>
		/// Gets the initialization and display order of the configuration setting.
		/// Settings with lower explicit order values will initialize and display before settings with higher order values.
		/// </summary>
		public int Order { get; }

		#endregion

		#region Initialization

		/// <summary>
		/// Initializes a new instance of the <see cref="ConfigAttribute"/> class.
		/// </summary>
		/// <param name="_key">The unique identifier key used for reading and saving.</param>
		/// <param name="_defaultValue">The default fallback value of the setting.</param>
		/// <param name="_description">Optional description used for tooltips and generation comments.</param>
		/// <param name="_backgroundColorHex">Optional hex code (e.g., "#FF0000") to colorize the UI background.</param>
		/// <param name="_order">The display and initialization explicitly defined order. Default is 0.</param>
		public ConfigAttribute(string _key, object _defaultValue, string _description = "", string _backgroundColorHex = "", int _order = 0)
		{
			Key = _key;
			DefaultValue = _defaultValue;
			Description = _description;
			BackgroundColorHex = _backgroundColorHex;
			Order = _order;
		}

		#endregion
	}
}
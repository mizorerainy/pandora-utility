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
	/// This attribute is used to annotate public static readonly fields that represent configuration entries.
	/// </summary>
	[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
	[SuppressMessage("ReSharper", "RedundantAttributeUsageProperty")]
	public sealed class ConfigAttribute : Attribute
	{
		/// <summary>
		/// Gets the unique identifier for the configuration setting.
		/// This key is utilized to identify and associate the configuration
		/// entry with its respective value. It must remain unique across
		/// all configuration entries to avoid conflicts during initialization
		/// or lookup processes.
		/// </summary>
		public string Key { get; }

		/// <summary>
		/// Represents the default value associated with a configuration setting.
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
		/// Represents an attribute used to define configuration metadata for a field.
		/// This attribute can be applied to fields to specify a configuration key,
		/// default value, and an optional description.
		/// </summary>
		/// <remarks>
		/// This attribute is sealed and cannot be inherited. It is applied at the
		/// field level and allows specifying metadata for configurations.
		/// </remarks>
		/// <example>
		/// This attribute is typically used in classes that represent configuration
		/// settings to define metadata for each configuration option.
		/// </example>
		public ConfigAttribute(string _key, object _defaultValue, string _description = "", string _backgroundColorHex = "")
		{
			Key = _key;
			DefaultValue = _defaultValue;
			Description = _description;
			BackgroundColorHex = _backgroundColorHex;
		}
	}
}
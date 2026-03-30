// =================================================================================
// File: ConfigGroupAttribute.cs
// Author: MizoreRainy
// Description: Attribute used to mark classes as configuration groups and specify
//              metadata like ordering priority in the Editor UI.
// =================================================================================

using System;
using System.Diagnostics.CodeAnalysis;

// ReSharper disable once CheckNamespace
namespace MizoreRainy.Pandora.ConfigUtility
{
	/// <summary>
	/// Represents an attribute that specifies metadata for a configuration group, such as ordering priority.
	/// Apply this attribute to classes holding [Config] members to manage their listing order.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
	[SuppressMessage("ReSharper", "RedundantAttributeUsageProperty")]
	public sealed class ConfigGroupAttribute : Attribute
	{
		/// <summary>
		/// Gets the initialization and display order of the configuration group.
		/// Groups with lower explicit order values will display before those with higher order values.
		/// </summary>
		public int Order { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ConfigGroupAttribute"/> class.
		/// </summary>
		/// <param name="_order">The display order of this group. Default is 0.</param>
		public ConfigGroupAttribute(int _order = 0)
		{
			Order = _order;
		}
	}
}

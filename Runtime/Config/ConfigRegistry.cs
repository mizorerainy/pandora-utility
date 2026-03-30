using MizoreRainy.Pandora;
using System.Collections.Generic;
using MizoreRainy.Pandora.ConfigUtility.Parsers;

namespace MizoreRainy.Pandora.ConfigUtility
{
    /// <summary>
    ///     Represents the active state of configuration settings and parsers.
    ///     This instance-based approach allows isolated testing environments.
    /// </summary>
    public class ConfigRegistry
    {
        /// <summary>
        ///     Gets the collection of configurable settings used within the application lifecycle.
        /// </summary>
        public readonly List<IConfigEntry> Settings = new();

        /// <summary>
        ///     Gets the collection of registered configuration value parsers.
        /// </summary>
        public readonly List<IConfigValueParser> Parsers = new List<IConfigValueParser>
        {
            new Parsers.ArrayConfigParser()
        };

        /// <summary>
        ///     Gets the dictionary storing explicit initialization and display orders for configuration groups.
        /// </summary>
        public readonly Dictionary<string, int> GroupOrders = new();
    }
}

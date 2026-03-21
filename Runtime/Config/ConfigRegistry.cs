using MizoreRainy.Pandora;
using System.Collections.Generic;

namespace MizoreRainy.Pandora.ConfigUtility
{
    /// <summary>
    ///     Holds the active state of configuration settings and parsers.
    ///     This instance-based approach allows isolated testing environments.
    /// </summary>
    public class ConfigRegistry
    {
        /// <summary>
        ///     Contains a collection of configurable settings used within the application lifecycle.
        /// </summary>
        public readonly List<IConfigEntry> Settings = new();

        /// <summary>
        ///     Represents a collection of registered configuration value parsers.
        /// </summary>
        public readonly List<IConfigValueParser> Parsers = new();
    }
}

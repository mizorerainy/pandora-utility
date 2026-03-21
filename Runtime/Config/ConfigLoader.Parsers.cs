using MizoreRainy.Pandora;
// =================================================================================
// File: ConfigLoader.cs
// Author: MizoreRainy
// Description: The core engine for loading, parsing, and saving configuration.
//    This system is dependency-free and uses built-in .NET Task async.
// =================================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
#if HAVE_VYAML
using VYaml.Serialization;
#endif

// This attribute grants the specified editor assembly access to this assembly's internal members.
[assembly: InternalsVisibleTo("MizoreRainy.Pandora.Editor.Config")]

// ReSharper disable once CheckNamespace
namespace MizoreRainy.Pandora.ConfigUtility
{
	/// <summary>
	///     Provides functionality for initializing, managing, and interacting with application configuration settings.
	///     This class includes methods for loading, saving, runtime updates, and monitoring configuration files.
	/// </summary>
	public static partial class ConfigLoader
	{
		#region Public API

		/// <summary>
		///     Registers a custom parser for handling complex or non-primitive types.
		///     The parser must implement the IConfigValueParser interface and should be added
		///     prior to the completion of the ConfigLoader initialization process.
		/// </summary>
		/// <param name="_parser">
		///     The custom parser to be registered, responsible for
		///     parsing and converting complex types to and from strings within the configuration system.
		/// </param>
		public static void RegisterParser(IConfigValueParser _parser)
		{
			if (_IsInitialized)
			{
				PandoraLogger.LogConfigError("Parsers must be registered before initialization.");
				return;
			}

			if (!Registry.Parsers.Contains(_parser)) Registry.Parsers.Add(_parser);
		}

		#endregion

		#region Utility Methods

		/// <summary>
		///     Retrieves an appropriate parser instance that implements <see cref="IConfigValueParser" />
		///     for handling the specified type.
		///     If a parser that can handle the type is not found, null is returned.
		/// </summary>
		/// <param name="_type">The type for which a suitable parser is being requested.</param>
		/// <returns>
		///     An instance of <see cref="IConfigValueParser" /> capable of parsing the specified type,
		///     or null if no registered parser can handle the given type.
		/// </returns>
		internal static IConfigValueParser GetParserForType(Type _type)
		{
			return Registry.Parsers.FirstOrDefault(_p => _p.CanParse(_type));
		}

		#endregion
	}
}

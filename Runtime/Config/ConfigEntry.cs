using MizoreRainy.Pandora;
// =================================================================================
// File: ConfigEntry.cs
// Author: MizoreRainy
// Description: Holds the value and state for a single configuration setting.
//              This system is dependency-free and uses built-in .NET Task async.
// =================================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace MizoreRainy.Pandora.ConfigUtility
{
	/// <summary>
	///     Defines the contract for a configuration entry, providing access to its key,
	///     description, group name, and value type.
	///     It also specifies methods for
	///     setting the value from a string, retrieving the value as a string, and
	///     resetting the value to its default.
	/// </summary>
	public interface IConfigEntry
	{
		/// <summary>
		///     Gets the unique key that identifies the configuration entry.
		/// </summary>
		string Key { get; }

		/// <summary>
		///     Gets the description of the configuration entry, providing additional context or information.
		/// </summary>
		string Description { get; }

		/// <summary>
		///     Gets the name of the group to which the configuration entry belongs.
		/// </summary>
		string GroupName { get; }

		/// <summary>
		///     Gets the optional custom background color hex code for displaying this setting in the Editor UI.
		/// </summary>
		string BackgroundColorHex { get; }

		/// <summary>
		///     Gets the explicit configuration initialization order.
		/// </summary>
		int Order { get; }

		/// <summary>
		///     Gets the type of the value stored in the configuration entry.
		/// </summary>
		Type ValueType { get; }

		/// <summary>
		///     Sets the value of the configuration entry from a raw string.
		/// </summary>
		/// <param name="_rawValue">The raw string value to be parsed and set.</param>
		void SetValueFromString(string _rawValue);

		/// <summary>
		///     Gets the value of the configuration entry as a string.
		/// </summary>
		/// <returns>The string representation of the configuration entry's value.</returns>
		string GetValueAsString();

		/// <summary>
		///     Resets the configuration entry's value to its default.
		/// </summary>
		void SetToDefault();
	}

	/// <summary>
	///     Represents a single, typed configuration setting that is both awaitable and reactive.
	///     This class manages the value of a configuration entry, its default value, and provides
	///     mechanisms for awaiting its initialization and subscribing to value changes.
	/// </summary>
	/// <typeparam name="T">The type of the value held by the configuration entry.</typeparam>
	public class ConfigEntry<T> : IConfigEntry
	{
		#region Fields


		private T _Value;
		private readonly T _DefaultValue;
		private readonly TaskCompletionSource<T> _InitializationTcs = new();

		#endregion

		#region Events

		private event Action<T> OnChangeEvent;

		#endregion

		#region Properties

		/// <summary>
		///     Gets the unique key for the configuration entry.
		/// </summary>
		public string Key { get; }

		/// <summary>
		///     Gets the description of the configuration entry.
		/// </summary>
		public string Description { get; }

		/// <summary>
		///     Gets the group name for the configuration entry.
		/// </summary>
		public string GroupName { get; }

		/// <summary>
		///     Gets the optional custom background color hex code for displaying this setting in the Editor UI.
		/// </summary>
		public string BackgroundColorHex { get; }

		/// <summary>
		///     Gets the explicit configuration initialization order.
		/// </summary>
		public int Order { get; }

		/// <summary>
		///     Gets the type of the value for the configuration entry.
		/// </summary>
		public Type ValueType => typeof(T);

		/// <summary>
		///     Gets the current value of the configuration entry.
		///     Setting a new value triggers the OnChangeEvent if the value has changed.
		/// </summary>
		public T Value
		{
			get => _Value;
			private set
			{
				if (EqualityComparer<T>.Default.Equals(_Value, value)) return;
				_Value = value;
				OnChangeEvent?.Invoke(_Value);
				if (!_InitializationTcs.Task.IsCompleted) _InitializationTcs.TrySetResult(_Value);
			}
		}

		#endregion

		#region Operators

		/// <summary>
		///     Allows implicit conversion from the configuration entry to its underlying value type.
		///     Safely handles uninitialized or null instances by logging a helpful error message.
		/// </summary>
		public static implicit operator T(ConfigEntry<T> _entry)
		{
			if (_entry == null)
			{
				Debug.LogError($"[Pandora Config] Attempted to access a null ConfigEntry<{typeof(T).Name}>! This usually indicates that the PandoraConfigCache is outdated. Try clicking 'Pandora -> Config -> Generate Config Cache' in the editor, or ensure ConfigLoader is initialized before accessing.");
				return default;
			}
			return _entry.Value;
		}

		#endregion

		#region Unity Lifecycle / Initialization

		/// <summary>
		///     Initializes a new instance of the ConfigEntry class with the specified attribute and group name.
		/// </summary>
		/// <param name="_attribute">The ConfigAttribute containing metadata for the entry.</param>
		/// <param name="_groupName">The name of the group to which this entry belongs.</param>
		/// <exception cref="ArgumentException">Thrown if the default value in the attribute cannot be converted to type T.</exception>
		public ConfigEntry(ConfigAttribute _attribute, string _groupName)
		{
			Key = _attribute.Key;
			GroupName = _groupName;

			Order = _attribute.Order;
			Description = _attribute.Description;
			BackgroundColorHex = _attribute.BackgroundColorHex;

			try
			{
				_DefaultValue = (T)Convert.ChangeType(_attribute.DefaultValue, typeof(T));
			}
			catch (Exception ex) when (ex is InvalidCastException || ex is FormatException)
			{
				// This provides a clear error if the default value in the attribute doesn't match type T.
				throw new ArgumentException(
					$"The default value '{_attribute.DefaultValue}' (type: {_attribute.DefaultValue.GetType().Name}) " +
					$"for key '{_attribute.Key}' cannot be converted to the required type '{typeof(T).Name}'.",
					nameof(_attribute.DefaultValue),
					ex);
			}

			_Value = _DefaultValue;
		}

		#endregion

		#region Public API

		/// <summary>
		///     Gets an awaiter for the asynchronous initialization of this configuration entry.
		/// </summary>
		/// <returns>A TaskAwaiter for the initialization task.</returns>
		public TaskAwaiter<T> GetAwaiter()
		{
			return _InitializationTcs.Task.GetAwaiter();
		}

		/// <summary>
		///     Subscribes a handler to the value change event of this configuration entry.
		/// </summary>
		/// <param name="_handler">The action to be invoked when the value changes.</param>
		/// <returns>An IDisposable that can be used to unsubscribe from the event.</returns>
		public IDisposable OnChange(Action<T> _handler)
		{
			OnChangeEvent += _handler;
			if (_InitializationTcs.Task.IsCompleted) _handler?.Invoke(Value);
			return new Subscription(this, _handler);
		}

		/// <summary>
		///     Sets a new value for the configuration entry in memory.
		/// </summary>
		/// <param name="_newValue">The new value to be set.</param>
		public void SetValue(T _newValue)
		{
			Value = _newValue;
		}

		#endregion

		#region Internal & Interface Implementations

		/// <summary>
		///     Sets the value of the entry from a raw string, using registered or default parsers.
		/// </summary>
		void IConfigEntry.SetValueFromString(string _rawValue)
		{
			var parser = ConfigLoader.GetParserForType(typeof(T));
			if (parser != null)
			{
				if (parser.TryParse(_rawValue, typeof(T), out var parsedResult))
				{
					Value = (T)parsedResult;
				}
				else
				{
					PandoraLogger.LogConfigWarning($"Custom parser failed for key '{Key}'. Using default value.");
					Value = _DefaultValue;
				}
			}
			else
			{
				try
				{
					Value = (T)Convert.ChangeType(_rawValue, typeof(T), CultureInfo.InvariantCulture);
				}
				catch (Exception)
				{
					PandoraLogger.LogConfigWarning($"Could not parse value '{_rawValue}' for key '{Key}'. Using default value.");
					Value = _DefaultValue;
				}
			}
		}

		/// <summary>
		///     Gets the string representation of the entry's value.
		/// </summary>
		string IConfigEntry.GetValueAsString()
		{
			var parser = ConfigLoader.GetParserForType(typeof(T));
			if (parser != null) return parser.ToString(Value);
			return Value?.ToString() ?? "";
		}

		/// <summary>
		///     Resets the entry's value to its default.
		/// </summary>
		void IConfigEntry.SetToDefault()
		{
			Value = _DefaultValue;
		}

		#endregion

		#region Nested Types

		/// <summary>
		///     Represents a subscription to a configuration entry's value change event.
		///     This struct implements IDisposable to allow for easy unsubscribing,
		///     preventing memory leaks.
		/// </summary>
		private readonly struct Subscription : IDisposable
		{
			private readonly ConfigEntry<T> _Owner;
			private readonly Action<T> _Handler;

			public Subscription(ConfigEntry<T> _owner, Action<T> _handler)
			{
				_Owner = _owner;
				_Handler = _handler;
			}

			public void Dispose()
			{
				_Owner.OnChangeEvent -= _Handler;
			}
		}

		#endregion
	}
}
// =================================================================================
// File: ConfigEntry.cs
// Author: MizoreRainy
// Description: Holds the value and state for a single configuration setting.
//              This system is dependency-free and uses built-in .NET Task async.
// =================================================================================

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace MizoreRainy.Pandora.ConfigUtility
{
    #region Interfaces

    /// <summary>
    /// Defines the contract for a configuration entry, outlining essential
    /// properties and methods for managing configuration data within the system.
    /// </summary>
    public interface IConfigEntry
    {
        /// <summary>
        /// Gets the unique identifier for the configuration entry used to associate
        /// this setting with its stored value.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Gets the description of the configuration entry.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the name of the group to which the configuration entry belongs.
        /// </summary>
        string GroupName { get; }

        /// <summary>
        /// Gets the data type of the value associated with the configuration entry.
        /// </summary>
        Type ValueType { get; }

        /// <summary>
        /// Sets the value of the configuration entry by parsing a string representation
        /// of the desired value.
        /// </summary>
        /// <param name="_rawValue">
        /// The string representation of the value to be assigned to the configuration entry.
        /// </param>
        void SetValueFromString(string _rawValue);

        /// <summary>
        /// Retrieves the value of the configuration entry as a string representation.
        /// </summary>
        /// <returns>The string representation of the configuration entry's value.</returns>
        string GetValueAsString();

        /// <summary>
        /// Reverts the configuration entry's value to its defined default state.
        /// </summary>
        void SetToDefault();
    }

    #endregion

    #region ConfigEntry Implementation

    /// <summary>
    /// Represents a single typed configuration entry, allowing reactive updates,
    /// asynchronous initialization, and efficient management of configuration values.
    /// </summary>
    /// <typeparam name="T">The type of the configuration value.</typeparam>
    public class ConfigEntry<T> : IConfigEntry
    {
        #region Nested Types

        /// <summary>
        /// Represents a subscription to a configuration change event, enabling
        /// reactive handling of value updates and lifecycle management for event handlers.
        /// </summary>
        private readonly struct Subscription : IDisposable
        {
            /// <summary>
            /// Holds a reference to the owning instance of the <see cref="ConfigEntry{T}"/> that contains this subscription.
            /// Ensures proper disposal of event subscriptions tied to the owning configuration entry.
            /// </summary>
            private readonly ConfigEntry<T> _Owner;

            /// <summary>
            /// Represents the delegate that handles configuration value changes.
            /// </summary>
            private readonly Action<T> _Handler;

            /// <summary>
            /// Manages the lifecycle of an event subscription for a configuration entry.
            /// Ensures the event handler is properly detached to prevent memory leaks when disposed.
            /// </summary>
            public Subscription(ConfigEntry<T> _owner, Action<T> _handler)
            {
                _Owner = _owner;
                _Handler = _handler;
            }

            /// <summary>
            /// Releases the resources held by this object and unregisters associated event handlers.
            /// </summary>
            public void Dispose()
            {
                _Owner.OnChangeEvent -= _Handler;
            }
        }

        #endregion

        #region Fields

        /// <summary>
        /// Stores the current value of the configuration entry.
        /// </summary>
        private T _Value;

        /// <summary>
        /// Stores the default value for the configuration entry. This value is used
        /// during initialization, and when a reset to default is requested or when
        /// parsing the provided value fails.
        /// </summary>
        private readonly T _DefaultValue;

        /// <summary>
        /// A <see cref="TaskCompletionSource{T}"/> used to handle the completion of the initialization
        /// process for the configuration entry's value. This allows consumers to await the loading
        /// of the initial value from the configuration source.
        /// </summary>
        private readonly TaskCompletionSource<T> _InitializationTcs = new TaskCompletionSource<T>();

        /// <summary>
        /// An event that is triggered when the configuration value changes.
        /// Subscribed handlers receive the updated value as an argument.
        /// </summary>
        private event Action<T> OnChangeEvent;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the unique key associated with the configuration entry.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets the description of the configuration entry.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the name of the group to which the configuration entry belongs.
        /// </summary>
        public string GroupName { get; }

        /// <summary>
        /// Gets the type of the value stored in the configuration entry.
        /// </summary>
        public Type ValueType => typeof(T);

        /// <summary>
        /// Gets or sets the current value of the configuration entry.
        /// Triggers a change event and updates the initialization task upon modification.
        /// </summary>
        public T Value
        {
            get => _Value;
            private set
            {
                if (EqualityComparer<T>.Default.Equals(_Value, value)) return;
                _Value = value;
                OnChangeEvent?.Invoke(_Value);

                if (!_InitializationTcs.Task.IsCompleted)
                {
                    _InitializationTcs.TrySetResult(_Value);
                }
            }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Represents a single typed configuration entry, providing mechanisms for reactive updates,
        /// asynchronous initialization, and value management in the context of a configuration system.
        /// </summary>
        /// <typeparam name="T">The type of the configuration value managed by this entry.</typeparam>
        public ConfigEntry(ConfigAttribute _attribute, string _groupName)
        {
            Key = _attribute.Key;
            Description = _attribute.Description;
            GroupName = _groupName;
            _DefaultValue = (T)Convert.ChangeType(_attribute.DefaultValue, typeof(T));
            _Value = _DefaultValue;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Allows awaiting this object until its value is loaded from the configuration file,
        /// enabling asynchronous initialization of the configuration entry.
        /// </summary>
        /// <returns>A task awaiter, allowing consumers to await the completion of the initialization task.</returns>
        public TaskAwaiter<T> GetAwaiter() => _InitializationTcs.Task.GetAwaiter();

        /// <summary>
        /// Registers a handler to be invoked when the value of this configuration entry changes.
        /// </summary>
        /// <param name="_handler">The action to execute when the configuration value changes.</param>
        /// <returns>An IDisposable that, when disposed, unsubscribes the registered handler.</returns>
        public IDisposable OnChange(Action<T> _handler)
        {
            OnChangeEvent += _handler;
            if (_InitializationTcs.Task.IsCompleted)
            {
                _handler?.Invoke(Value);
            }

            return new Subscription(this, _handler);
        }

        /// <summary>
        /// Sets the value of this configuration entry in memory.
        /// You must call ConfigLoader.SaveAsync() to persist the value to storage.
        /// </summary>
        /// <param name="_newValue">The new value to assign to this configuration entry.</param>
        public void SetValue(T _newValue)
        {
            Value = _newValue;
        }

        #endregion

        #region Interface Implementation

        /// <summary>
        /// Sets the configuration value by parsing the provided string representation.
        /// </summary>
        /// <param name="_rawValue">The raw string value to parse and set as the configuration value.</param>
        void IConfigEntry.SetValueFromString(string _rawValue)
        {
            var parser = ConfigLoader.GetParserForType(typeof(T));
            if (parser != null)
            {
                if (parser.TryParse(_rawValue, typeof(T), out object parsedResult))
                {
                    Value = (T)parsedResult;
                }
                else
                {
                    Debug.LogWarning($"[ConfigLoader] Custom parser failed for key '{Key}'. Using default value.");
                    Value = _DefaultValue;
                }
            }
            else
            {
                try
                {
                    Value = (T)Convert.ChangeType(_rawValue, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
                }
                catch (Exception)
                {
                    Debug.LogWarning($"[ConfigLoader] Could not parse value '{_rawValue}' for key '{Key}'. Using default value.");
                    Value = _DefaultValue;
                }
            }
        }

        /// <summary>
        /// Retrieves the current value of the configuration entry as a string representation.
        /// </summary>
        /// <returns>
        /// A string representation of the current configuration value. Returns an empty string
        /// if the value is null or if no suitable parser is available.
        /// </returns>
        string IConfigEntry.GetValueAsString()
        {
            var parser = ConfigLoader.GetParserForType(typeof(T));
            if (parser != null)
            {
                return parser.ToString(Value);
            }
            return Value?.ToString() ?? "";
        }

        /// <summary>
        /// Resets the value of this configuration entry to its predefined default value.
        /// </summary>
        void IConfigEntry.SetToDefault()
        {
            Value = _DefaultValue;
        }

        #endregion
    }

    #endregion
}
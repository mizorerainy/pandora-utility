// =================================================================================
// File: ConfigSubscriptionHelper.cs
// Author: MizoreRainy
// Description: Provides a UniRx-like .AddTo(this) extension method for IDisposable
//              to automatically manage subscription lifecycles in MonoBehaviours.
//              This system is dependency-free.
// =================================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace MizoreRainy.Pandora.ConfigUtility
{
	/// <summary>
	/// Manages a collection of IDisposable subscriptions and ensures their disposal when the GameObject is destroyed.
	/// Automatically added by using the .AddTo() extension method.
	/// </summary>
	[AddComponentMenu("")] // Hides this component from the "Add Component" menu.
	public class ConfigSubscriptionDisposer : MonoBehaviour
	{
		#region Fields

		/// <summary>
		/// A private collection of IDisposable objects managed by the ConfigSubscriptionDisposer.
		/// Subscriptions added to this list are disposed of when the OnDestroy method is called, ensuring proper cleanup of resources.
		/// </summary>
		private readonly List<IDisposable> _Disposables = new();

		#endregion

		#region Public API

		/// <summary>
		/// Adds an IDisposable to the internal list for later disposal when the MonoBehaviour is destroyed.
		/// </summary>
		/// <param name="_disposable">The IDisposable object to be added and managed.</param>
		public void Add(IDisposable _disposable)
		{
			_Disposables.Add(_disposable);
		}

		#endregion

		#region Constructors/Initialization

		/// <summary>
		/// Handles the destruction event of the MonoBehaviour by disposing of all
		/// attached IDisposable subscriptions and clearing the internal list.
		/// </summary>
		private void OnDestroy()
		{
			foreach (var disposable in _Disposables)
			{
				disposable.Dispose();
			}

			_Disposables.Clear();
		}

		#endregion
	}

	/// <summary>
	/// Provides extension methods to manage IDisposable subscriptions by attaching them to a MonoBehaviour's lifecycle.
	/// Ensures that the subscriptions are properly disposed when the associated GameObject is destroyed.
	/// </summary>
	public static class ConfigDisposableExtensions
	{
		#region Public API
		/// <summary>
		/// Associates an IDisposable subscription with a specified MonoBehaviour.
		/// The IDisposable will automatically be disposed of when the MonoBehaviour's GameObject is destroyed.
		/// </summary>
		/// <param name="_disposable">The IDisposable subscription to be managed.</param>
		/// <param name="_target">The target MonoBehaviour to associate the subscription with.
		/// If null, the IDisposable will be immediately disposed.</param>
		public static void AddTo(this IDisposable _disposable, Component _target)
		{
			if (_target == null)
			{
				_disposable.Dispose();
				return;
			}

			// Try to get an existing disposer or add a new one if it doesn't exist.
			if (!_target.TryGetComponent<ConfigSubscriptionDisposer>(out var disposer))
			{
				disposer = _target.gameObject.AddComponent<ConfigSubscriptionDisposer>();
			}

			disposer.Add(_disposable);
		}
		#endregion
	}
}
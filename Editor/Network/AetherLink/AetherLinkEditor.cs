// =====================================================================================================================
//
// AetherLinkEditor.cs
//
// A custom editor for the AetherLink component.
//
// Features:
// - Clean, organized layout with foldable sections.
// - A prominent Master/Slave mode toggle with improved UI.
// - A real-time status box in Play Mode showing the connection state.
// - Action buttons to manually start/stop the link from the Inspector.
// - Network statistics monitoring.
// - Enhanced visual feedback and validation.
//
// =====================================================================================================================

#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using System.Net;

// ReSharper disable once CheckNamespace
namespace MizoreRainy.Pandora.NetworkUtility.Editor
{
	/// <summary>
	/// Provides a custom Inspector UI for the <see cref="AetherLink"/>,
	/// making it easier to configure and monitor.
	/// </summary>
	[CustomEditor(typeof(AetherLink))]
	public partial class AetherLinkEditor : UnityEditor.Editor
	{
		#region Private Members

		private SerializedProperty _SettingsProp;
		private SerializedProperty _OnConnectedProp;
		private SerializedProperty _OnPacketReceivedProp;
		private SerializedProperty _OnDisconnectedProp;

		// Foldout states for the UI organization
		private bool _SettingsFoldout = true;
		private bool _EventsFoldout = true;
		private bool _StatisticsFoldout = true;
		private bool _AdvancedFoldout;

		// Editor update tracking
		private double _LastUpdateTime;
		private const double _UPDATE_INTERVAL = 0.1; // Update every 100 ms

		// UI Colors
		private readonly Color _MasterColor = new(0.2f, 0.8f, 1f, 1f); // Light blue
		private readonly Color _SlaveColor = new(1f, 0.6f, 0.2f, 1f); // Orange
		private readonly Color _FaintColor = new(0.7f, 0.7f, 0.7f, 0.3f); // Faint gray

		#endregion

		#region Unity Editor Methods

		private void OnEnable()
		{
			// Cache SerializedProperty references for performance
			_SettingsProp = serializedObject.FindProperty("m_Settings");
			_OnConnectedProp = serializedObject.FindProperty("OnConnectedInspector");
			_OnPacketReceivedProp = serializedObject.FindProperty("OnPacketReceivedInspector");
			_OnDisconnectedProp = serializedObject.FindProperty("OnDisconnectedInspector");

			// Register for editor updates in play mode
			if (Application.isPlaying)
			{
				EditorApplication.update += OnEditorUpdate;
			}
		}

		private void OnDisable()
		{
			EditorApplication.update -= OnEditorUpdate;
		}

		private void OnEditorUpdate()
		{
			// Only update the UI at regular intervals to avoid performance issues
			if (EditorApplication.timeSinceStartup - _LastUpdateTime > _UPDATE_INTERVAL)
			{
				_LastUpdateTime = EditorApplication.timeSinceStartup;
				Repaint();
			}
		}

		/// <summary>
		/// Draws the custom Inspector GUI.
		/// </summary>
		public override void OnInspectorGUI()
		{
			serializedObject.Update();
			var link = (AetherLink)target;

			// Header with logo/title
			DrawHeader();

			DrawModeSelection();

			EditorGUILayout.Space();

			// Validate settings
			DrawValidationWarnings();

			// Only show status and control UI while the application is playing
			if (Application.isPlaying)
			{
				DrawStatusBox(link);
				DrawControlButtons(link);
				DrawStatistics(link);
			}

			DrawSettings();
			DrawAdvancedSettings();
			DrawEvents();

			serializedObject.ApplyModifiedProperties();
		}

		#endregion
	}
}
#endif

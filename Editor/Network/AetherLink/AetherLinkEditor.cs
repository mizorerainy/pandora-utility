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
		private SerializedProperty _IsConfiguredProp;

		// Foldout states for the UI organization
		private bool _StatisticsFoldout = true;
		private bool _AdvancedFoldout;
		private bool _SimulationFoldout = true;

		// Simulation Profiles State
		private SimulationProfile[] _AvailableProfiles = new SimulationProfile[0];
		private string[] _ProfileNames = new string[0];
		private int _SelectedProfileIndex = 0;

		// Settings Tab State
		private int _SettingsTab = 0;
		private readonly string[] _TabNames = { "General", "Network", "Advanced" };

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
			_IsConfiguredProp = serializedObject.FindProperty("IsConfigured");

			RefreshSimulationProfiles();

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

			if (!_IsConfiguredProp.boolValue)
			{
				DrawSetupWizard();
			}
			else
			{
				DrawStandardUI(link);
			}

			serializedObject.ApplyModifiedProperties();
		}

		private void DrawStandardUI(AetherLink link)
		{
			DrawHeader();

			EditorGUILayout.Space();

			// Validate settings
			DrawValidationWarnings();

			// Only show status and control UI while the application is playing
			if (Application.isPlaying)
			{
				DrawStatusBox(link);
				DrawControlButtons(link);
				DrawSimulationTools(link);
				DrawStatistics(link);
			}

			DrawTabbedSettings();
		}
		private void RefreshSimulationProfiles()
		{
			string[] guids = AssetDatabase.FindAssets("t:SimulationProfile");
			_AvailableProfiles = new SimulationProfile[guids.Length];
			_ProfileNames = new string[guids.Length];

			for (int i = 0; i < guids.Length; i++)
			{
				string path = AssetDatabase.GUIDToAssetPath(guids[i]);
				_AvailableProfiles[i] = AssetDatabase.LoadAssetAtPath<SimulationProfile>(path);
				_ProfileNames[i] = _AvailableProfiles[i] != null ? _AvailableProfiles[i].ProfileName : "Missing Profile";
			}
			
			if (_SelectedProfileIndex >= _AvailableProfiles.Length)
			{
				_SelectedProfileIndex = 0;
			}
		}

		#endregion
	}
}
#endif

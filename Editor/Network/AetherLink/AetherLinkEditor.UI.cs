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
	public partial class AetherLinkEditor : UnityEditor.Editor
	{
		#region GUI Drawing Methods

		/// <summary>
		/// Draws the header section.
		/// </summary>
		private new void DrawHeader()
		{
			EditorGUILayout.BeginVertical("box");
			var headerStyle = new GUIStyle(EditorStyles.boldLabel)
			{
				fontSize = 16,
				alignment = TextAnchor.MiddleCenter
			};
			EditorGUILayout.LabelField("AetherLink Network Manager", headerStyle);
			EditorGUILayout.EndVertical();
			EditorGUILayout.Space();
		}

		/// <summary>
		/// Draws validation warnings for common configuration issues.
		/// </summary>
		private void DrawValidationWarnings()
		{
			var udpPort = _SettingsProp.FindPropertyRelative("UdpBroadcastPort").intValue;
			var tcpPort = _SettingsProp.FindPropertyRelative("TcpConnectionPort").intValue;

			if (udpPort == tcpPort)
			{
				EditorGUILayout.HelpBox("UDP and TCP ports should be different to avoid conflicts.", MessageType.Warning);
			}

			if (udpPort < 1024 || tcpPort < 1024)
			{
				EditorGUILayout.HelpBox("Using ports below 1024 may require administrator privileges.", MessageType.Warning);
			}
		}

		/// <summary>
		/// Draws the Master/Slave mode toggles buttons with improved layout.
		/// </summary>
		private void DrawModeSelection()
		{
			var linkModeProp = _SettingsProp.FindPropertyRelative("LinkMode");
			var currentMode = (AetherLink.Mode)linkModeProp.enumValueIndex;

			// Create a horizontal layout with label and buttons
			EditorGUILayout.BeginVertical("box");

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Link Mode:", GUILayout.Width(80));

			var originalColor = GUI.backgroundColor;

			// Master button
			GUI.backgroundColor = currentMode == AetherLink.Mode.Master ? _MasterColor : _FaintColor;
			if (GUILayout.Button("Master", GUILayout.Height(25)))
			{
				linkModeProp.enumValueIndex = (int)AetherLink.Mode.Master;
			}

			// Slave button
			GUI.backgroundColor = currentMode == AetherLink.Mode.Slave ? _SlaveColor : _FaintColor;
			if (GUILayout.Button("Slave", GUILayout.Height(25)))
			{
				linkModeProp.enumValueIndex = (int)AetherLink.Mode.Slave;
			}

			GUI.backgroundColor = originalColor;
			EditorGUILayout.EndHorizontal();

			// Show mode description
			var modeDescription = currentMode == AetherLink.Mode.Master
				? "Master listens for connections and broadcasts discovery packets."
				: "Slave searches for and connects to a Master.";
			EditorGUILayout.HelpBox(modeDescription, MessageType.Info);

			EditorGUILayout.EndVertical();
		}

		/// <summary>
		/// Draws the real-time status box shown in Play Mode.
		/// </summary>
		/// <param name="_link">The target AetherLink instance.</param>
		private void DrawStatusBox(AetherLink _link)
		{
			EditorGUILayout.BeginVertical("box");
			EditorGUILayout.LabelField("Runtime Status", EditorStyles.boldLabel);

			var statusStyle = new GUIStyle(EditorStyles.label) { richText = true };

			// Connection state
			var statusText = _link.IsRunning
				? "<color=green>✓ Running</color>"
				: "<color=red>✗ Stopped</color>";
			EditorGUILayout.LabelField("State:", statusText, statusStyle);

			if (_link.IsRunning)
			{
				var connectionText = _link.IsConnected
					? $"<color=green>✓ Connected</color> to {_link.RemoteEndPoint}"
					: "<color=orange>⚡ Listening / Searching...</color>";
				EditorGUILayout.LabelField("Connection:", connectionText, statusStyle);

				// Show local IP addresses
				try
				{
					var hostName = Dns.GetHostName();
					var hostEntry = Dns.GetHostEntry(hostName);
					var localIPs = (from ip in hostEntry.AddressList where ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork select ip.ToString()).ToList();

					if (localIPs.Count > 0)
					{
						EditorGUILayout.LabelField("Local IPs:", string.Join(", ", localIPs), statusStyle);
					}
				}
				catch
				{
					// Ignore DNS resolution errors
				}
			}

			EditorGUILayout.EndVertical();
		}

		/// <summary>
		/// Draws the Start/Stop control buttons shown in Play Mode.
		/// </summary>
		/// <param name="_link">The target AetherLink instance.</param>
		private void DrawControlButtons(AetherLink _link)
		{
			EditorGUILayout.BeginHorizontal();

			GUI.enabled = !_link.IsRunning;
			if (GUILayout.Button("Start Link", GUILayout.Height(25)))
			{
				_link.StartLink();
			}

			GUI.enabled = _link.IsRunning;
			if (GUILayout.Button("Stop Link", GUILayout.Height(25)))
			{
				_link.StopLink();
			}

			GUI.enabled = true;
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.Space();
		}

		/// <summary>
		/// Draws network statistics in Play Mode with enhanced error tracking.
		/// </summary>
		/// <param name="_link">The target AetherLink instance</param>
		private void DrawStatistics(AetherLink _link)
		{
			_StatisticsFoldout = EditorGUILayout.Foldout(_StatisticsFoldout, "Network Statistics", true, EditorStyles.foldoutHeader);
			if (_StatisticsFoldout)
			{
				EditorGUILayout.BeginVertical("box");
				var stats = _link.Statistics;

				// Basic statistics
				EditorGUILayout.LabelField("Packets Sent:", stats.PacketsSent.ToString());
				EditorGUILayout.LabelField("Packets Received:", stats.PacketsReceived.ToString());
				EditorGUILayout.LabelField("Bytes Sent:", FormatBytes(stats.BytesSent));
				EditorGUILayout.LabelField("Bytes Received:", FormatBytes(stats.BytesReceived));

				EditorGUILayout.Space();

				// Error statistics
				EditorGUILayout.LabelField("--- Error Statistics ---", EditorStyles.boldLabel);
				EditorGUILayout.LabelField("Corrupted Packets:", stats.CorruptedPackets.ToString());
				EditorGUILayout.LabelField("Malformed Packets:", stats.MalformedPackets.ToString());

				// Connection statistics
				EditorGUILayout.Space();
				EditorGUILayout.LabelField("--- Connection Statistics ---", EditorStyles.boldLabel);
				EditorGUILayout.LabelField("Connection Attempts:", stats.ConnectionAttempts.ToString());
				EditorGUILayout.LabelField("Disconnection Count:", stats.DisconnectionCount.ToString());

				if (stats.LastPacketTime > 0)
				{
					var timeSinceLastPacket = Time.time - stats.LastPacketTime;
					EditorGUILayout.LabelField("Last Packet:", $"{timeSinceLastPacket:F1}s ago");
				}

				EditorGUILayout.Space();

				// Connection quality indicator
				DrawConnectionQuality(_link);

				EditorGUILayout.BeginHorizontal();
				if (GUILayout.Button("Reset Statistics"))
				{
					_link.ResetStatistics();
				}
				EditorGUILayout.EndHorizontal();

				EditorGUILayout.EndVertical();
			}
			EditorGUILayout.Space();
		}

		/// <summary>
		/// Evaluates and displays the quality of the network connection
		/// based on statistical data from the provided <see cref="AetherLink"/> instance.
		/// </summary>
		/// <param name="_link">The <see cref="AetherLink"/> instance containing network statistics to evaluate.</param>
		private void DrawConnectionQuality(AetherLink _link)
		{
			var stats = _link.Statistics;
			var quality = "Unknown";
			var qualityColor = Color.gray;

			if (stats.PacketsReceived > 10)
			{
				var errorRate = (stats.CorruptedPackets + stats.MalformedPackets) / (float)stats.PacketsReceived;
				if (errorRate < 0.01f) { quality = "Excellent"; qualityColor = Color.green; }
				else if (errorRate < 0.05f) { quality = "Good"; qualityColor = Color.yellow; }
				else { quality = "Poor"; qualityColor = Color.red; }
			}

			var style = new GUIStyle(EditorStyles.label) { normal = { textColor = qualityColor } };
			EditorGUILayout.LabelField("Connection Quality:", quality, style);
		}


		/// <summary>
		/// Draws the main configuration settings in a foldout group.
		/// </summary>
		private void DrawSettings()
		{
			_SettingsFoldout = EditorGUILayout.Foldout(_SettingsFoldout, "Configuration Settings", true, EditorStyles.foldoutHeader);
			if (_SettingsFoldout)
			{
				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(_SettingsProp.FindPropertyRelative("StartOnEnable"));
				EditorGUILayout.PropertyField(_SettingsProp.FindPropertyRelative("StopOnPause"));
				EditorGUILayout.PropertyField(_SettingsProp.FindPropertyRelative("AllowSameMachineConnection"));
				EditorGUILayout.PropertyField(_SettingsProp.FindPropertyRelative("UdpBroadcastPort"));
				EditorGUILayout.PropertyField(_SettingsProp.FindPropertyRelative("TcpConnectionPort"));
				EditorGUILayout.PropertyField(_SettingsProp.FindPropertyRelative("HandshakeInterval"));
				EditorGUILayout.PropertyField(_SettingsProp.FindPropertyRelative("HeartbeatInterval"));
				EditorGUILayout.PropertyField(_SettingsProp.FindPropertyRelative("HeartbeatTimeout"));
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.Space();
		}

		/// <summary>
		/// Draws advanced configuration settings in a foldout group.
		/// </summary>
		private void DrawAdvancedSettings()
		{
			_AdvancedFoldout = EditorGUILayout.Foldout(_AdvancedFoldout, "Advanced Settings", true, EditorStyles.foldoutHeader);
			if (_AdvancedFoldout)
			{
				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(_SettingsProp.FindPropertyRelative("MaxPacketSize"));
				EditorGUILayout.PropertyField(_SettingsProp.FindPropertyRelative("TcpBufferSize"));
				EditorGUILayout.PropertyField(_SettingsProp.FindPropertyRelative("DebugUdpMessages"));
				EditorGUILayout.PropertyField(_SettingsProp.FindPropertyRelative("DebugTcpMessages"));
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.Space();
		}

		/// <summary>
		/// Draws the UnityEvent fields in a foldout group.
		/// </summary>
		private void DrawEvents()
		{
			_EventsFoldout = EditorGUILayout.Foldout(_EventsFoldout, "Events", true, EditorStyles.foldoutHeader);
			if (_EventsFoldout)
			{
				// Add null checks to prevent NullReferenceException
				if (_OnConnectedProp != null)
					EditorGUILayout.PropertyField(_OnConnectedProp);
				else
					EditorGUILayout.HelpBox("OnConnected event property not found.", MessageType.Warning);

				if (_OnPacketReceivedProp != null)
					EditorGUILayout.PropertyField(_OnPacketReceivedProp);
				else
					EditorGUILayout.HelpBox("OnPacketReceived event property not found.", MessageType.Warning);

				if (_OnDisconnectedProp != null)
					EditorGUILayout.PropertyField(_OnDisconnectedProp);
				else
					EditorGUILayout.HelpBox("OnDisconnected event property not found.", MessageType.Warning);
			}
		}


		/// <summary>
		/// Formats bytes into a human-readable string.
		/// </summary>
		/// <param name="_bytes">The number of bytes.</param>
		/// <returns>A formatted string (e.g., "1.2 KB").</returns>
		private string FormatBytes(long _bytes)
		{
			if (_bytes < 1024) return $"{_bytes} B";
			if (_bytes < 1024 * 1024) return $"{_bytes / 1024.0:F1} KB";
			if (_bytes < 1024 * 1024 * 1024) return $"{_bytes / (1024.0 * 1024.0):F1} MB";
			return $"{_bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
		}

		#endregion
	}
}
#endif

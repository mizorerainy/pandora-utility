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
		#region Internal & Interface Implementations

		// Draws the initial setup wizard for unconfigured components.
		private void DrawSetupWizard()
		{
			EditorGUILayout.BeginVertical("box");
			var headerStyle = new GUIStyle(EditorStyles.boldLabel)
			{
				fontSize = 16,
				alignment = TextAnchor.MiddleCenter
			};
			EditorGUILayout.LabelField("AetherLink Setup Wizard", headerStyle);
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Select the operating mode for this component:");

			var linkModeProp = _SettingsProp.FindPropertyRelative("LinkMode");
			
			EditorGUILayout.BeginHorizontal();
			var oldColor = GUI.backgroundColor;
			GUI.backgroundColor = _MasterColor;
			if (GUILayout.Button("SERVER / MASTER", GUILayout.Height(30)))
			{
				linkModeProp.enumValueIndex = (int)AetherLink.Mode.Master;
			}
			GUI.backgroundColor = _SlaveColor;
			if (GUILayout.Button("CLIENT / SLAVE", GUILayout.Height(30)))
			{
				linkModeProp.enumValueIndex = (int)AetherLink.Mode.Slave;
			}
			GUI.backgroundColor = oldColor;
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Choose your Network Address (Leave default for Localhost):");
			EditorGUILayout.PropertyField(_SettingsProp.FindPropertyRelative("UdpBroadcastPort"));
			EditorGUILayout.PropertyField(_SettingsProp.FindPropertyRelative("TcpConnectionPort"));

			EditorGUILayout.Space();
			EditorGUILayout.Space();

			EditorGUILayout.BeginHorizontal();
			GUI.backgroundColor = Color.green;
			if (GUILayout.Button("Complete Setup", GUILayout.Height(25)))
			{
				_IsConfiguredProp.boolValue = true;
			}
			GUI.backgroundColor = oldColor;
			
			if (GUILayout.Button("Skip Wizard", GUILayout.Height(25)))
			{
				_IsConfiguredProp.boolValue = true;
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.EndVertical();
		}

		// Draws the header section.
		private new void DrawHeader()
		{
			var linkModeProp = _SettingsProp.FindPropertyRelative("LinkMode");
			var currentMode = (AetherLink.Mode)linkModeProp.enumValueIndex;
			string modeLabel = currentMode == AetherLink.Mode.Master ? "MASTER MODE" : "SLAVE MODE";
			string switchLabel = currentMode == AetherLink.Mode.Master ? "Switch to SLAVE" : "Switch to MASTER";

			var originalColor = GUI.backgroundColor;
			GUI.backgroundColor = currentMode == AetherLink.Mode.Master ? _MasterColor : _SlaveColor;

			EditorGUILayout.BeginVertical("box");
			EditorGUILayout.BeginHorizontal();
			
			var headerStyle = new GUIStyle(EditorStyles.boldLabel)
			{
				fontSize = 16,
				alignment = TextAnchor.MiddleLeft,
				richText = true
			};
			EditorGUILayout.LabelField($"AETHERLINK <i>({modeLabel})</i>", headerStyle);

			GUI.enabled = !Application.isPlaying;
			GUI.backgroundColor = currentMode == AetherLink.Mode.Master ? _SlaveColor : _MasterColor;
			if (GUILayout.Button(switchLabel, GUILayout.Width(130), GUILayout.Height(24)))
			{
				linkModeProp.enumValueIndex = currentMode == AetherLink.Mode.Master 
					? (int)AetherLink.Mode.Slave 
					: (int)AetherLink.Mode.Master;
			}
			GUI.backgroundColor = currentMode == AetherLink.Mode.Master ? _MasterColor : _SlaveColor;
			GUI.enabled = true;

			EditorGUILayout.EndHorizontal();
			
			var subStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft };
			EditorGUILayout.LabelField("High Performance Network Transport", subStyle);
			EditorGUILayout.EndVertical();

			GUI.backgroundColor = originalColor;
			EditorGUILayout.Space();
		}

		// Draws validation warnings for common configuration issues.
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



		// Draws the real-time status box shown in Play Mode.
		// Param _link: The target AetherLink instance.
		private void DrawStatusBox(AetherLink _link)
		{
			var linkModeProp = _SettingsProp.FindPropertyRelative("LinkMode");
			var currentMode = (AetherLink.Mode)linkModeProp.enumValueIndex;
			
			var originalColor = GUI.backgroundColor;
			GUI.backgroundColor = currentMode == AetherLink.Mode.Master ? _MasterColor : _SlaveColor;

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
			GUI.backgroundColor = originalColor;
		}

		// Draws the Start/Stop control buttons shown in Play Mode.
		// Param _link: The target AetherLink instance.
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

		// Draws network statistics in Play Mode with enhanced error tracking.
		// Param _link: The target AetherLink instance
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

		// Evaluates and displays the quality of the network connection
		// based on statistical data from the provided AetherLink instance.
		// Param _link: The AetherLink instance containing network statistics to evaluate.
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


		// Draws the main configuration settings organized into a tabbed toolbar.
		private void DrawTabbedSettings()
		{
			EditorGUILayout.BeginVertical("box");
			EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
			EditorGUILayout.Space(2);

			_SettingsTab = GUILayout.Toolbar(_SettingsTab, _TabNames, GUILayout.Height(25));
			EditorGUILayout.Space();

			EditorGUI.indentLevel++;

			switch (_SettingsTab)
			{
				case 0: // General
					DrawGeneralSettingsTab();
					break;
				case 1: // Network
					DrawNetworkSettingsTab();
					break;
				case 2: // Advanced
					DrawAdvancedSettingsTab();
					break;
			}

			EditorGUI.indentLevel--;
			EditorGUILayout.EndVertical();
			EditorGUILayout.Space();
		}

		private void DrawGeneralSettingsTab()
		{
			EditorGUILayout.PropertyField(_SettingsProp.FindPropertyRelative("StartOnEnable"));
			EditorGUILayout.PropertyField(_SettingsProp.FindPropertyRelative("StopOnPause"));
			
			GUI.enabled = !Application.isPlaying;
			EditorGUILayout.PropertyField(_SettingsProp.FindPropertyRelative("AllowSameMachineConnection"));
			GUI.enabled = true;
		}

		private void DrawNetworkSettingsTab()
		{
			GUI.enabled = !Application.isPlaying;
			EditorGUILayout.PropertyField(_SettingsProp.FindPropertyRelative("UdpBroadcastPort"));
			EditorGUILayout.PropertyField(_SettingsProp.FindPropertyRelative("TcpConnectionPort"));
			GUI.enabled = true;
			
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Timings (Milliseconds)", EditorStyles.miniBoldLabel);
			EditorGUILayout.PropertyField(_SettingsProp.FindPropertyRelative("HandshakeInterval"));
			EditorGUILayout.PropertyField(_SettingsProp.FindPropertyRelative("HeartbeatInterval"));
			EditorGUILayout.PropertyField(_SettingsProp.FindPropertyRelative("HeartbeatTimeout"));
		}

		private void DrawAdvancedSettingsTab()
		{
			EditorGUILayout.LabelField("Data Allocation Limit", EditorStyles.miniBoldLabel);
			EditorGUILayout.PropertyField(_SettingsProp.FindPropertyRelative("MaxPacketSize"));
			EditorGUILayout.PropertyField(_SettingsProp.FindPropertyRelative("TcpBufferSize"));
			
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Diagnosis", EditorStyles.miniBoldLabel);
			EditorGUILayout.PropertyField(_SettingsProp.FindPropertyRelative("DebugUdpMessages"));
			EditorGUILayout.PropertyField(_SettingsProp.FindPropertyRelative("DebugTcpMessages"));
		}




		// Formats bytes into a human-readable string.
		// Param _bytes: The number of bytes.
		// Returns: A formatted string (e.g., "1.2 KB").
		private string FormatBytes(long _bytes)
		{
			if (_bytes < 1024) return $"{_bytes} B";
			if (_bytes < 1024 * 1024) return $"{_bytes / 1024.0:F1} KB";
			if (_bytes < 1024 * 1024 * 1024) return $"{_bytes / (1024.0 * 1024.0):F1} MB";
			return $"{_bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
		}

		// Draws the Simulation Tools section allowing developers to fake network events.
		// Param _link: The target AetherLink instance
		private void DrawSimulationTools(AetherLink _link)
		{
			_SimulationFoldout = EditorGUILayout.Foldout(_SimulationFoldout, "Packet Simulation (Editor Only)", true, EditorStyles.foldoutHeader);
			if (_SimulationFoldout)
			{
				EditorGUILayout.BeginVertical("box");
				
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField("Connection Simulation", EditorStyles.boldLabel);
				
				GUI.enabled = !_link.IsConnected;
				if (GUILayout.Button("Simulate Connect"))
				{
					_link.EditorSimulateConnect();
				}
				
				GUI.enabled = _link.IsConnected;
				if (GUILayout.Button("Simulate Disconnect"))
				{
					_link.EditorSimulateDisconnect();
				}
				GUI.enabled = true;
				
				EditorGUILayout.EndHorizontal();

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Packet Simulation Profiles", EditorStyles.boldLabel);

				if (_AvailableProfiles == null || _AvailableProfiles.Length == 0)
				{
					EditorGUILayout.HelpBox("No Simulation Profiles found in the project. Create one to test packets.", MessageType.Info);
					if (GUILayout.Button("+ Create New Profile Asset..."))
					{
						CreateNewSimulationProfile();
					}
				}
				else
				{
					EditorGUILayout.BeginHorizontal();
					_SelectedProfileIndex = EditorGUILayout.Popup("Active Profile", _SelectedProfileIndex, _ProfileNames);
					if (GUILayout.Button("Refresh", GUILayout.Width(65))) RefreshSimulationProfiles();
					if (GUILayout.Button("Edit Asset", GUILayout.Width(80))) Selection.activeObject = _AvailableProfiles[_SelectedProfileIndex];
					EditorGUILayout.EndHorizontal();

					var selectedProfile = _AvailableProfiles[_SelectedProfileIndex];
					if (selectedProfile != null && selectedProfile.Packets != null)
					{
						EditorGUILayout.Space();
						for (int i = 0; i < selectedProfile.Packets.Count; i++)
						{
							var packet = selectedProfile.Packets[i];
							
							EditorGUILayout.BeginVertical("box");
							string foldKey = $"AetherLink_Profile_{selectedProfile.name}_Packet_{i}_Fold";
							bool isExpanded = EditorPrefs.GetBool(foldKey, false);

							EditorGUILayout.BeginHorizontal();
							isExpanded = EditorGUILayout.Foldout(isExpanded, packet.Name, true, EditorStyles.foldoutHeader);
							EditorPrefs.SetBool(foldKey, isExpanded);

							var oldColor = GUI.backgroundColor;

							if (!isExpanded)
							{
								GUI.backgroundColor = new Color(0.3f, 0.7f, 0.3f); // Green
								if (GUILayout.Button("Receive", GUILayout.Width(70)))
								{
									byte[] payloadBytes = System.Text.Encoding.UTF8.GetBytes(packet.JsonPayload ?? "");
									_link.EditorSimulateReceive(packet.Header, payloadBytes);
								}

								GUI.backgroundColor = new Color(0.3f, 0.5f, 0.9f); // Blue
								if (GUILayout.Button("Send", GUILayout.Width(50)))
								{
									byte[] payloadBytes = System.Text.Encoding.UTF8.GetBytes(packet.JsonPayload ?? "");
									_link.EditorSimulateSend(packet.Header, payloadBytes);
								}
								GUI.backgroundColor = oldColor;
							}
							EditorGUILayout.EndHorizontal();

							if (isExpanded)
							{
								EditorGUILayout.Space(2);
								EditorGUILayout.BeginHorizontal();

								EditorGUILayout.BeginVertical();
								EditorGUI.BeginChangeCheck();
								ushort newHeader = (ushort)EditorGUILayout.IntField("Header ID", packet.Header);
								EditorGUILayout.LabelField("String Payload (Converted to UTF8 Bytes internally)", EditorStyles.miniLabel);

								var textAreaStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
								string newPayload = EditorGUILayout.TextArea(packet.JsonPayload, textAreaStyle, GUILayout.MinHeight(42));

								if (EditorGUI.EndChangeCheck())
								{
									packet.Header = newHeader;
									packet.JsonPayload = newPayload;
									selectedProfile.Packets[i] = packet;
									EditorUtility.SetDirty(selectedProfile);
								}
								EditorGUILayout.EndVertical();

								EditorGUILayout.Space(4);

								EditorGUILayout.BeginVertical(GUILayout.Width(130));
								var btnOpts = new GUILayoutOption[] { GUILayout.Height(30), GUILayout.ExpandWidth(true) };

								GUI.backgroundColor = new Color(0.3f, 0.7f, 0.3f); // Green
								if (GUILayout.Button("Simulate Receive", btnOpts))
								{
									byte[] payloadBytes = System.Text.Encoding.UTF8.GetBytes(packet.JsonPayload ?? "");
									_link.EditorSimulateReceive(packet.Header, payloadBytes);
								}

								EditorGUILayout.Space(4);

								GUI.backgroundColor = new Color(0.3f, 0.5f, 0.9f); // Blue
								if (GUILayout.Button("Simulate Send", btnOpts))
								{
									byte[] payloadBytes = System.Text.Encoding.UTF8.GetBytes(packet.JsonPayload ?? "");
									_link.EditorSimulateSend(packet.Header, payloadBytes);
								}
								GUI.backgroundColor = oldColor;
								EditorGUILayout.EndVertical();

								EditorGUILayout.EndHorizontal();
							}
							EditorGUILayout.EndVertical();
							EditorGUILayout.Space(2);
						}
					}
					
					EditorGUILayout.Space();
					if (GUILayout.Button("+ Create New Profile Asset..."))
					{
						CreateNewSimulationProfile();
					}
				}

				EditorGUILayout.EndVertical();
			}
			EditorGUILayout.Space();
		}

		private void CreateNewSimulationProfile()
		{
			var path = EditorUtility.SaveFilePanelInProject("Create Simulation Profile", "NewSimulationProfile", "asset", "Create a new simulation profile asset");
			if (!string.IsNullOrEmpty(path))
			{
				var newProfile = ScriptableObject.CreateInstance<SimulationProfile>();
				newProfile.Packets.Add(new SimulationPacket { Name = "Sample Packet", Header = 1000, JsonPayload = "{}" });
				AssetDatabase.CreateAsset(newProfile, path);
				AssetDatabase.SaveAssets();
				RefreshSimulationProfiles();
				
				// Auto-select the newly created profile
				for (int i = 0; i < _AvailableProfiles.Length; i++)
				{
					if (_AvailableProfiles[i] == newProfile)
					{
						_SelectedProfileIndex = i;
						break;
					}
				}
			}
		}

		#endregion
	}
}
#endif

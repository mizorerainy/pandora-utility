using System;
using System.Collections.Generic;
using UnityEngine;

namespace MizoreRainy.Pandora.NetworkUtility.Editor
{
	[Serializable]
	public struct SimulationPacket
	{
		[Tooltip("A name to identify this packet in the profile.")]
		public string Name;
		
		[Tooltip("The ushort header ID for this packet.")]
		public ushort Header;
		
		[Tooltip("The JSON payload data, sent as UTF-8 bytes.")]
		[TextArea(3, 10)]
		public string JsonPayload;
	}

	[CreateAssetMenu(fileName = "NewSimulationProfile", menuName = "Pandora/Network/Simulation Profile", order = 100)]
	public class SimulationProfile : ScriptableObject
	{
		[Tooltip("Display name for this simulation profile.")]
		public string ProfileName = "New Profile";

		[Tooltip("List of pre-configured simulation packets to send or receive.")]
		public List<SimulationPacket> Packets = new List<SimulationPacket>();
	}
}

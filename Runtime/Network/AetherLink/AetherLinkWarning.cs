#if !HAVE_CYSHARP_UNITASK
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace MizoreRainy.Pandora.NetworkUtility
{
	/// <summary>
	/// Represents a networking utility component designed to manage connection modes
	/// and communication between Master and Slave nodes within the Pandora framework.
	/// </summary>
	public partial class AetherLink : MonoBehaviour
	{
		/// <summary>
		/// Logs a warning message indicating that the UniTask package is required for full functionality
		/// of the AetherLink class. Provides guidance for installing the UniTask package or accessing
		/// the Network Utility Setup window.
		/// </summary>
		private void ShowUniTaskWarning()
		{
			Debug.LogWarning("UniTask package is required for full AetherLink functionality. " +
			                 "Please install UniTask or use Window > Pandora > Network Utility Setup");
		}

		/// <summary>
		/// Initializes the component when the script instance is being loaded.
		/// This method will display a warning if the UniTask package is not installed,
		/// indicating that the full functionality of AetherLink requires UniTask.
		/// </summary>
		private void Awake()
		{
			ShowUniTaskWarning();
		}
	}
}
#endif
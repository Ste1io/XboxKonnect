/*
 * Console Auto Discovery and Status Scanner
 * Created by Stelio Kontos
 * Date: 10/24/2017
 */
 
namespace SK.XboxKonnect
{
	/// <summary>
	/// Enum identifying the console's current connection state.
	/// </summary>
	public enum ConnectionState
	{
		/// <summary>
		/// Initial connection type upon discovery.
		/// </summary>
		None = 0,

		/// <summary>
		/// Console is offline.
		/// </summary>
		Offline = 1,

		/// <summary>
		/// Console is online.
		/// </summary>
		Online = 2,
	}

}

/*
 * Console Auto Discovery and Status Scanner
 * 
 * Coded by Stelio Kontos,
 * aka Daniel McClintock
 * 
 * Created: 10/24/2017
 * Updated: 01/20/2020
 * 
 */

using System;

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
		Unknown = 0,

		/// <summary>
		/// This enum member is obsolete. Use <see cref="Unknown"/> instead.
		/// </summary>
		[Obsolete("This enum member is obsolete. Use ConnectionState.Unknown instead.", false)]
		None = Unknown,

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

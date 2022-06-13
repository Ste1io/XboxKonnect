/*
 * XboxKonnect - Xbox Auto Discovery API
 * 
 * Created: 10/24/2017
 * Author:  Daniel McClintock (alias: Stelio Kontos)
 * 
 * Copyright (c) 2017 Daniel McClintock
 * 
 */

using System;

namespace SK.XboxKonnect
{
	// TODO Add distinction between wifi/network/bridged connections

	/// <summary>
	/// Enum identifying the console's current network connection type.
	/// </summary>
	public enum ConnectionType
	{
		/// <summary>
		/// No connection.
		/// </summary>
		Unknown = 0,

		/// <summary>
		/// This enum member is obsolete. Use <see cref="Unknown"/> instead.
		/// </summary>
		[Obsolete("This enum member is obsolete. Use ConnectionType.Unknown instead.", false)]
		None = Unknown,

		/// <summary>
		/// Console is connected on the Local Area Network (wired connection).
		/// </summary>
		LAN = 1,

		/// <summary>
		/// Unused. Console is connected on the local Wireless Area Network (WiFi).
		/// </summary>
		Wireless = 2,

		/// <summary>
		/// Console is connected on a bridged connection to this device.
		/// </summary>
		Bridged = 3,
	}

}

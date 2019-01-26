/*
 * Console Auto Discovery and Status Scanner
 * Created by Stelio Kontos
 * Date: 10/24/2017
 */
using System;

namespace XboxKonnect
{
	//todo: Add distinction between wifi/network/bridged connections

	/// <summary>
	/// Bitflag representation of the console's current network connection state.
	/// </summary>
	[Flags]
	public enum ConnectionFlags
	{
		Offline = 0x0,
		Online = 0x1,
		LAN = 0x2,
		Wireless = 0x4,
		Bridged = 0x8,
		Default = 0x10,
	}

}

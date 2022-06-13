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
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace SK.XboxKonnect
{
	/// <summary>
	/// Utility methods for <see cref="ConsoleScanner"/> class.
	/// </summary>
	public static class Utils
	{
		internal static IPEndPoint GetHostEndPoint()
		{
			using (Socket udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
			{
				udpSocket.Connect("10.0.20.20", 31337);
				return udpSocket.LocalEndPoint as IPEndPoint;
			}
		}

		internal static string GetSubnetRange(IPEndPoint endpoint)
		{
			return String.Join(".", endpoint.Address.GetAddressBytes().Take(3));
		}

		/// <summary>
		/// Static construction helper to simplify object creation of <see cref="ConsoleScanner"/> class.
		/// </summary>
		/// <param name="scanner">The <see cref="ConsoleScanner"/> object being constructed.</param>
		/// <returns></returns>
		public static ConsoleScanner StartScanning(this ConsoleScanner scanner)
		{
			scanner.Start();
			return scanner;
		}
	}
}

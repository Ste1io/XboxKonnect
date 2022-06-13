/*
 * XboxKonnect - Xbox Auto Discovery API
 * 
 * Created: 10/24/2017
 * Author:  Daniel McClintock (alias: Stelio Kontos)
 * 
 * Copyright (c) 2017 Daniel McClintock
 * 
 */

using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace SK.XboxKonnect
{
	internal static class Utils
	{
		internal static IPEndPoint? GetHostEndPoint()
		{
			using Socket udpSocket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			udpSocket.Connect("10.0.20.20", 31337);
			return udpSocket.LocalEndPoint as IPEndPoint;
		}

		internal static IPAddress GetBroadcastAddress(this IPAddress ip)
		{
			ReadOnlySpan<byte> span = ip.GetAddressBytes();
			uint addr = MemoryMarshal.Read<uint>(span);
			uint mask = 0x00FFFFFF;
			return new IPAddress(addr | ~(mask));
		}
	}
}

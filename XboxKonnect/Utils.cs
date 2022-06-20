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
using System.Net.NetworkInformation;

namespace SK.XboxKonnect
{
	internal static class Utils
	{
		internal static IEnumerable<(NetworkInterface NIC, IPAddress BroadcastAddress)> GetSubnets()
		{
			return NetworkInterface.GetAllNetworkInterfaces()
				.Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
				.SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses
					.Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
					.Select(ipInfo => (adapter, ipInfo.Address.GetBroadcastAddress()))
				);
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

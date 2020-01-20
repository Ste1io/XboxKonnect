/*
 * Console Auto Discovery and Status Scanner
 * Created by Stelio Kontos
 * Date: 10/24/2017
 */

#define DEBUG

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SK.XboxKonnect
{
	public partial class ConsoleScanner
	{
		private static readonly string _subnetRangeBridged = "192.168.137";

		// ..jtag
		private static readonly byte[] _responseJtag = {
			0x03, 0x04, 0x6a, 0x74, 0x61, 0x67
		};

		// ..XeDevkit
		private static readonly byte[] _responseDevkit =
		{
			0x03, 0x04, 0x58, 0x65, 0x44, 0x65, 0x76, 0x6B, 0x69, 0x74
		};

		private List<string> _subnetRanges;
		private IPEndPoint _hostEndPoint;
		private IPEndPoint _clientEndPoint;
		private UdpClient _udpScanner;

		#region Public Properties

		/// <summary>
		/// Stores all current console connections, disconnections, and basic connection details.
		/// </summary>
		public Dictionary<string, Connection> Connections { get; private set; } = new Dictionary<string, Connection>();

		/// <summary>
		/// Returns whether connection scanning is currently active or not.
		/// </summary>
		public bool Scanning { get; private set; } = false;

		/// <summary>
		/// Get or set the frequency to scan for connection changes on the local network.
		/// </summary>
		public TimeSpan ScanFrequency { get; set; }

		/// <summary>
		/// The maximum amount of time before a non-responsive connection is considered offline.
		/// </summary>
		public TimeSpan DisconnectTimeout { get; set; }

		/// <summary>
		/// Whether offline connections should be automatically purged from the connection list or not.
		/// </summary>
		public bool RemoveOnDisconnect { get; set; }

		#endregion

		#region Events

		public event EventHandler<OnAddConnectionEventArgs> AddConnectionEvent;
		public event EventHandler<OnUpdateConnectionEventArgs> UpdateConnectionEvent;
		public event EventHandler<OnRemoveConnectionEventArgs> RemoveConnectionEvent;

		protected virtual void OnAddConnection(Connection xboxConnection)
		{
			AddConnectionEvent?.Invoke(this, new OnAddConnectionEventArgs(xboxConnection));
		}

		protected virtual void OnUpdateConnection(Connection xboxConnection)
		{
			UpdateConnectionEvent?.Invoke(this, new OnUpdateConnectionEventArgs(xboxConnection));
		}

		protected virtual void OnRemoveConnection(Connection xboxConnection)
		{
			RemoveConnectionEvent?.Invoke(this, new OnRemoveConnectionEventArgs(xboxConnection));
		}

		#endregion

		/// <summary>
		/// Initializes a new instance of the <see cref="ConsoleScanner"/> class.
		/// </summary>
		/// <param name="autoStart">Begin passive console scan automatically.</param>
		/// <param name="frequency">Delay interval between pings.</param>
		public ConsoleScanner(bool autoStart, TimeSpan frequency)
		{
			this._hostEndPoint = Utils.GetHostEndPoint();
			this._subnetRanges = GetSubnetRanges();
			this.ScanFrequency = frequency;
			this.DisconnectTimeout = new TimeSpan(0, 0, 3);
			this.RemoveOnDisconnect = false;

			if (autoStart)
				Start();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ConsoleScanner"/> class.
		/// </summary>
		/// <param name="autoStart">Begin passive console scan automatically.</param>
		public ConsoleScanner(bool autoStart) :
			this(autoStart, new TimeSpan(0, 0, 0, 1))
		{ }

		/// <summary>
		/// Initializes a new instance of the <see cref="ConsoleScanner"/> class.
		/// </summary>
		/// <param name="frequency">Delay interval between pings.</param>
		public ConsoleScanner(TimeSpan frequency) :
			this(false, frequency)
		{ }

		/// <summary>
		/// Initializes a new instance of the <see cref="ConsoleScanner"/> class.
		/// </summary>
		public ConsoleScanner() :
			this(false, new TimeSpan(0, 0, 0, 1))
		{ }

		#region Private Methods

		private List<string> GetSubnetRanges()
		{
			return new List<string>
			{
				String.Format("{0}.255", Utils.GetSubnetRange(_hostEndPoint)),
				String.Format("{0}.255", _subnetRangeBridged)
			};
		}

		private void AddConnection(Connection xbox)
		{
			try
			{
				lock (Connections)
					Connections.Add(xbox.IP, xbox);
				OnAddConnection(xbox);
			}
			catch (Exception ex)
			{
				Trace.WriteLine(ex);
			}
		}

		private void RemoveConnection(Connection xbox)
		{
			try
			{
				lock (Connections)
					Connections.Remove(xbox.IP);
				OnRemoveConnection(xbox);
			}
			catch (Exception ex)
			{
				Trace.WriteLine(ex);
			}
		}

		//private void UpdateConnectionState(Connection xbox)
		//{
		//	try
		//	{
		//		Connections[xbox.IP].ConnectionState = xbox.ConnectionState;
		//		OnUpdateConnection(xbox);
		//	}
		//	catch (Exception ex)
		//	{
		//		Trace.WriteLine(ex);
		//	}
		//}

		private void UpdateConnectionState(string ip, ConnectionState newState)
		{
			try
			{
				if (Connections.TryGetValue(ip, out Connection xbox))
				{
					Connections[ip].ConnectionState = newState;
					OnUpdateConnection(xbox);
				}
			}
			catch (Exception ex)
			{
				Trace.WriteLine(ex);
			}

		}

		private void ProcessResponse(UdpReceiveResult receiveResult)
		{
			string response = Encoding.ASCII.GetString(receiveResult.Buffer).Remove(0, 2);
			string ip = receiveResult.RemoteEndPoint.Address.ToString();

			if (Connections.ContainsKey(ip))
			{
				Connections[ip].LastPing = DateTime.Now;
				if (Connections[ip].ConnectionState != ConnectionState.Online)
				{
					UpdateConnectionState(ip, ConnectionState.Online);
				}
			}
			else
			{
				Connection xbox = Connection.NewXboxConnection();
				xbox.response = response;
				xbox.ConnectionState = ConnectionState.Online;
				xbox.ConsoleType = ConsoleType.Jtag;
				xbox.IP = ip;

				if (Convert.ToBoolean(xbox.IP.Split('.')[2].Equals("137")))
					xbox.ConnectionType = ConnectionType.Bridged;
				else
					xbox.ConnectionType = ConnectionType.LAN;

				AddConnection(xbox);
			}
		}

		private void Listen()
		{
			Task.Run(async () =>
			{
				while (Scanning)
				{
					var response = await _udpScanner.ReceiveAsync();
					ProcessResponse(response);
				}
			});
		}

		private async void BroadcastAsync()
		{
			while (Scanning)
			{
				foreach (var range in _subnetRanges)
				{
					try
					{
						_udpScanner.Send(_responseJtag, _responseJtag.Length, range, 730);
					}
					catch (Exception ex)
					{
						Trace.WriteLine(ex);
					}
				}

				await Task.Delay(ScanFrequency);
			}
		}

		private async void MonitorAsync()
		{
			while (Scanning)
			{
				var consoles = new List<Connection>(Connections.Values).Where(x => DateTime.Now.Subtract(x.LastPing) > DisconnectTimeout);

				foreach (var xbox in consoles)
				{
					switch (xbox.ConnectionState)
					{
						case ConnectionState.None:
						case ConnectionState.Offline:
							if (RemoveOnDisconnect)
								RemoveConnection(xbox);
							break;
						case ConnectionState.Online:
							UpdateConnectionState(xbox.IP, ConnectionState.Offline);
							break;
						default:
							break;
					}
				}

				await Task.Delay(ScanFrequency);
			}
		}

		#endregion

		#region Public Methods

		/// <summary>
		/// Start monitoring the local network for new or changed connections.
		/// </summary>
		public void Start()
		{
			if (Scanning)
				return;

			_clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
			_udpScanner = new UdpClient(_clientEndPoint);
			_hostEndPoint = Utils.GetHostEndPoint();
			_subnetRanges = GetSubnetRanges();

			Scanning = true;

			Listen();

			var broadcastTask = Task.Run(() => { BroadcastAsync(); });
			var monitorTask = Task.Run(() => { MonitorAsync(); });

			Debug.WriteLine(String.Format("[XboxKonnect] Monitoring {0} local network ranges for new or changed connections.", _subnetRanges.Count));
		}

		/// <summary>
		/// Stop monitoring the local network for new or changed connections.
		/// </summary>
		public void Stop()
		{
			if (!Scanning)
				return;

			Scanning = false;
			_udpScanner.Close();
			_clientEndPoint = null;
			_subnetRanges.Clear();

			Debug.WriteLine("[XboxKonnect] Scanning stopped.");
		}

		/// <summary>
		/// Purge stale connections from Connections list.
		/// </summary>
		public void PurgeList()
		{
			var consoles = new List<Connection>(Connections.Values).Where(x => x.ConnectionState.Equals(ConnectionState.Offline));

			foreach (var xbox in consoles)
				RemoveConnection(xbox);

			Debug.WriteLine("[XboxKonnect] Purged Connections list");
		}

		#endregion

	}

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

		public static ConsoleScanner StartScanning(this ConsoleScanner scanner)
		{
			scanner.Start();
			return scanner;
		}
	}
}

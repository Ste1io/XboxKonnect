/*
 * XboxKonnect - Xbox Auto Discovery API
 * 
 * Created: 10/24/2017
 * Author:  Daniel McClintock (alias: Stelio Kontos)
 * 
 * Copyright (c) 2017 Daniel McClintock
 * 
 */

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SK.XboxKonnect
{
	/// <summary>
	/// Scans the local network for console connections, and manages connection state of discovered consoles.
	/// </summary>
	public partial class ConsoleScanner
	{
		private static readonly byte[] _responseJtag = { 0x03, 0x04, 0x6a, 0x74, 0x61, 0x67 }; // ..jtag
		private static readonly byte[] _responseXdk = { 0x03, 0x04, 0x58, 0x65, 0x44, 0x65, 0x76, 0x6B, 0x69, 0x74 }; // ..XeDevkit

		private volatile Boolean _scanning = false;
		private List<IPAddress> _subnetList = new();
		private UdpClient? _udpClient;

		#region Public Properties

		/// <summary>
		/// Stores all current console connections, disconnections, and basic connection details.
		/// </summary>
		public Dictionary<IPAddress, Connection> Connections { get; init; } = new Dictionary<IPAddress, Connection>();

		/// <summary>
		/// Returns whether connection scanning is currently active or not.
		/// </summary>
		public bool Scanning { get => _scanning; private set => _scanning = value; }

		/// <summary>
		/// Get or set the frequency to scan for connection changes on the local network.
		/// Default is 3 seconds.
		/// </summary>
		public TimeSpan ScanFrequency { get; set; } = new TimeSpan(0, 0, 3);

		/// <summary>
		/// The number of failed pings before a connection is considered <see cref="ConnectionState.Offline"/>.
		/// Default is 2.
		/// </summary>
		public int TimeoutAttempts { get; set; } = 2;

		/// <summary>
		/// Whether offline connections should be automatically purged from the connection list or not.
		/// Default is false.
		/// </summary>
		public bool RemoveOnDisconnect { get; set; } = false;

		/// <summary>
		/// <para>
		/// This enum member is obsolete. Use <see cref="TimeoutAttempts"/> instead.
		/// </para>
		/// The amount of time before a non-responsive connection is considered offline,
		/// from the time of the most recent response.
		/// Value must be greater than that of <see cref="ScanFrequency"/>; if set lower,
		/// it is automatically adjusted to 1.5 x <see cref="ScanFrequency"/>'s value.
		/// Default is 6 seconds.
		/// </summary>
		[Obsolete("This property is obsolete. Use ConsoleScanner.TimeoutAttempts instead.", true)]
		public TimeSpan DisconnectTimeout => new TimeSpan(ScanFrequency.Ticks * TimeoutAttempts);

		#endregion

		#region Events

		/// <summary>
		/// Event Handler for <see cref="OnAddConnection"/> events.
		/// </summary>
		public event EventHandler<OnAddConnectionEventArgs>? AddConnectionEvent;

		/// <summary>
		/// Event Handler for <see cref="OnUpdateConnection"/> events.
		/// </summary>
		public event EventHandler<OnUpdateConnectionEventArgs>? UpdateConnectionEvent;

		/// <summary>
		/// Event Handler for <see cref="OnRemoveConnection"/> events.
		/// </summary>
		public event EventHandler<OnRemoveConnectionEventArgs>? RemoveConnectionEvent;

		/// <summary>
		/// Invokes the <see cref="AddConnectionEvent"/> Event Handler.
		/// </summary>
		/// <param name="xboxConnection">The <see cref="Connection"/> object.</param>
		protected virtual void OnAddConnection(Connection xboxConnection)
		{
			AddConnectionEvent?.Invoke(this, new OnAddConnectionEventArgs(xboxConnection));
		}

		/// <summary>
		/// Invokes the <see cref="UpdateConnectionEvent"/> Event Handler.
		/// </summary>
		/// <param name="xboxConnection">The <see cref="Connection"/> object.</param>
		protected virtual void OnUpdateConnection(Connection xboxConnection)
		{
			UpdateConnectionEvent?.Invoke(this, new OnUpdateConnectionEventArgs(xboxConnection));
		}

		/// <summary>
		/// Invokes the <see cref="RemoveConnectionEvent"/> Event Handler.
		/// </summary>
		/// <param name="xboxConnection">The <see cref="Connection"/> object.</param>
		protected virtual void OnRemoveConnection(Connection xboxConnection)
		{
			RemoveConnectionEvent?.Invoke(this, new OnRemoveConnectionEventArgs(xboxConnection));
		}

		#endregion

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="ConsoleScanner"/> class.
		/// </summary>
		public ConsoleScanner() :
			this(false, new TimeSpan(0, 0, 0, 3))
		{ }

		/// <summary>
		/// Initializes a new instance of the <see cref="ConsoleScanner"/> class.
		/// </summary>
		/// <param name="autoStart">Begin passive console scan automatically.</param>
		public ConsoleScanner(bool autoStart) :
			this(autoStart, new TimeSpan(0, 0, 0, 3))
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
		/// <param name="autoStart">Begin passive console scan automatically.</param>
		/// <param name="frequency">Delay interval between pings.</param>
		public ConsoleScanner(bool autoStart, TimeSpan frequency)
		{
			ScanFrequency = frequency;

			if (autoStart)
				Start();
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

			_subnetList = new()
			{
				IPAddress.Parse("192.168.137.255"), // bridged subnet
				Utils.GetHostEndPoint()!.Address.GetBroadcastAddress(), // lan subnet
			};

			if (_udpClient is null)
				_udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, 0));

			Scanning = _subnetList.Count > 0;

			Listen(_udpClient);

			var broadcastTask = Task.Run(() => BroadcastAsync(_udpClient));
			var monitorTask = Task.Run(() => MonitorAsync());

			Trace.WriteLine($"[XboxKonnect] Monitoring {_subnetList.Count} local network ranges for new or changed connections.");
		}

		/// <summary>
		/// Stop monitoring the local network for new or changed connections.
		/// </summary>
		public void Stop()
		{
			if (!Scanning)
				return;

			Scanning = false;

			if (_udpClient is not null)
			{
				_udpClient.Close();
				_udpClient = null;
			}

			Trace.WriteLine("[XboxKonnect] Scanning stopped.");
		}

		/// <summary>
		/// Purge stale connections from Connections list.
		/// </summary>
		public void PurgeList()
		{
			foreach (var xbox in Connections.Values.Where(x => x.ConnectionState.Equals(ConnectionState.Offline)))
				RemoveConnection(xbox);

			Trace.WriteLine("[XboxKonnect] Purged Connections list");
		}

		#endregion

		#region Private Methods

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

		private void UpdateConnectionState(IPAddress ip, ConnectionState newState)
		{
			try
			{
				if (Connections.TryGetValue(ip, out Connection? xbox))
				{
					//xbox.ConnectionState = newState;
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
			var endpoint = receiveResult.RemoteEndPoint;
			var response = Encoding.ASCII.GetString(receiveResult.Buffer)[2..];

			if (Connections.ContainsKey(endpoint.Address))
			{
				Connections[endpoint.Address].LastPing = DateTime.Now;
				if (Connections[endpoint.Address].ConnectionState != ConnectionState.Online)
				{
					UpdateConnectionState(endpoint.Address, ConnectionState.Online);
				}
			}
			else
			{
				AddConnection(new Connection
				{
					EndPoint = endpoint,
					Name = response,
					ConsoleType = ConsoleType.Jtag,
					ConnectionType = endpoint.Address.GetAddressBytes()[2].Equals("137") ? ConnectionType.Bridged : ConnectionType.LAN,
					ConnectionState = ConnectionState.Online,
				});
			}
		}

		private void Listen(UdpClient udpClient)
		{
			Task.Run(async () =>
			{
				while (Scanning)
				{
					try
					{
						var response = await udpClient.ReceiveAsync();
						ProcessResponse(response);
					}
					catch (Exception ex)
					{
						Trace.WriteLine(ex);
					}
				}
			});
		}

		private async void BroadcastAsync(UdpClient udpClient)
		{
			while (Scanning)
			{
				foreach (var range in _subnetList)
				{
					try
					{
						//udpClient.Send(_responseDevkit, _responseDevkit.Length, range.ToString(), 730);
						udpClient.Send(_responseJtag, _responseJtag.Length, range.ToString(), 730);
					}
					catch (Exception ex)
					{
						Trace.WriteLine($"Exception broadcasting to: {range}");
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
				foreach (var xbox in Connections.Values.Where(x => DateTime.Now.Ticks > (x.LastPing.Ticks + (ScanFrequency.Ticks * TimeoutAttempts))))
				{
					switch (xbox.ConnectionState)
					{
						case ConnectionState.Unknown:
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
	}
}

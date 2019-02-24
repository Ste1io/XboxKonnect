/*
 * Console Auto Discovery and Status Scanner
 * Created by Stelio Kontos
 * Date: 10/24/2017
 */
//#undef DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace XboxKonnect
{
	public partial class ConsoleScanner
	{
		private static readonly string subnetRangeBridged = "192.168.137";
		private static readonly byte[] pingPacketMsgData = {
			0x03,
			0x04,
			0x6a,
			0x74,
			0x61,
			0x67
		};

		private UdpClient udpClientScanner;
		private IPEndPoint myEndPoint;
		private IPEndPoint localIpEndPoint;
		private List<String> subnetRanges;

		public ConsoleController ConsoleController {
			get;
			set;
		}

		public TimeSpan Frequency {
			get;
			set;
		}

		public bool Scanning {
			get;
			private set;
		}

		#region Events

		public event EventHandler<OnAddConnectionEventArgs> AddConnectionEvent;

		public event EventHandler<OnRemoveConnectionEventArgs> RemoveConnectionEvent;

		protected virtual void OnAddConnection(ConsoleConnection xboxConnection)
		{
			AddConnectionEvent?.Invoke(this, new OnAddConnectionEventArgs(xboxConnection));
		}

		protected virtual void OnRemoveConnection(ConsoleConnection xboxConnection)
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
			this.ConsoleController = new ConsoleController();

			this.myEndPoint = GetMyEndPoint();
			this.subnetRanges = GetSubnetRanges();
			this.Frequency = frequency;
			this.Scanning = false;

			if (autoStart)
				Start();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ConsoleScanner"/> class.
		/// </summary>
		/// <param name="autoStart">Begin passive console scan automatically.</param>
		public ConsoleScanner(bool autoStart) :
			this(autoStart, new TimeSpan(0, 0, 0, 1))
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ConsoleScanner"/> class.
		/// </summary>
		/// <param name="frequency">Delay interval between pings.</param>
		public ConsoleScanner(TimeSpan frequency) :
			this(false, frequency)
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ConsoleScanner"/> class.
		/// </summary>
		public ConsoleScanner() :
			this(false, new TimeSpan(0, 0, 0, 1))
		{

		}

		#region Private Methods

		private IPEndPoint GetMyEndPoint()
		{
			using (Socket udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
			{
				udpSocket.Connect("10.0.20.20", 31337);
				return udpSocket.LocalEndPoint as IPEndPoint;
			}
		}

		private String GetSubnetRange(IPEndPoint endpoint)
		{
			return String.Join(".", endpoint.Address.GetAddressBytes().Take(3));
		}

		private List<String> GetSubnetRanges()
		{
			return new List<String>
			{
				String.Format("{0}.255", GetSubnetRange(myEndPoint)),
				String.Format("{0}.255", subnetRangeBridged)
			};
		}

		private void AddConnection(ConsoleConnection xbox)
		{
			try
			{
				lock (ConsoleController.ConnectedConsoles)
				{
					ConsoleController.ConnectedConsoles.Add(xbox.IP, xbox);
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
			}

			OnAddConnection(xbox);
		}

		private void UpdateConnection(ConsoleConnection xbox)
		{
			try
			{
				lock (ConsoleController.ConnectedConsoles)
				{
					ConsoleController.ConnectedConsoles[xbox.IP].LastPing = xbox.LastPing;
					ConsoleConnection updXbox = ConsoleController.ConnectedConsoles[xbox.IP];
					SetConnectionType(ref updXbox);
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
			}
		}

		private void SetConnectionFlag(ConsoleConnection xbox, ConnectionFlags flag)
		{
			try
			{
				lock (ConsoleController.ConnectedConsoles)
				{
					ConsoleController.ConnectedConsoles[xbox.IP].ConnectionState = flag;
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
			}

			Debug.WriteLine(String.Format("Connection flag set to {0}", flag));
		}

		private void PurgeConnection(ConsoleConnection xbox)
		{
			try
			{
				lock (ConsoleController.ConnectedConsoles)
					ConsoleController.ConnectedConsoles.Remove(xbox.IP);
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
			}

			Debug.WriteLine(String.Format("[XboxKonnect] {0} PURGED", xbox.IP));
			OnRemoveConnection(xbox);
		}

		// TODO: Fire event when stale console reconnects
		private void SetConnectionType(ref ConsoleConnection xbox)
		{
			xbox.ConnectionState |= ConnectionFlags.Online;

			if (Convert.ToBoolean(xbox.IP.Split('.')[2].Equals("137")))
			{
				xbox.ConnectionState |= ConnectionFlags.Bridged;
			}
			else
			{
				xbox.ConnectionState |= ConnectionFlags.LAN;
			}
		}

		private void ProcessResponse(UdpReceiveResult receiveResult)
		{
			var xbox = ConsoleConnection.NewXboxConnection();
			xbox.Response = Encoding.ASCII.GetString(receiveResult.Buffer).Remove(0, 2);
			xbox.IP = receiveResult.RemoteEndPoint.Address.ToString();
			xbox.LastPing = DateTime.Now;
			SetConnectionType(ref xbox);

			if (ConsoleController.ConnectedConsoles.ContainsKey(xbox.IP))
			{
				UpdateConnection(xbox);
			}
			else
			{
				AddConnection(xbox);
			}
		}

		private void ListenAsync()
		{
			Task.Run(async () =>
			{
				while (Scanning)
				{
					var response = await udpClientScanner.ReceiveAsync();
					ProcessResponse(response);
				}
			});
		}

		// TODO: Convert to async implementation
		private async void Broadcast()
		{
			while (Scanning)
			{
				foreach (var range in subnetRanges)
				{
					try
					{
						udpClientScanner.Send(pingPacketMsgData, pingPacketMsgData.Length, range, 730);
					}
					catch (Exception ex)
					{
						Debug.WriteLine(ex);
					}
				}

				await Task.Delay(Frequency);
			}
		}

		// TODO: Convert to async implementation
		private async void Monitor()
		{
			while (Scanning)
			{
				List<ConsoleConnection> consoles = new List<ConsoleConnection>(ConsoleController.ConnectedConsoles.Values);

				foreach (var xbox in consoles)
				{
					var timespan = DateTime.Now.Subtract(xbox.LastPing);
					if (timespan.TotalSeconds > 3 && xbox.ConnectionState != ConnectionFlags.Offline)
					{
						ConsoleConnection _xbox = xbox;
						SetConnectionFlag(_xbox, ConnectionFlags.Offline);
					}
				}

				await Task.Delay(Frequency);
			}
		}

		#endregion

		#region Public Methods

		/// <summary>
		/// Start scanning for connections.
		/// </summary>
		public void Start()
		{
			if (Scanning)
				return;

			localIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
			udpClientScanner = new UdpClient(localIpEndPoint);

			myEndPoint = GetMyEndPoint();
			subnetRanges = GetSubnetRanges();

			Scanning = true;

			ListenAsync();

			var broadcastTask = Task.Run(() =>
			{
				Broadcast();
			});

			var monitorTask = Task.Run(() =>
			{
				Monitor();
			});

			Debug.WriteLine(String.Format("[XboxKonnect] Scanning started on {0} ranges ({1} frequency).", subnetRanges.Count, this.Frequency));
		}

		/// <summary>
		/// Stop scanning for connections.
		/// </summary>
		public void Stop()
		{
			if (!Scanning)
				return;

			Scanning = false;
			udpClientScanner.Close();
			localIpEndPoint = null;
			subnetRanges.Clear();

			Debug.WriteLine("[XboxKonnect] Scanning stopped.");
		}

		/// <summary>
		/// Purge stale connections from list.
		/// If one or more consoles are purged, the remaining consoles
		/// will most likely be assigned a new index.
		/// </summary>
		public void PurgeList()
		{
			List<ConsoleConnection> consoles = new List<ConsoleConnection>(ConsoleController.ConnectedConsoles.Values);

			foreach (var xbox in consoles)
			{
				if (xbox.ConnectionState == ConnectionFlags.Offline)
				{
					PurgeConnection(xbox);
				}
			}

		}

		#endregion

	}
}

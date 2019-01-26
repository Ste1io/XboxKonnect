/*
 * Console Auto Discovery and Status Scanner
 * Created by Stelio Kontos
 * Date: 10/24/2017
 */
#undef DEBUG
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
	public class ConsoleScanner
	{
		private static readonly string subnetRangeBridged = "192.168.137";
		private static readonly byte[] pingPacketMsgData = { 0x03, 0x04, 0x6a, 0x74, 0x61, 0x67 };

		private UdpClient udpClientScanner;
		private IPEndPoint myEndPoint;
		private IPEndPoint localIpEndPoint;
		private List<String> subnetRanges;

		public ConsoleController ConsoleController { get; private set; }
		public TimeSpan Frequency { get; set; }
		public bool Scanning { get; private set; }

		public event Action<object, EventArgs> SubscribeToFilterChanges;
		public void AddConnectionEvent(EventArgs e)
		{
			SubscribeToFilterChanges?.Invoke(this, e);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ConsoleScanner"/> class.
		/// </summary>
		/// <param name="autoStart">Begin passive console scan automatically.</param>
		/// <param name="frequency">Delay interval between pings.</param>
		public ConsoleScanner(bool autoStart, TimeSpan frequency)
		{
			this.ConsoleController = new ConsoleController();

			// Initialize subnet ranges
			this.myEndPoint = GetMyEndPoint();
			this.subnetRanges = GetSubnetRanges();

			// Initialize scanner
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

		// TODO: Watch connection list for disconnections if last ping doesn't update
		// TODO: Add events: AddConnection, RemoveConnection, UpdateConnection

		private void AddConnection(ConsoleConnection xbox)
		{
			try {
				lock (ConsoleController.ConnectedConsoles)
					ConsoleController.ConnectedConsoles.Add(xbox.IP, xbox);
			}
			catch(Exception ex)
			{
				Debug.WriteLine(ex);
			}

			// TODO: Pass args to custom event
			//AddConnectionEvent(EventArgs.Empty);
			Debug.WriteLine("[XboxKonnect] {0} DISCOVERED: {1}", xbox.IP, xbox.LastPing);
		}

		private void UpdateConnection(ConsoleConnection xbox)
		{
			try
			{
				lock (ConsoleController.ConnectedConsoles)
					ConsoleController.ConnectedConsoles[xbox.IP].LastPing = xbox.LastPing;
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
			}

			// TODO: Pass args to custom event
			//AddConnectionEvent(EventArgs.Empty);
			Debug.WriteLine("[XboxKonnect] {0} REPLIED: {1}", xbox.IP, xbox.LastPing);
		}

		private void RemoveConnection(ConsoleConnection xbox)
		{
			try {
				lock (ConsoleController.ConnectedConsoles)
					ConsoleController.ConnectedConsoles.Remove(xbox.IP);
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
			}

			// TODO: Pass args to custom event
			//AddConnectionEvent(EventArgs.Empty);
			Debug.WriteLine("[XboxKonnect] {0} REMOVED", xbox.IP);
		}

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

		private void ProcessResponse(Byte[] bytes)
		{
			var xbox = ConsoleConnection.NewXboxConnection();
			xbox.Response = Encoding.ASCII.GetString(bytes).Remove(0, 2);
			xbox.IP = localIpEndPoint.Address.ToString();
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

		private async void Listen()
		{
			while (Scanning)
			{
				try
				{
					Byte[] receiveBytes = udpClientScanner.Receive(ref localIpEndPoint);

					if (receiveBytes.Length > 0)
					{
						ProcessResponse(receiveBytes);
						receiveBytes = null;
					}
				}
				catch (Exception ex)
				{
					Debug.WriteLine(ex);
				}

				await Task.Delay(10);
			}
		}

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

		private async void Monitor()
		{
			while (Scanning)
			{
				//if(ConsoleController.ConnectedConsoles.Any<KeyValuePair<string, ConsoleConnection>>(x => DateTime.Now.Subtract(x.Value.LastPing).TotalSeconds > 3)) { }

				List<ConsoleConnection> consoles = new List<ConsoleConnection>(ConsoleController.ConnectedConsoles.Values);

				foreach (var xbox in consoles)
				{
					var timespan = DateTime.Now.Subtract(xbox.LastPing);
					if (timespan.TotalSeconds > 3) // TODO: Make timeout delay a property
						RemoveConnection(xbox);
				}

				await Task.Delay(Frequency);
			}
		}

		#endregion

		#region Public Methods

		public void Start()
		{
			if (Scanning)
				return;

			localIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
			udpClientScanner = new UdpClient(localIpEndPoint);

			myEndPoint = GetMyEndPoint();
			subnetRanges = GetSubnetRanges();

			Scanning = true;

			var listenTask = Task.Run(() =>
			{
				Listen();
			});
			var broadcastTask = Task.Run(() =>
			{
				Broadcast();
			});
			var monitorTask = Task.Run(() =>
			{
				Monitor();
			});

			Debug.WriteLine("[XboxKonnect] Scanning started ({0} frequency).", this.Frequency);

			//Listen();
			//Broadcast();
			//Monitor();
		}

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

		#endregion

	}
}

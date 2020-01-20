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
using System.ComponentModel;
using System.Net;

namespace SK.XboxKonnect
{
	/// <summary>
	/// An object that represents a console connection.
	/// </summary>
	public class Connection : INotifyPropertyChanged
	{
		static readonly object _lock = new object();

		private IPEndPoint _ip = null;
		private DateTime _firstPing = DateTime.Now;
		private DateTime _lastPing = DateTime.Now;
		private CPUKey _cpuKey = null;
		private ConsoleType _consoleType = ConsoleType.None;
		private ConnectionState _connectionState = ConnectionState.None;
		private ConnectionType _connectionType = ConnectionType.None;
		private string _name;

		#region Public Properties

		/// <summary>
		/// The connection's local network IP.
		/// </summary>
		public IPEndPoint IP {
			get => _ip;
			internal set {
				lock (_lock)
					_ip = value;
				NotifyPropertyChanged();
			}
		}

		/// <summary>
		/// Console name (default is Jtag / XeDevkit)
		/// </summary>
		public string Name {
			get => _name;
			internal set {
				lock (_lock)
					_name = value;
				NotifyPropertyChanged();
			}
		}

		/// <summary>
		/// Timestamp for when the connection was initially discovered.
		/// </summary>
		public DateTime FirstPing {
			get => _firstPing;
			internal set {
				lock (_lock)
					_firstPing = value;
				NotifyPropertyChanged();
			}
		}

		/// <summary>
		/// Timestamp of the last received response from the connection.
		/// </summary>
		public DateTime LastPing {
			get => _lastPing;
			internal set {
				lock (_lock)
					_lastPing = value;
				NotifyPropertyChanged();
			}
		}

		/// <summary>
		/// The network connection type for this connection (Bridged, LAN, WAN).
		/// </summary>
		public ConnectionType ConnectionType {
			get => _connectionType;
			set {
				lock (_lock)
					_connectionType = value;
				NotifyPropertyChanged();
			}
		}

		/// <summary>
		/// The current state of this connection (Online, Offline).
		/// </summary>
		public ConnectionState ConnectionState {
			get => _connectionState;
			internal set {
				lock (_lock)
					_connectionState = value;
				NotifyPropertyChanged();
			}
		}

		/// <summary>
		/// The type of device this connection represents (Jtag, Devkit).
		/// </summary>
		public ConsoleType ConsoleType {
			get => _consoleType;
			internal set {
				lock (_lock)
					_consoleType = value;
				NotifyPropertyChanged();
			}
		}

		/// <summary>
		/// The CPUKey for this connection.
		/// </summary>
		public CPUKey CPUKey {
			get => _cpuKey;
			set {
				lock (_lock)
					_cpuKey = value;
				NotifyPropertyChanged();
			}
		}

		#endregion

		/// <summary>
		/// Default constructor, initializes <see cref="Connection"/> class with default values.
		/// </summary>
		public Connection()
		{ }

		/// <summary>
		/// Public factory method, initializes <see cref="Connection"/> class using default values.
		/// </summary>
		/// <returns></returns>
		public static Connection NewXboxConnection()
		{
			return new Connection();
		}

		/// <summary>
		/// Equality comparer between two instances of the <see cref="Connection"/> class.
		/// If both objects have valid CPUKeys, equality comparison is based on <see cref="CPUKey"/>; 
		/// otherwise, equality is based on <see cref="IP"/>.
		/// </summary>
		/// <param name="obj">Object to compare.</param>
		/// <returns>Returns true if <see cref="IP"/> matches.</returns>
		public bool Equals(Connection obj)
		{
			if (obj.CPUKey is null || CPUKey is null)
				return obj.IP == IP;
			return obj.CPUKey == CPUKey;
		}

		/// <summary>
		/// Overrides base ToString() method.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return String.Format("{0} [{1}, {2}] - {3}", IP, ConnectionState, ConnectionType, Name);
		}

		#region PropertyChanged Events

		/// <summary>
		/// Event Handler for <seealso cref="INotifyPropertyChanged"/>.
		/// </summary>
		public event PropertyChangedEventHandler PropertyChanged;

		private void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion

	}
}

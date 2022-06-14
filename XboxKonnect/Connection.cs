/*
 * XboxKonnect - Xbox Auto Discovery API
 * 
 * Created: 10/24/2017
 * Author:  Daniel McClintock (alias: Stelio Kontos)
 * 
 * Copyright (c) 2017 Daniel McClintock
 * 
 */

using System.ComponentModel;
using System.Net;

namespace SK.XboxKonnect
{
	/// <summary>
	/// An object that represents a console connection.
	/// </summary>
	public class Connection : INotifyPropertyChanged
	{
		private static readonly object _lock = new object();

		private IPEndPoint? _ep;
		private CPUKey _cpuKey = CPUKey.Empty;
		private string _name = String.Empty;
		private ConsoleType _consoleType = ConsoleType.Unknown;
		private ConnectionType _connectionType = ConnectionType.Unknown;
		private ConnectionState _connectionState = ConnectionState.Unknown;
		private DateTime _firstPing = DateTime.Now;
		private DateTime _lastPing = DateTime.Now;

		#region Connection Properties

		/// <summary>
		/// The connection's <seealso cref="IPAddress"/> on the local network.
		/// </summary>
		public IPAddress IP => EndPoint?.Address ?? IPAddress.None;

		/// <summary>
		/// The connection's <seealso cref="IPEndPoint"/> on the local network.
		/// </summary>
		public IPEndPoint? EndPoint
		{
			get => _ep;
			internal set { lock (_lock) Set(ref _ep, value); }
		}

		/// <summary>
		/// The CPUKey for this connection. This is set by the consumer (typically through xbdm),
		/// so as not to add additional dependencies to this API.
		/// </summary>
		public CPUKey CPUKey
		{
			get => _cpuKey;
			set { lock (_lock) Set(ref _cpuKey, value); }
		}

		/// <summary>
		/// Console name (default is Jtag / XeDevkit).
		/// </summary>
		public string Name
		{
			get => _name;
			internal set { lock (_lock) Set(ref _name, value); }
		}

		/// <summary>
		/// The type of device this connection represents (Jtag, Devkit).
		/// </summary>
		public ConsoleType ConsoleType
		{
			get => _consoleType;
			set { lock (_lock) Set(ref _consoleType, value); }
		}

		/// <summary>
		/// The network'd type for this connection (Bridged, LAN, WAN).
		/// WAN is currently not being detected, and will return LAN by default.
		/// </summary>
		public ConnectionType ConnectionType
		{
			get => _connectionType;
			internal set { lock (_lock) Set(ref _connectionType, value); }
		}

		/// <summary>
		/// The last known state of this connection on the local network (Online, Offline).
		/// </summary>
		public ConnectionState ConnectionState
		{
			get => _connectionState;
			internal set { lock (_lock) Set(ref _connectionState, value); }
		}

		/// <summary>
		/// Timestamp for when the connection was initially discovered.
		/// </summary>
		public DateTime FirstPing
		{
			get => _firstPing;
			internal set { lock (_lock) Set(ref _firstPing, value); }
		}

		/// <summary>
		/// Timestamp of the last received response from the connection.
		/// </summary>
		public DateTime LastPing
		{
			get => _lastPing;
			internal set { lock (_lock) Set(ref _lastPing, value); }
		}

		#endregion

		/// <summary>
		/// Equality comparer between two instances of the <see cref="Connection"/> class.
		/// If both objects have valid CPUKeys, equality comparison is based on <see cref="CPUKey"/>;
		/// otherwise, equality is based on <see cref="IP"/>.
		/// </summary>
		/// <param name="other">Another <see cref="Connection"/> to compare for equality.</param>
		/// <returns>Returns true if <see cref="CPUKey"/> or <see cref="IP"/> matches.</returns>
		public bool Equals(Connection other)
		{
			if (other.CPUKey is null || CPUKey is null)
				return other.IP == IP;
			return other.CPUKey == CPUKey;
		}

		/// <summary>
		/// Overrides base ToString() method, returning a string in the form of: <c>Jtag (192.168.0.69)</c>
		/// </summary>
		/// <returns></returns>
		public override string ToString() => $"{Name} ({IP})";

		/// <summary>
		/// Event Handler for <seealso cref="INotifyPropertyChanged"/>.
		/// </summary>
		public event PropertyChangedEventHandler? PropertyChanged;

		/// <summary>
		/// Notifies the <see cref="PropertyChanged"/> event handler when a property value has changed.
		/// </summary>
		/// <param name="propertyName"></param>
		protected void OnPropertyChanged(string? propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		/// <summary>
		/// Sets the value of <paramref name="field"/> to <paramref name="value"/>, raising the <see cref="OnPropertyChanged"/> event notification if the old and new values are not equal.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="field">The field that stores this Property's value</param>
		/// <param name="value">The value to to save</param>
		/// <param name="propertyName">The name of the Property being changed, provided by the compiler</param>
		/// <returns>true if <paramref name="value"/> does not equal the current value in <paramref name="field"/>, otherwise false.</returns>
		protected bool Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
		{
			if (EqualityComparer<T>.Default.Equals(field, value))
				return false;
			field = value;
			OnPropertyChanged(propertyName);
			return true;
		}

	}
}

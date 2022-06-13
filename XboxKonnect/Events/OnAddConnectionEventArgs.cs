/*
 * XboxKonnect - Xbox Auto Discovery API
 * 
 * Created: 10/24/2017
 * Author:  Daniel McClintock (alias: Stelio Kontos)
 * 
 * Copyright (c) 2017 Daniel McClintock
 * 
 */

namespace SK.XboxKonnect
{
	/// <summary>
	/// Event args for events triggered when a connection is added to the connections dictionary.
	/// </summary>
	public class OnAddConnectionEventArgs : EventArgs
	{
		/// <summary>
		/// Provides access to an <see cref="Connection"/> instance.
		/// </summary>
		public Connection XboxConnection
		{
			get;
			private set;
		}

		/// <summary>
		/// Initializes a new instance of <see cref="OnAddConnectionEventArgs"/>.
		/// </summary>
		/// <param name="xboxConnection"></param>
		public OnAddConnectionEventArgs(Connection xboxConnection)
		{
			XboxConnection = xboxConnection;
		}
	}

}

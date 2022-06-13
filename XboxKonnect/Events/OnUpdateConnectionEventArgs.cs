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
	/// Event args for events triggered when a connection is updated in the connections dictionary
	/// </summary>
	public class OnUpdateConnectionEventArgs : EventArgs
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
		/// Initializes a new instance of <see cref="OnUpdateConnectionEventArgs"/>.
		/// </summary>
		/// <param name="xboxConnection"></param>
		public OnUpdateConnectionEventArgs(Connection xboxConnection)
		{
			XboxConnection = xboxConnection;
		}
	}
}

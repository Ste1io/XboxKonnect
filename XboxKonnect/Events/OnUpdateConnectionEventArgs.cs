/*
 * Console Auto Discovery and Status Scanner
 * Created by Stelio Kontos
 * Date: 10/24/2017
 */

using System;

namespace SK.XboxKonnect
{
	/// <summary>
	/// Event args for events triggered when a connection is updated in <see cref="ConsoleController"/>.
	/// </summary>
	public class OnUpdateConnectionEventArgs : EventArgs
	{
		/// <summary>
		/// Provides access to an <see cref="Connection"/> instance.
		/// </summary>
		public Connection XboxConnection {
			get;
			private set;
		}

		/// <summary>
		/// Initializes a new instance of <see cref="OnUpdateConnectionEventArgs"/>.
		/// </summary>
		/// <param name="xboxConnection"></param>
		public OnUpdateConnectionEventArgs(Connection xboxConnection)
		{
			this.XboxConnection = xboxConnection;
		}
	}

}

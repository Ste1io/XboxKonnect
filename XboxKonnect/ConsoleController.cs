/*
 * Console Auto Discovery and Status Scanner
 * Created by Stelio Kontos
 * Date: 10/24/2017
 */
using System;
using System.Collections.Generic;

namespace XboxKonnect
{
	public class ConsoleController
	{
		public Dictionary<string, ConsoleConnection> ConnectedConsoles {
			get;
			set;
		}

		/// <summary>
		/// Tracks all console connections, disconnections, and basic connection details,
		/// and provides access to the list of active connections.
		/// </summary>
		public ConsoleController()
		{
			ConnectedConsoles = new Dictionary<string, ConsoleConnection>();
		}
	}
}

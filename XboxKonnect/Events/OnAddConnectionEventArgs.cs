﻿/*
 * Console Auto Discovery and Status Scanner
 * Created by Stelio Kontos
 * Date: 10/24/2017
 */

using System;

namespace XboxKonnect
{
	public class OnAddConnectionEventArgs : EventArgs
	{
		public ConsoleConnection XboxConnection {
			get;
			private set;
		}

		public OnAddConnectionEventArgs(ConsoleConnection xboxConnection)
		{
			this.XboxConnection = xboxConnection;
		}
	}

}
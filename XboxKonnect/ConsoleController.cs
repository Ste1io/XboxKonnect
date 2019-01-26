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
		public Dictionary<string, ConsoleConnection> ConnectedConsoles { get; set; } = new Dictionary<string, ConsoleConnection>();

		public ConsoleController()
		{

		}
	}
}

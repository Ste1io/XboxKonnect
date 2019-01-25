using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

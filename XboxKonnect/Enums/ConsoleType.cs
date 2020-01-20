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

namespace SK.XboxKonnect
{
	/// <summary>
	/// Enum for identifying a console type.
	/// </summary>
	public enum ConsoleType
	{
		/// <summary>
		/// Unknown console type.
		/// </summary>
		None = 0,

		/// <summary>
		/// Jtag console.
		/// </summary>
		Jtag = 1,

		/// <summary>
		/// Devkit console.
		/// </summary>
		DevKit = 2,
	}

}

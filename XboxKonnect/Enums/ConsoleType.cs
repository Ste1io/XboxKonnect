/*
 * Console Auto Discovery and Status Scanner
 * Created by Stelio Kontos
 * Date: 10/24/2017
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

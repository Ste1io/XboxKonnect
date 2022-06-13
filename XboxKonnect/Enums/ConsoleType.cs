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

using System;

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
		Unknown = 0,

		/// <summary>
		/// This enum member is obsolete. Use <see cref="Unknown"/> instead.
		/// </summary>
		[Obsolete("This enum member is obsolete. Use ConsoleType.Unknown instead.", false)]
		None = Unknown,

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

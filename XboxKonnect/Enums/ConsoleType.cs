/*
 * XboxKonnect - Xbox Auto Discovery API
 * 
 * Created: 10/24/2017
 * Author:  Daniel McClintock (alias: Stelio Kontos)
 * 
 * Copyright (c) 2017 Daniel McClintock
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

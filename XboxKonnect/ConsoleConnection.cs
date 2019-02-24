/*
 * Console Auto Discovery and Status Scanner
 * Created by Stelio Kontos
 * Date: 10/24/2017
 */
using System;

namespace XboxKonnect
{
	public class ConsoleConnection
	{
		internal string Response {
			get;
			set;
		}

		public string IP {
			get;
			set;
		}

		public DateTime FirstPing {
			get;
			set;
		}

		public DateTime LastPing {
			get;
			set;
		}

		public ConnectionFlags ConnectionState {
			get;
			set;
		}

		public ConsoleConnection()
		{
			FirstPing = DateTime.Now;
		}

		/// <summary>
		/// Public factory method, initialized with default (empty) values.
		/// </summary>
		/// <returns></returns>
		public static ConsoleConnection NewXboxConnection()
		{
			return new ConsoleConnection();
		}

		#region Public Methods

		/// <summary>
		/// Equality comparer between two instances of the <see cref="ConsoleConnection"/> class based on CPUKey.
		/// </summary>
		/// <param name="obj">Object to compare.</param>
		/// <param name="y">Object being compared against.</param>
		/// <returns></returns>
		public bool Equals(ConsoleConnection obj)
		{
			return obj.IP == IP;
		}

		/// <summary>
		/// Overrides <see cref="ToString"/>.
		/// </summary>
		/// <returns>IP - CPUKey</returns>
		public override string ToString()
		{
			return String.Format("{0} [{1}] - {2}", this.IP, this.ConnectionState, this.Response);
		}

		#endregion
	}
}

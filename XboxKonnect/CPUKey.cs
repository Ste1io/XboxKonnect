using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SK
{
	/// <summary>
	/// Represents a 32-character Xbox CPUKey and provides convenience methods for parsing, validating, and converting.
	/// See <see cref="CPUKeyUtils"/> class for static helper methods.
	/// </summary>
	public class CPUKey : IEquatable<CPUKey>
	{
		/// <summary>
		/// The CPUKey as a byte[] array.
		/// </summary>
		public byte[] Bytes { get; set; } = null;

		#region Object Construction

		/// <summary>
		/// Public constructor for <see cref="CPUKey"/> class initialized from a preexisting CPUKey object.
		/// </summary>
		/// <param name="cpukey">A valid <see cref="CPUKey"/> object.</param>
		public CPUKey(CPUKey cpukey) : this()
		{
			Bytes = cpukey.Bytes;
		}

		/// <summary>
		/// Default constructor for <see cref="CPUKey"/> class.
		/// </summary>
		public CPUKey()
		{ }

		public CPUKey ShallowCopy()
		{
			return (CPUKey)this.MemberwiseClone();
		}

		public CPUKey DeepCopy()
		{
			CPUKey other = (CPUKey)this.MemberwiseClone();
			other.Bytes = this.Bytes;
			return other;
		}

		#endregion

		#region Parsing Methods

		public static CPUKey Parse(byte[] value)
		{
			CPUKey cpuKey = new CPUKey();
			cpuKey.TryUpdate(value);
			return cpuKey;
		}

		public static CPUKey Parse(string value)
		{
			return Parse(CPUKeyUtils.HexStringToBytes(value));
		}

		public static bool TryParse(byte[] value, out CPUKey cpuKey)
		{
			cpuKey = new CPUKey();
			return cpuKey.TryUpdate(value);
		}

		public static bool TryParse(string value, out CPUKey cpuKey)
		{
			return TryParse(CPUKeyUtils.HexStringToBytes(value), out cpuKey);
		}

		#endregion

		public bool TryUpdate(byte[] value)
		{
			if (value.Length == 0x10)
			{
				Bytes = value;
				return true;
			}
			return false;
		}

		public bool TryUpdate(string value)
		{
			return TryUpdate(CPUKeyUtils.HexStringToBytes(value));
		}
		
		/// <summary>
		/// Sanity check to verify that a <see cref="CPUKey"/> object is valid.
		/// </summary>
		/// <param name="cpukey">The <see cref="CPUKey"/> object to validate.</param>
		/// <returns>Returns <c>true</c> if the object is a valid CPUKey, <c>false</c> otherwise.</returns>
		public bool IsValid()
		{
			return CPUKeyUtils.IsValid(this);
		}

		/// <summary>
		/// Converts a CPUKey into a byte[] array.
		/// </summary>
		/// <returns>A byte[] array containing the CPUKey.</returns>
		public byte[] ToByteArray()
		{
			return Bytes;
		}

		/// <summary>
		/// Overrides base ToString() method.
		/// </summary>
		/// <returns>A UTF-16 encoded string representing the CPUKey.</returns>
		public override string ToString()
		{
			return ToByteArray() is null
				? String.Empty
				: CPUKeyUtils.BytesToHexString(ToByteArray());
		}

		public override int GetHashCode()
		{
			return 1182642244 + EqualityComparer<byte[]>.Default.GetHashCode(ToByteArray());
		}

		public override bool Equals(object obj)
		{
			if (obj is CPUKey)
				return Equals((CPUKey)obj);
			else if (obj is byte[] || obj is string)
				return Equals(obj);
			else
				return false;
		}

		public bool Equals(CPUKey other) => EqualityComparer<byte[]>.Default.Equals(ToByteArray(), other.ToByteArray());
		public bool Equals(byte[] other) => EqualityComparer<byte[]>.Default.Equals(ToByteArray(), other);
		public bool Equals(string other) => EqualityComparer<string>.Default.Equals(ToString(), other);

		public static bool operator ==(CPUKey key1, CPUKey key2) => key1.Equals(key2);
		public static bool operator !=(CPUKey key1, CPUKey key2) => !(key1 == key2);
		public static bool operator ==(CPUKey key1, byte[] key2) => key1.Equals(key2);
		public static bool operator !=(CPUKey key1, byte[] key2) => !(key1 == key2);
		public static bool operator ==(CPUKey key1, string key2) => key1.Equals(key2);
		public static bool operator !=(CPUKey key1, string key2) => !(key1 == key2);
	}

	/// <summary>
	/// Static helper methods for <see cref="CPUKey"/> class.
	/// </summary>
	public static class CPUKeyUtils
	{
		#region CPUKey Validation Methods

		/// <summary>
		/// Sanity check to check verify that a <see cref="CPUKey"/> object is valid.
		/// </summary>
		/// <param name="cpukey">The <see cref="CPUKey"/> object to validate.</param>
		/// <returns>Returns <c>true</c> if the object is a valid CPUKey, <c>false</c> otherwise.</returns>
		public static bool IsValid(this CPUKey cpukey)
		{
			return cpukey is null
				|| cpukey.ToByteArray() is null
				|| cpukey.ToByteArray().Length != 0x10
				? false : true;
		}

		/// <summary>
		/// Sanity check to check verify that a byte[] array containing a CPUKey is valid.
		/// </summary>
		/// <param name="value">The byte[] array representing a CPUKey.</param>
		/// <returns>Returns <c>true</c> if the byte[] array is a valid CPUKey, <c>false</c> otherwise.</returns>
		public static bool IsValid(this byte[] value)
		{
			if (value is null || value.Length != 0x10)
				return false;

			return true;
		}

		/// <summary>
		/// Sanity check to check verify that a UTF-16 encoded string representing a CPUKey is valid.
		/// </summary>
		/// <param name="value">A UTF-16 encoded string representing a CPUKey in hexidecimal format.</param>
		/// <returns>Returns <c>true</c> if the string represents a valid CPUKey, <c>false</c> otherwise</returns>
		public static bool IsValid(this string value)
		{
			if (String.IsNullOrEmpty(value) || value.Length != 32)
				return false;

			if (!Regex.IsMatch(value, "[a-fA-F0-9]{32}"))
				return false;

			return true;
		}

		#endregion

		#region Primitive Conversion Methods

		/// <summary>
		/// Compare two <c>byte[]</c> arrays for equality.
		/// </summary>
		/// <param name="array">A byte[] aray to be compared.</param>
		/// <param name="targetArray">The target byte[] array to compare <paramref name="array"/> with.</param>
		/// <returns></returns>
		public static bool CompareBytes(byte[] array, byte[] targetArray)
		{
			if (array.Length != targetArray.Length)
				return false;

			for (int i = 0; i < array.Length; i++)
			{
				if (array[i] != targetArray[i])
					return false;
			}

			return true;
		}

		/// <summary>
		/// Converts a string of hexidecimal characters represented in UTF-16 encoding to a byte[] array.
		/// </summary>
		/// <param name="hexString">A string representation of hexidecimal byte values.</param>
		/// <returns></returns>
		public static byte[] HexStringToBytes(this string hexString)
		{
			byte[] returnArray = new byte[hexString.Length / 2];

			for (int i = 0; i < hexString.Length; i += 2)
			{
				try
				{
					returnArray[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);
				}
				catch
				{
					returnArray[i / 2] = 0;
				}
			}

			return returnArray;
		}

		/// <summary>
		/// Converts a byte[] array into a string representation of hexidecimal values in UTF-16 encoding.
		/// </summary>
		/// <param name="buffer">A byte[] array.</param>
		/// <returns></returns>
		public static string BytesToHexString(this byte[] buffer)
		{
			string str = String.Empty;

			foreach (var b in buffer)
				str = str + b.ToString("X2");

			return str;
		}

		#endregion

	}
}

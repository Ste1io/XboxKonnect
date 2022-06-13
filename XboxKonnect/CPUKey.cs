/*
 * CPUKey class
 * 
 * Created: 01/20/2020
 * Author:  Daniel McClintock (alias: Stelio Kontos)
 * 
 * Copyright (c) 2020 Daniel McClintock
 * 
 */

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Numerics;

namespace SK
{
	/// <summary>
	/// Represents a 32-character Xbox CPUKey and provides convenience methods for parsing, validating, and converting.
	/// See <see cref="CPUKeyUtils"/> class for static helper methods.
	/// </summary>
	public class CPUKey : IEquatable<CPUKey>
	{
		private Memory<byte> data = Memory<byte>.Empty;

		internal static int kValidByteLen = 0x10;
		internal static ulong kECDMask = 0xFFFFFFFFFF030000;

		/// <summary>
		/// Returns an empty/invalid CPUKey object.
		/// </summary>
		public static readonly CPUKey None = new();

		/// <summary>
		/// Initializes a new CPUKey instance that is empty (and invalid).
		/// </summary>
		public CPUKey() { }

		/// <summary>
		/// Initializes a new CPUKey instance, copying the underlying data from <paramref name="other"/>.
		/// </summary>
		/// <param name="other">A CPUKey instance</param>
		/// <exception cref="ArgumentNullException"><paramref name="other"/> is null</exception>
		public CPUKey(CPUKey other)
		{
			if (other is null)
				throw new ArgumentNullException(nameof(other));

			data = new byte[kValidByteLen];
			other.data.CopyTo(data);
		}

		/// <summary>
		/// Initializes a new CPUKey instance from an <seealso cref="Array"/>.
		/// </summary>
		/// <param name="value">A <seealso cref="ReadOnlySpan{T}"/> representation of an <seealso cref="Array"/></param>
		/// <exception cref="ArgumentException"><paramref name="value"/> length is not 0x10 (16)</exception>
		public CPUKey(ReadOnlySpan<byte> value)
		{
			if (value.Length != kValidByteLen)
				throw new ArgumentException("Source length is not equal to the length of a CPUKey (0x10 bytes).", nameof(value));

			data = new byte[kValidByteLen];
			value.CopyTo(data.Span);
		}

		/// <summary>
		/// Creates a new CPUKey instance from <paramref name="value"/>.
		/// </summary>
		/// <param name="value">A <seealso cref="ReadOnlySpan{T}"/> representation of a CPUKey <seealso cref="Array"/></param>
		/// <returns>A new instance of CPUKey</returns>
		public static CPUKey? Parse(ReadOnlySpan<byte> value)
		{
			if (value.Length != kValidByteLen)
				return default;
			return new CPUKey(value);
		}

		/// <summary>
		/// Creates a new CPUKey instance from <paramref name="value"/>.
		/// </summary>
		/// <param name="value">A CPUKey <seealso cref="String"/></param>
		/// <returns>A new instance of CPUKey</returns>
		public static CPUKey? Parse(string? value)
		{
			if (value is null || !CPUKeyUtils.IsValid(value))
				return default;
			return new CPUKey(CPUKeyUtils.HexStringToBytes(value));
		}

		/// <summary>
		/// Verify <paramref name="value"/> is a valid CPUKey, and initialize a new CPUKey instance at <paramref name="cpukey"/>
		/// </summary>
		/// <param name="value">The <seealso cref="Array"/> to validate and parse</param>
		/// <param name="cpukey">A new CPUKey instance</param>
		/// <returns>true if <paramref name="value"/> is a valid CPUKey, otherwise false</returns>
		public static bool TryParse([NotNullWhen(true)] ReadOnlySpan<byte> value, [NotNullWhen(true)] out CPUKey? cpukey)
		{
			cpukey = Parse(value);
			return cpukey is not null;
		}

		/// <summary>
		/// Verify <paramref name="value"/> is a valid CPUKey, and initialize a new CPUKey instance at <paramref name="cpukey"/>
		/// </summary>
		/// <param name="value">The <seealso cref="String"/> to validate and parse</param>
		/// <param name="cpukey">A new CPUKey instance</param>
		/// <returns>true if <paramref name="value"/> is a valid CPUKey, otherwise false</returns>
		public static bool TryParse([NotNullWhen(true)] string? value, [NotNullWhen(true)] out CPUKey? cpukey)
		{
			cpukey = Parse(value);
			return cpukey is not null;
		}

		/// <summary>
		/// Sanity check to verify that a CPUKey object is valid.
		/// </summary>
		/// <returns>Returns true if the object is a valid CPUKey, otherwise false</returns>
		public bool IsValid()
		{
			if (data.IsEmpty)
				return false;

			Span<byte> bytes = stackalloc byte[kValidByteLen];
			data.Span.CopyTo(bytes);
			bytes[..sizeof(ulong)].Reverse();
			bytes[sizeof(ulong)..].Reverse();

			Span<ulong> parts = MemoryMarshal.Cast<byte, ulong>(bytes);
			var hammingWeight = BitOperations.PopCount(parts[0]) + BitOperations.PopCount(parts[1] & kECDMask);

			return hammingWeight == 53;
		}

		/// <summary>
		/// Returns a <seealso cref="ReadOnlySpan{T}"/> from the current CPUKey instance
		/// </summary>
		/// <returns>A <seealso cref="ReadOnlySpan{T}"/> created from the CPUKey object</returns>
		public ReadOnlySpan<byte> ToSpan() => data.Span;

		/// <summary>
		/// Copies the CPUKey into a new <seealso cref="Array"/>.
		/// </summary>
		/// <returns>An <seealso cref="Array"/> containing the CPUKey</returns>
		public byte[] ToArray() => IsValid() ? Array.Empty<byte>() : data.ToArray();

		/// <summary>
		/// Returns the CPUKey as a <seealso cref="String"/>.
		/// </summary>
		/// <returns>A UTF-16 encoded <seealso cref="String"/> representing the CPUKey</returns>
		public override string ToString() => IsValid() ? data.ToArray().BytesToHexString() : String.Empty;

		/// <inheritdoc/>
		public override int GetHashCode() => data.GetHashCode();

		/// <inheritdoc/>
		public override bool Equals([NotNullWhen(true)] object? obj)
		{
			if (obj is null)
				return false;

			if (obj is CPUKey cpukey)
				return Equals(cpukey);

			return false;
		}

		/// <inheritdoc/>
		public bool Equals([NotNullWhen(true)] CPUKey? other)
		{
			if (other is null)
				return false;

			return other.data.Span.SequenceEqual(data.Span);
		}

		/// <summary>
		/// Indicates whether the current object is equal to <paramref name="value"/>.
		/// </summary>
		/// <param name="value">The comparand as a <seealso cref="ReadOnlySpan{T}"/></param>
		/// <returns></returns>
		public bool Equals(ReadOnlySpan<byte> value) => value.SequenceEqual(data.Span);

		/// <summary>
		/// Indicates whether the current object is equal to <paramref name="value"/>.
		/// </summary>
		/// <param name="value">The comparand as a <seealso cref="String"/></param>
		/// <returns></returns>
		public bool Equals([NotNullWhen(true)] string? value) => String.Equals(data.ToArray().BytesToHexString(), value?.Trim(), StringComparison.OrdinalIgnoreCase);

		/// <inheritdoc/>
		public static bool operator ==(CPUKey lhs, CPUKey rhs) => lhs.Equals(rhs);
		/// <inheritdoc/>
		public static bool operator !=(CPUKey lhs, CPUKey rhs) => !lhs.Equals(rhs);
		/// <inheritdoc/>
		public static bool operator ==(CPUKey lhs, ReadOnlySpan<byte> rhs) => lhs.Equals(rhs);
		/// <inheritdoc/>
		public static bool operator !=(CPUKey lhs, ReadOnlySpan<byte> rhs) => !lhs.Equals(rhs);
		/// <inheritdoc/>
		public static bool operator ==(CPUKey lhs, string rhs) => lhs.Equals(rhs);
		/// <inheritdoc/>
		public static bool operator !=(CPUKey lhs, string rhs) => !lhs.Equals(rhs);
	}

	/// <summary>
	/// Extention methods for CPUKey.
	/// </summary>
	public static class CPUKeyUtils
	{
		/// <summary>
		/// Sanity check to check verify that a byte[] array containing a CPUKey is valid.
		/// </summary>
		/// <param name="value">The byte[] array representing a CPUKey.</param>
		/// <returns>Returns true if the byte[] array is a valid CPUKey, false otherwise.</returns>
		public static bool IsValid(ReadOnlySpan<byte> value) => value.Length == CPUKey.kValidByteLen;

		/// <summary>
		/// Sanity check to check verify that a UTF-16 encoded string representing a CPUKey is valid.
		/// </summary>
		/// <param name="value">A UTF-16 encoded string representing a CPUKey in hexidecimal format.</param>
		/// <returns>Returns true if the string represents a valid CPUKey, false otherwise</returns>
		public static bool IsValid(ReadOnlySpan<char> value)
		{
			var span = value.Trim();

			if (span.Length != 0x20)
				return false;

			//if (!Regex.IsMatch(span.ToString(), "[a-fA-F0-9]{32}"))
			//	return false;

			foreach (var ch in span)
			{
				if (!Char.IsAscii(ch) || !Char.IsLetterOrDigit(ch))
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
	}
}

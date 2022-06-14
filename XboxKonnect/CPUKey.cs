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
using System.Numerics;
using System.Runtime.InteropServices;

namespace SK
{
	/// <summary>
	/// Encapsulates a 32-character Xbox CPUKey, and provides parsing, validation, and conversion methods.
	/// </summary>
	public class CPUKey : IEquatable<CPUKey>
	{
		private readonly Memory<byte> data = Memory<byte>.Empty;

		internal static int kValidByteLen = 0x10;
		internal static int kValidCharLen = 0x20;
		internal static ulong kECDMask = 0xFFFFFFFFFF030000;

		/// <summary>
		/// Returns an empty/invalid CPUKey object.
		/// </summary>
		public static readonly CPUKey Empty = new();

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
		public static CPUKey? Parse(ReadOnlySpan<char> value)
		{
			if (!ValidateString(value))
				return default;
			return new CPUKey(Convert.FromHexString(value));
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
			{
				System.Diagnostics.Trace.WriteLine($"Invalid CPUKey: No data");
				return false;
			}

			if (!ValidateHammingWeight())
			{
				System.Diagnostics.Trace.WriteLine($"Invalid CPUKey: Failed hamming weight check");
				return false;
			}

			if (!ValidateECD())
			{
				System.Diagnostics.Trace.WriteLine($"Invalid CPUKey: Failed ECD check");
				return false;
			}

			return true;
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
		public byte[] ToArray() => data.ToArray();

		/// <summary>
		/// Returns the CPUKey as a <seealso cref="String"/>.
		/// </summary>
		/// <returns>A UTF-16 encoded <seealso cref="String"/> representing the CPUKey</returns>
		public override string ToString() => Convert.ToHexString(data.Span);

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
		/// <returns>true if the CPUKey instance is equal to <paramref name="value"/>, otherwise false</returns>
		public bool Equals(ReadOnlySpan<byte> value) => value.SequenceEqual(data.Span);

		/// <summary>
		/// Indicates whether the current object is equal to <paramref name="value"/>.
		/// </summary>
		/// <param name="value">The comparand as a <seealso cref="String"/></param>
		/// <returns>true if the CPUKey instance is equal to <paramref name="value"/>, otherwise false</returns>
		public bool Equals([NotNullWhen(true)] string? value) => String.Equals(Convert.ToHexString(data.Span), value?.Trim(), StringComparison.OrdinalIgnoreCase);

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

		internal bool ValidateHammingWeight()
		{
			Span<byte> span = stackalloc byte[kValidByteLen];
			data.Span.CopyTo(span);
			span[..sizeof(ulong)].Reverse();
			span[sizeof(ulong)..].Reverse();

			Span<ulong> parts = MemoryMarshal.Cast<byte, ulong>(span);
			var hammingWeight = BitOperations.PopCount(parts[0]) + BitOperations.PopCount(parts[1] & kECDMask);

			return hammingWeight == 53;
		}

		internal bool ValidateECD()
		{
			Span<byte> span = stackalloc byte[kValidByteLen];
			data.Span.CopyTo(span);
			ComputeECD(span);
			return span.SequenceEqual(data.Span);
		}

		private static void ComputeECD(Span<byte> cpukey)
		{
			//uint mask  = 000003FF; // 0xFFFFFFFFFF030000 reversed

			// accumulator vars
			uint acc1 = 0;
			uint acc2 = 0;

			for (var i = 0; i < 128; i++, acc1 >>= 1) // foreach (bit in cpukey)
			{
				var bTmp = cpukey[i >> 3];
				uint dwTmp = (uint)((bTmp >> (i & 7)) & 1);

				if (i < 0x6A) // if (i < 106) // (hammingweight * 2)
				{
					acc1 = dwTmp ^ acc1;
					if ((acc1 & 1) > 0)
						acc1 ^= 0x360325;
					acc2 = dwTmp ^ acc2;
				}
				else if (i < 0x7F) // else if (i != lastbit) // (127)
				{
					if (dwTmp != (acc1 & 1))
						cpukey[(i >> 3)] = (byte)((1 << (i & 7)) ^ (bTmp & 0xFF));
					acc2 = (acc1 & 1) ^ acc2;
				}
				else if (dwTmp != acc2)
				{
					cpukey[0xF] = (byte)((0x80 ^ bTmp) & 0xFF); // ((128 ^ bTmp) & 0xFF)
				}
			}
		}

		internal static bool ValidateString(ReadOnlySpan<char> value)
		{
			if (value.Length != kValidCharLen)
				return false;

			for (int i = 0; i < kValidCharLen; i++)
			{
				if (!IsHexDigit(value[i]))
					return false;
			}

			return true;
		}

		internal static bool IsHexDigit(char value) => value is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');
	}
}

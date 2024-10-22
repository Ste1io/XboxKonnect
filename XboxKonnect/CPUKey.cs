/*
 * CPUKey class - v3.2.0
 * Created: 01/20/2020
 * Author:  Daniel McClintock (alias: Stelio Kontos)
 *
 * Copyright (c) 2020 Daniel McClintock
 */

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

[assembly: InternalsVisibleTo("XboxKonnect.Tests")]

namespace SK;

/// <summary>
/// Encapsulates an immutable 16-byte (32-character hexadecimal) Xbox 360 CPUKey, and provides parsing, validation, conversion, and utility
/// methods. It is designed to be highly performant with minimal memory footprint. Optimized hardware intrinsics are used when possible, and
/// efficient use of stack-allocated buffers for scratch storage and direct memory access to the underlying data buffer avoid unnecessary
/// allocations and copies.
/// </summary>
/// <remarks>
/// CPUKey instances can be used in collections that require fast look-up, comparison, sorting, and equality checks,
/// such as <see cref="List{CPUKey}"/>, <see cref="HashSet{CPUKey}"/>, <see cref="Dictionary{CPUKey, TValue}"/>, <see
/// cref="SortedSet{CPUKey}"/>, and <see cref="SortedDictionary{CPUKey, TValue}"/>.
/// </remarks>
public sealed class CPUKey : IEquatable<CPUKey>, IComparable<CPUKey>
{
	private static readonly int ValidByteLen = 0x10;
	private static readonly int ValidCharLen = 0x20;
	private static readonly int ValidHammingWeight = 0x35;

	private readonly ReadOnlyMemory<byte> data = ReadOnlyMemory<byte>.Empty;

	/// <summary>
	/// Returns an empty CPUKey object.
	/// </summary>
	public static readonly CPUKey Empty = new();

	/// <summary>
	/// Initializes a new CPUKey instance equivalent to <see cref="Empty"/>.
	/// </summary>
	public CPUKey() { }

	/// <summary>
	/// Initializes a new CPUKey instance, copying the underlying data from <paramref name="other"/>.
	/// </summary>
	/// <param name="other">A CPUKey instance.</param>
	/// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
	public CPUKey(CPUKey? other)
	{
		if (other is null)
			throw new ArgumentNullException(nameof(other));
		data = new ReadOnlyMemory<byte>(other.data.ToArray());
	}

	/// <summary>
	/// Initializes a new CPUKey instance from a byte <see cref="Array"/>.
	/// </summary>
	/// <param name="value">The <see cref="ReadOnlySpan{T}"/> representation of a byte sequence to parse and validate.</param>
	/// <exception cref="ArgumentException"><paramref name="value"/> is empty.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not 16 bytes long.</exception>
	/// <exception cref="FormatException"><paramref name="value"/> is all zeroes.</exception>
	/// <exception cref="CPUKeyHammingWeightException"><paramref name="value"/> has an invalid Hamming weight.</exception>
	/// <exception cref="CPUKeyECDException"><paramref name="value"/> failed ECD validation.</exception>
	public CPUKey(ReadOnlySpan<byte> value) : this(SanitizeInput(value)) => ValidateData(data.Span);

	/// <summary>
	/// Initializes a new CPUKey instance from a <see cref="String"/>.
	/// </summary>
	/// <param name="value">The <see cref="ReadOnlySpan{T}"/> representation of a hexadecimal string to parse and validate.</param>
	/// <exception cref="ArgumentException"><paramref name="value"/> is empty.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not 32 hexadecimal characters long.</exception>
	/// <exception cref="FormatException"><paramref name="value"/> is all zeroes or contains a non-hex character.</exception>
	/// <exception cref="CPUKeyHammingWeightException"><paramref name="value"/> has an invalid Hamming weight.</exception>
	/// <exception cref="CPUKeyECDException"><paramref name="value"/> failed ECD validation.</exception>
	public CPUKey(ReadOnlySpan<char> value) : this(SanitizeInput(value)) => ValidateData(data.Span);

	private CPUKey(byte[] value) => data = new ReadOnlyMemory<byte>(value);

	/// <summary>
	/// Generates a cryptographically random CPUKey instance with valid Hamming weight and ECD.
	/// </summary>
	/// <returns>A new CPUKey object.</returns>
	public static CPUKey CreateRandom()
	{
		Span<byte> span = stackalloc byte[ValidByteLen];
		do { RandomNumberGenerator.Fill(span); } while (!VerifyHammingWeight(span));
		ComputeECD(span);
		return new CPUKey(span);
	}

	/// <summary>
	/// Creates a new CPUKey instance from the given byte <see cref="Array"/>.
	/// </summary>
	/// <param name="value">The <see cref="ReadOnlySpan{T}"/> representation of the byte sequence to parse and validate.</param>
	/// <returns>A new CPUKey instance if parsing and validation succeed.</returns>
	/// <exception cref="ArgumentException"><paramref name="value"/> is empty.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not 16 bytes long.</exception>
	/// <exception cref="FormatException"><paramref name="value"/> is all zeroes.</exception>
	/// <exception cref="CPUKeyHammingWeightException"><paramref name="value"/> has an invalid Hamming weight.</exception>
	/// <exception cref="CPUKeyECDException"><paramref name="value"/> failed ECD validation.</exception>
	public static CPUKey Parse(ReadOnlySpan<byte> value) => ParseInternal(SanitizeInput(value));

	/// <summary>
	/// Creates a new CPUKey instance from the given hexadecimal <see cref="String"/>.
	/// </summary>
	/// <param name="value">The <see cref="ReadOnlySpan{T}"/> representation of the hexadecimal string to parse and validate.</param>
	/// <returns>A new CPUKey instance if parsing and validation succeed.</returns>
	/// <exception cref="ArgumentException"><paramref name="value"/> is empty.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not 32 hexadecimal characters long.</exception>
	/// <exception cref="FormatException"><paramref name="value"/> is all zeroes or contains a non-hex character.</exception>
	/// <exception cref="CPUKeyHammingWeightException"><paramref name="value"/> has an invalid Hamming weight.</exception>
	/// <exception cref="CPUKeyECDException"><paramref name="value"/> failed ECD validation.</exception>
	public static CPUKey Parse(ReadOnlySpan<char> value) => ParseInternal(SanitizeInput(value));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static CPUKey ParseInternal(ReadOnlySpan<byte> sanitizedValue)
	{
		ValidateData(sanitizedValue);
		return new CPUKey(sanitizedValue);
	}

	/// <summary>
	/// Parses and validates the given byte <see cref="Array"/>, initializing a new CPUKey instance at <paramref name="cpukey"/>. If the
	/// input is malformed, <paramref name="cpukey"/> is passed uninitialized. If the input is well-formed but invalid, <paramref
	/// name="cpukey"/> is set to <see cref="Empty"/>.
	/// </summary>
	/// <param name="value">The <see cref="ReadOnlySpan{T}"/> representation of the byte sequence to parse and validate.</param>
	/// <param name="cpukey">
	/// When this method returns, contains the CPUKey value equivalent of the byte sequence contained in <paramref name="value"/> if the
	/// conversion succeeded. The possible values are:
	/// <br/>- Uninitialized: If the input is malformed.
	/// <br/>- <see cref="Empty"/>: If the input is well-formed but invalid.
	/// <br/>- A valid CPUKey instance: If the conversion succeeded.
	/// </param>
	/// <returns>true if <paramref name="value"/> represents a valid CPUKey; otherwise, false.</returns>
	public static bool TryParse(ReadOnlySpan<byte> value, [NotNullWhen(true)] out CPUKey? cpukey) => TryParseInternal(SanitizeInputSafe(value), out cpukey);

	/// <summary>
	/// Parses and validates the given hexadecimal <see cref="String"/>, initializing a new CPUKey instance at <paramref name="cpukey"/> if
	/// successful; otherwise, setting it to null or <see cref="Empty"/> to signify a malformed or invalid CPUKey.
	/// </summary>
	/// <param name="value">The <see cref="ReadOnlySpan{T}"/> representation of the hexadecimal string to parse and validate.</param>
	/// <param name="cpukey">
	/// When this method returns, contains the CPUKey value equivalent of the hexadecimal string contained in <paramref name="value"/> if
	/// the conversion succeeded. The possible values are:
	/// <br/>- Uninitialized: If the input is malformed.
	/// <br/>- <see cref="Empty"/>: If the input is well-formed but invalid.
	/// <br/>- A valid CPUKey instance: If the conversion succeeded.
	/// </param>
	/// <returns>true if <paramref name="value"/> represents a valid CPUKey; otherwise, false.</returns>
	public static bool TryParse(ReadOnlySpan<char> value, [NotNullWhen(true)] out CPUKey? cpukey) => TryParseInternal(SanitizeInputSafe(value), out cpukey);

	private static bool TryParseInternal(ReadOnlySpan<byte> sanitizedValue, [NotNullWhen(true)] out CPUKey? cpukey)
	{
		cpukey = sanitizedValue switch
		{
			_ when sanitizedValue.IsEmpty => default, // malformed, pass uninitialized
			_ when !ValidateDataSafe(sanitizedValue) => Empty, // invalid, pass Empty
			_ => new CPUKey(sanitizedValue) // well-formed and valid
		};

		return cpukey?.IsValid() ?? false;
	}

	/// <summary>
	/// Determines whether the current CPUKey instance represents a valid CPUKey.
	/// </summary>
	/// <returns>true if the current instance is not equal to <see cref="Empty"/>; otherwise, false.</returns>
	public bool IsValid() => !data.IsEmpty;

	/// <summary>
	/// Returns a <see cref="ReadOnlySpan{T}"/> from the current CPUKey instance.
	/// </summary>
	/// <returns>A <see cref="ReadOnlySpan{T}"/> created from the CPUKey object.</returns>
	public ReadOnlySpan<byte> AsSpan() => data.Span;

	/// <summary>
	/// Copies the CPUKey into a new byte array.
	/// </summary>
	/// <returns>An <see cref="Array"/> containing the CPUKey.</returns>
	public byte[] ToArray() => data.ToArray();

	#region Object Overrides

	/// <summary>
	/// Determines whether the specified object is equal to the current CPUKey instance.
	/// </summary>
	/// <remarks>
	/// Two CPUKey instances are considered equal if their underlying byte arrays are sequence-equal. This method can also compare against a
	/// byte array or a hexadecimal string that represents a CPUKey. String comparisons are performed in an ordinal and case-insensitive
	/// manner.
	/// </remarks>
	/// <param name="obj">The object to compare with the current instance.</param>
	/// <returns>true if the specified object is equal to the current instance; otherwise, false.</returns>
	public override bool Equals([NotNullWhen(true)] object? obj) => obj switch
	{
		CPUKey cpukey => Equals(cpukey),
		byte[] arr => Equals(arr),
		string str => Equals(str),
		_ => false
	};

	/// <summary>
	/// Generates a hash code for the current CPUKey instance, optimized for performance and collision resistance. This implementation
	/// treats the underlying 16-byte data as two 64-bit integers, applies XOR folding to combine them, and mixes the result using a large
	/// prime multiplier to ensure good hash distribution.
	/// </summary>
	/// <remarks>
	/// The method leverages <see cref="MemoryMarshal"/> to perform memory-efficient reads without allocations. XOR folding helps capture
	/// small differences between CPUKeys, while the prime multiplier spreads out hash values to minimize collisions in hash-based
	/// collections. This hash code is suitable for use in high-performance collections such as <see cref="HashSet{T}"/> and <see
	/// cref="Dictionary{TKey, TValue}"/>.
	/// </remarks>
	/// <returns>A 32-bit integer hash code representing the CPUKey.</returns>
	public override int GetHashCode()
	{
		var parts = MemoryMarshal.Cast<byte, ulong>(data.Span); // reinterpret as ulongs
		var hash = parts[0] ^ parts[1]; // xor-fold
		hash *= 0x9E3779B185EBCA87UL; // golden ratio
		return (int)(hash ^ (hash >> 32)); // mix bits and truncate
	}

	/// <summary>
	/// Returns the <see cref="String"/> representation of the CPUKey object.
	/// </summary>
	/// <returns>A UTF-16 encoded hexadecimal <see cref="String"/> representing the CPUKey.</returns>
	public override string ToString() => ConvertToHexString();

	#endregion

	#region IEquatable<CPUKey>

	/// <summary>
	/// Determines whether the specified CPUKey is equal to the current CPUKey instance.
	/// </summary>
	/// <inheritdoc cref="Equals(object)"/>
	/// <param name="other">The CPUKey to compare with the current instance.</param>
	public bool Equals([NotNullWhen(true)] CPUKey? other) => other is not null && data.Span.SequenceEqual(other.data.Span);

	/// <summary>
	/// Determines whether the specified byte array is equal to the current CPUKey instance.
	/// </summary>
	/// <inheritdoc cref="Equals(object)"/>
	/// <param name="value">The byte array to compare with the current instance.</param>
	public bool Equals(ReadOnlySpan<byte> value) => data.Span.SequenceEqual(value);

	/// <summary>
	/// Determines whether the specified string is equal to the current CPUKey instance.
	/// </summary>
	/// <inheritdoc cref="Equals(object)"/>
	/// <param name="value">The string to compare with the current instance.</param>
	public bool Equals(ReadOnlySpan<char> value) => ConvertToHexString().AsSpan().Equals(value, StringComparison.OrdinalIgnoreCase);

	#endregion

	#region IComparable<CPUKey>

	/// <summary>
	/// Compares the current instance with another CPUKey object and returns an integer that indicates whether the current instance
	/// precedes, follows, or occurs in the same position in the sort order as the other object.
	/// </summary>
	/// <remarks>
	/// This method performs a lexographic byte-wise comparison using the underlying byte array of the CPUKey.
	/// </remarks>
	/// <param name="other">The CPUKey object to compare with this instance.</param>
	/// <returns>
	/// A value that indicates the relative order of the objects being compared. The return value has these meanings:
	/// <br/>- Less than zero: This instance precedes <paramref name="other"/> in the sort order.
	/// <br/>- Zero: This instance occurs in the same position in the sort order as <paramref name="other"/>.
	/// <br/>- Greater than zero: This instance follows <paramref name="other"/> in the sort order.
	/// </returns>
	public int CompareTo(CPUKey? other) => other is not null ? data.Span.SequenceCompareTo(other.data.Span) : 1;

	#endregion

	#region Operators

	public static bool operator ==(CPUKey left, CPUKey right) => left.Equals(right);
	public static bool operator !=(CPUKey left, CPUKey right) => !left.Equals(right);
	public static bool operator ==(CPUKey left, ReadOnlySpan<byte> right) => left.Equals(right);
	public static bool operator !=(CPUKey left, ReadOnlySpan<byte> right) => !left.Equals(right);
	public static bool operator ==(CPUKey left, ReadOnlySpan<char> right) => left.Equals(right);
	public static bool operator !=(CPUKey left, ReadOnlySpan<char> right) => !left.Equals(right);

	public static bool operator <(CPUKey left, CPUKey right) => left.CompareTo(right) < 0;
	public static bool operator <=(CPUKey left, CPUKey right) => left.CompareTo(right) <= 0;
	public static bool operator >(CPUKey left, CPUKey right) => left.CompareTo(right) > 0;
	public static bool operator >=(CPUKey left, CPUKey right) => left.CompareTo(right) >= 0;

	#endregion

	#region Implementation Detail

	internal static byte[] SanitizeInput(ReadOnlySpan<byte> value)
	{
		if (value.IsEmpty)
			throw new ArgumentException("Value cannot be empty.", nameof(value));
		if (value.Length != ValidByteLen)
			throw new ArgumentOutOfRangeException(nameof(value), value.ToArray(), $"Value must be {ValidByteLen} bytes long.");
		if (All(value, x => x == 0x00))
			throw new FormatException("Value cannot be all zeroes.");
		return value.ToArray();
	}

	internal static byte[]? SanitizeInputSafe(ReadOnlySpan<byte> value)
		=> value.IsEmpty || value.Length != ValidByteLen || All(value, x => x == 0x00) ? default : value.ToArray();

	internal static byte[] SanitizeInput(ReadOnlySpan<char> value)
	{
		if (value.IsEmpty)
			throw new ArgumentException("Value cannot be empty.", nameof(value));
		if (value.Length != ValidCharLen)
			throw new ArgumentOutOfRangeException(nameof(value), value.ToString(), $"Value must be {ValidCharLen} hexadecimal characters long.");
		if (All(value, x => x == '0') || !All(value, x => IsHexCharacter(x)))
			throw new FormatException("Value cannot be all zeroes and must only contain hexadecimal digits.");
		return Convert.FromHexString(value);
	}

	internal static byte[]? SanitizeInputSafe(ReadOnlySpan<char> value)
		=> value.IsEmpty || value.Length != ValidCharLen || All(value, x => x == '0') || !All(value, x => IsHexCharacter(x)) ? default : Convert.FromHexString(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void ValidateData(ReadOnlySpan<byte> value)
	{
		if (!VerifyHammingWeight(value))
			throw new CPUKeyHammingWeightException(value.ToArray());
		if (!VerifyECD(value))
			throw new CPUKeyECDException(value.ToArray());
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static bool ValidateDataSafe(ReadOnlySpan<byte> value)
		=> VerifyHammingWeight(value) && VerifyECD(value);

	/// <summary>
	/// Verifies that the Hamming weight (non-zero bit count, or popcount) of the given data is 0x35.
	/// </summary>
	/// <param name="value">The CPUKey data bytes to validate.</param>
	/// <returns>True if the Hamming weight is 0x35, false otherwise.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static bool VerifyHammingWeight(ReadOnlySpan<byte> value)
		=> ComputeHammingWeight(value) == ValidHammingWeight;

	/// <summary>
	/// Verifies the Error Correction and Detection (ECD) bits within a CPUKey by comparing them against a re-computed set.
	/// </summary>
	/// <param name="value">The CPUKey data bytes to validate.</param>
	/// <returns>True if the re-computed ECD matches the original, false otherwise.</returns>
	internal static bool VerifyECD(ReadOnlySpan<byte> value)
	{
		Span<byte> span = stackalloc byte[ValidByteLen];
		value.CopyTo(span);
		ComputeECD(span);
		return span.SequenceEqual(value);
	}

	/// <summary>
	/// Computes the Hamming weight (non-zero bit count, or popcount) of the given data. The ECD mask is used to exclude the
	/// 22 bits designated for Error Correction and Detection (ECD) in the CPUKey.
	/// </summary>
	/// <param name="value">The CPUKey data bytes to validate.</param>
	/// <returns>The Hamming weight of the CPUKey data.</returns>
	internal static int ComputeHammingWeight(ReadOnlySpan<byte> value)
	{
		// scratch space on the stack - faster than byte[] (zero allocations)
		Span<byte> span = stackalloc byte[ValidByteLen];
		value.CopyTo(span);

		// reverse byte order in-place and reinterpret as ulongs for processing on LE arch
		span[..sizeof(ulong)].Reverse();
		span[sizeof(ulong)..].Reverse();
		Span<ulong> parts = MemoryMarshal.Cast<byte, ulong>(span);

		// last 22 high bits (MSB) are reserved for ECD and excluded using a 64-bit (LE) mask
		const ulong ecdMask = 0xFFFF_FFFF_FF03_0000; // (BE: 0x03FF), cpukey bits 106-127 (inclusive)

		// apply mask and count set bits in one shot using hardware intrinsics (fast)
		return BitOperations.PopCount(parts[0]) + BitOperations.PopCount(parts[1] & ecdMask);
	}

	/// <summary>
	/// Calculates and updates the Error Correction and Detection (ECD) bits for the given CPUKey data bytes. These bits are used to detect
	/// and correct single-bit errors, and to detect (but not correct) double-bit errors. The data in <paramref name="value"/> is modified
	/// in-place.
	/// <para>
	/// Implements a type of customized Linear Feedback Shift Register (LFSR) variant for the ECD computation, simultaneously performing
	/// Hamming weight error correction. The algorithm is designed to be cryptographically secure and tamper resistant, notably XOR'ing each
	/// set bit with the magic constant 0x360325, most likely as a cheeky obfuscation measure.
	/// </para>
	/// </summary>
	/// <remarks>
	/// While it is commonly believed that the Xbox 360 had a failure rate close to 30%, Microsoft "unnoficially" claimed it to be only 3-5%
	/// shortly after the initial console launch. In the context of their ECD calculation, there is actually a semblance of truth to
	/// Microsoft's claim: the ECD calculation, which is keyed against a value of 0x360325, indeed results in an 0x360 3-two-5% error rate.
	/// </remarks>
	/// <param name="value">The CPUKey data bytes for which to recalculate the ECD bits. The data is modified in-place.</param>
	internal static void ComputeECD(Span<byte> value)
	{
		uint acc1 = 0;
		uint acc2 = 0;

		for (var i = 0; i < 128; i++, acc1 >>= 1)
		{
			byte bTmp = value[i >> 3];
			uint dwTmp = (uint)((bTmp >> (i & 7)) & 1);

			if (i < 0x6A)
			{
				acc1 ^= dwTmp;
				if ((acc1 & 1) > 0)
					acc1 ^= 0x360325;
				acc2 ^= dwTmp;
			}
			else if (i < 0x7F)
			{
				if (dwTmp != (acc1 & 1))
					value[i >> 3] = (byte)((1 << (i & 7)) ^ (bTmp & 0xFF));
				acc2 ^= acc1 & 1;
			}
			else if (dwTmp != acc2)
			{
				value[0xF] = (byte)((0x80 ^ bTmp) & 0xFF);
			}
		}
	}

	/// <summary>
	/// Converts the CPUKey's underlying data consisting of 8-bit unsigned integers to its equivalent string representation that is
	/// encoded with uppercase hex characters.
	/// </summary>
	/// <remarks>
	/// This method is a high-performance, zero-allocation custom implementation of <see cref="Convert.ToHexString(ReadOnlySpan{byte})"/>,
	/// designed to avoid redundant bounds checking and error handling that was already done during construction. A CPUKey instance favors
	/// deferred string conversion over caching, but comparisons/equality checks performed against another string are likely to be called in
	/// a tight loop or performance-critical path, hence the custom implementation.
	/// </remarks>
	/// <returns>A string representing the CPUKey in uppercase hexadecimal format.</returns>
	private string ConvertToHexString()
	{
		if (data.IsEmpty)
			return String.Empty;

		Span<char> span = stackalloc char[ValidCharLen];
		for (int i = 0; i < data.Span.Length; i++)
		{
			byte b = data.Span[i];
			span[i * 2] = GetHexValue(b >> 4); // high nibble
			span[i * 2 + 1] = GetHexValue(b & 0xF); // low nibble
		}

		return new String(span);
	}

	/// <summary>
	/// Converts a nibble to its corresponding uppercase hex character.
	/// </summary>
	/// <param name="nibble">A value from 0-15 representing the 4-bit nibble.</param>
	/// <returns>The uppercase hex character ('0'-'9', 'A'-'F').</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static char GetHexValue(int nibble) => (char)(nibble < 10 ? nibble + '0' : nibble - 10 + 'A');

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool IsHexCharacter(char value) => value is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

	private static bool All<T>(ReadOnlySpan<T> span, Predicate<T> predicate)
	{
		for (int i = 0; i < span.Length; i++)
			if (!predicate(span[i]))
				return false;
		return true;
	}

	#endregion
}

public static partial class CPUKeyExtensions
{
	public static byte[] GetDigest(this CPUKey cpukey) => SHA1.HashData(cpukey.AsSpan());
}

public class CPUKeyException : Exception
{
	public string Name { get; init; } = nameof(CPUKeyException);
	public object SourceData { get; init; }
	internal CPUKeyException(byte[] sourceData, string? message) : base(message) => SourceData = sourceData;
	internal CPUKeyException(string name, byte[] sourceData, string? message) : base(message) => (Name, SourceData) = (name, sourceData);
	public override string ToString()
	{
		var sb = new StringBuilder(Message);
		sb.AppendLine($"   {Name} [{SourceData}]");
		sb.AppendLine(StackTrace);
		return sb.ToString();
	}
}

public sealed class CPUKeyHammingWeightException : CPUKeyException
{
	internal CPUKeyHammingWeightException(byte[] data) : base("Invalid Hamming weight", data, null) { }
	internal CPUKeyHammingWeightException(byte[] data, string? message) : base("Invalid Hamming weight", data, message) { }
}

public sealed class CPUKeyECDException : CPUKeyException
{
	internal CPUKeyECDException(byte[] data) : base("Invalid ECD", data, null) { }
	internal CPUKeyECDException(byte[] data, string? message) : base("Invalid ECD", data, message) { }
}

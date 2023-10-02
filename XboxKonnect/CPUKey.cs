/*
 * CPUKey class
 * Created: 01/20/2020
 * Author:  Daniel McClintock (alias: Stelio Kontos)
 *
 * Copyright (c) 2020 Daniel McClintock
 */

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace SK;

/// <summary>
/// Encapsulates a 16-byte (32-character hexidecimal) Xbox 360 CPUKey, and provides parsing, validation, conversion, and utility methods.
/// </summary>
public sealed class CPUKey : IEquatable<CPUKey>, IComparable<CPUKey>
{
	private static readonly int ValidByteLen = 0x10;
	private static readonly int ValidCharLen = 0x20;

	private readonly Memory<byte> data = Memory<byte>.Empty;

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
		data = new byte[ValidByteLen];
		other.data.CopyTo(data);
	}

	/// <summary>
	/// Initializes a new CPUKey instance from a byte array.
	/// </summary>
	/// <param name="value">The <see cref="ReadOnlySpan{T}"/> representation of a CPUKey <see cref="Array"/> to validate and parse.</param>
	/// <exception cref="ArgumentException"><paramref name="value"/> cannot be empty.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> must be 16 bytes long.</exception>
	/// <exception cref="FormatException"><paramref name="value"/> cannot be all zeroes.</exception>
	/// <exception cref="CPUKeyHammingWeightException"></exception>
	/// <exception cref="CPUKeyECDException"></exception>
	public CPUKey(ReadOnlySpan<byte> value)
	{
		data = SanitizeInput(value);
		ValidateData(data.Span);
	}

	/// <summary>
	/// Initializes a new CPUKey instance from a hex <see cref="String"/> representation of the underlying 8-bit unsigned integer array.
	/// </summary>
	/// <param name="value">The <see cref="ReadOnlySpan{T}"/> representation of a CPUKey <see cref="Array"/> to validate and parse.</param>
	/// <exception cref="ArgumentException"><paramref name="value"/> cannot be empty.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> must be 32 hexidecimal chars long.</exception>
	/// <exception cref="FormatException"><paramref name="value"/> cannot be all zeroes and must only contain hexidecimal
	/// digits.</exception>
	/// <exception cref="CPUKeyHammingWeightException"></exception>
	/// <exception cref="CPUKeyECDException"></exception>
	public CPUKey(ReadOnlySpan<char> value)
	{
		data = SanitizeInput(value);
		ValidateData(data.Span);
	}

	private CPUKey(byte[] value) => data = new Memory<byte>(value);

	/// <summary>
	/// Generates a cryptographically random CPUKey instance with valid Hamming weight and ECD.
	/// </summary>
	/// <returns>A new CPUKey object.</returns>
	public static CPUKey CreateRandom()
	{
		Span<byte> span = stackalloc byte[ValidByteLen];
		using var rng = RandomNumberGenerator.Create();
		do { rng.GetNonZeroBytes(span); } while (!ValidateHammingWeight(span));
		ComputeECD(span);
		return new CPUKey(span);
	}

	/// <summary>
	/// Creates a new CPUKey instance from a byte array.
	/// </summary>
	/// <param name="value">The <see cref="ReadOnlySpan{T}"/> representation of a CPUKey <see cref="Array"/> to validate and parse.</param>
	/// <returns>If parsing succeeds, a new CPUKey object, otherwise null.</returns>
	public static CPUKey? Parse(ReadOnlySpan<byte> value) => SanitizeInputSafe(value) switch
	{
		byte[] bytes when ValidateDataSafe(bytes) => new CPUKey(bytes),
		_ => default
	};

	/// <summary>
	/// Creates a new CPUKey instance from a <see cref="String"/>.
	/// </summary>
	/// <param name="value">The <see cref="ReadOnlySpan{T}"/> representation of a CPUKey <see cref="String"/> to validate and parse.</param>
	/// <returns>If parsing succeeds, a new CPUKey object, otherwise null.</returns>
	public static CPUKey? Parse(ReadOnlySpan<char> value) => SanitizeInputSafe(value) switch
	{
		byte[] bytes when ValidateDataSafe(bytes) => new CPUKey(bytes),
		_ => default
	};

	/// <summary>
	/// Validates the given CPUKey byte array, initializing a new CPUKey instance at <paramref name="cpukey"/>.
	/// </summary>
	/// <param name="value">The <see cref="ReadOnlySpan{T}"/> representation of a CPUKey <see cref="Array"/> to validate and parse.</param>
	/// <param name="cpukey">
	/// When this method returns, contains the <see cref="CPUKey"/> value equivalent of the byte sequence contained in <paramref
	/// name="value"/>, if the conversion succeeded, or <see cref="Empty"/> if the conversion failed. The conversion fails if the
	/// <paramref name="value"/> is not of the correct format. This parameter is passed uninitialized.
	/// </param>
	/// <returns>true if <paramref name="value"/> represents a valid CPUKey, otherwise false.</returns>
	public static bool TryParse(ReadOnlySpan<byte> value, [NotNullWhen(true)] out CPUKey? cpukey)
	{
		cpukey = Parse(value) ?? Empty;
		return cpukey != Empty;
	}

	/// <summary>
	/// Validates the given CPUKey <see cref="String"/> representation, initializing a new CPUKey instance at <paramref name="cpukey"/>.
	/// </summary>
	/// <param name="value">The <see cref="ReadOnlySpan{T}"/> representation of a CPUKey <see cref="String"/> to validate and parse.</param>
	/// <param name="cpukey">
	/// When this method returns, contains the <see cref="CPUKey"/> value equivalent of the byte sequence contained in <paramref
	/// name="value"/>, if the conversion succeeded, or <see cref="Empty"/> if the conversion failed. The conversion fails if the
	/// <paramref name="value"/> is not of the correct format. This parameter is passed uninitialized.
	/// </param>
	/// <returns>true if <paramref name="value"/> represents a valid CPUKey, otherwise false.</returns>
	public static bool TryParse(ReadOnlySpan<char> value, [NotNullWhen(true)] out CPUKey? cpukey)
	{
		cpukey = Parse(value) ?? Empty;
		return cpukey != Empty;
	}

	/// <summary>
	/// Determines whether the current CPUKey instance represents a valid CPUKey.
	/// </summary>
	/// <returns>true if the current instance is not equal to <see cref="Empty"/>; otherwise, false.</returns>
	public bool IsValid() => !Equals(Empty);

	public byte[] GetDigest() => SHA1.Create().ComputeHash(ToArray());

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

	/// <summary>
	/// Returns the <see cref="String"/> representation of the CPUKey object.
	/// </summary>
	/// <returns>A UTF-16 encoded <see cref="String"/> representing the CPUKey.</returns>
	public override string ToString() => Convert.ToHexString(data.Span);

	/// <inheritdoc/>
	public override int GetHashCode() => data.GetHashCode();

	/// <inheritdoc/>
	public override bool Equals([NotNullWhen(true)] object? obj) => obj switch
	{
		byte[] arr => Equals(arr),
		string str => Equals(str),
		CPUKey cpukey => Equals(cpukey),
		_ => false
	};

	/// <inheritdoc/>
	public bool Equals([NotNullWhen(true)] CPUKey? other) => other is not null && data.Span.SequenceEqual(other.data.Span);

	/// <summary>
	/// Indicates whether the current object is equal to <paramref name="value"/>.
	/// </summary>
	/// <param name="value">The byte array representation of a CPUKey.</param>
	/// <returns>true if the CPUKey instance is equal to <paramref name="value"/>, otherwise false.</returns>
	public bool Equals(ReadOnlySpan<byte> value) => data.Span.SequenceEqual(value);

	/// <summary>
	/// Indicates whether the current object is equal to <paramref name="value"/>.
	/// </summary>
	/// <param name="value">The hex string representation of a CPUKey.</param>
	/// <returns>true if the CPUKey instance is equal to <paramref name="value"/>, otherwise false.</returns>
	public bool Equals(ReadOnlySpan<char> value) => ToString().AsSpan().SequenceEqual(value);

	/// <inheritdoc/>
	public int CompareTo(CPUKey? other) => other is not null ? data.Span.SequenceCompareTo(other.data.Span) : 1;

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

	private static byte[] SanitizeInput(ReadOnlySpan<byte> value)
	{
		if (value.IsEmpty)
			throw new ArgumentException("Value cannot be empty.", nameof(value));
		if (value.Length != ValidByteLen)
			throw new ArgumentOutOfRangeException(nameof(value), value.ToArray(), $"Value must be {ValidByteLen} bytes long.");
		if (All(value, x => x == 0x00))
			throw new FormatException("Value cannot be all zeroes.");
		return value.ToArray();
	}

	private static byte[] SanitizeInput(ReadOnlySpan<char> value)
	{
		if (value.IsEmpty)
			throw new ArgumentException("Value cannot be empty.", nameof(value));
		if (value.Length != ValidCharLen)
			throw new ArgumentOutOfRangeException(nameof(value), value.ToString(), $"Value must be {ValidCharLen} hexidecimal chars long.");
		if (All(value, x => x == '0') || !All(value, x => IsHexDigit(x)))
			throw new FormatException("Value cannot be all zeroes and must only contain hexidecimal digits.");
		return Convert.FromHexString(value);
	}

	private static byte[]? SanitizeInputSafe(ReadOnlySpan<byte> value)
		=> value.IsEmpty || value.Length != ValidByteLen ? default : value.ToArray();

	private static byte[]? SanitizeInputSafe(ReadOnlySpan<char> value)
		=> value.IsEmpty || value.Length != ValidCharLen || !All(value, x => IsHexDigit(x)) ? default : Convert.FromHexString(value);

	private static void ValidateData(ReadOnlySpan<byte> value)
	{
		if (!ValidateHammingWeight(value))
			throw new CPUKeyHammingWeightException(value.ToArray());
		if (!ValidateECD(value))
			throw new CPUKeyECDException(value.ToArray());
	}

	private static bool ValidateDataSafe(ReadOnlySpan<byte> value) => ValidateHammingWeight(value) && ValidateECD(value);

	/// <summary>
	/// Validates that the Hamming weight (non-zero bit count, or popcount) of the given data is 0x35. The ECD mask is used to exclude the
	/// 22 bits designated for Error Correction and Detection (ECD) in the CPUKey.
	/// </summary>
	/// <param name="value">The CPUKey data bytes to validate.</param>
	/// <returns>True if the Hamming weight is 0x35, false otherwise.</returns>
	private static bool ValidateHammingWeight(ReadOnlySpan<byte> value)
	{
		// scratch space on the stack - faster than byte[] (no heap allocations)
		Span<byte> span = stackalloc byte[ValidByteLen];
		value.CopyTo(span);

		// swap endianness since we'll be processing it in two 64-bit operations
		span[..sizeof(ulong)].Reverse();
		span[sizeof(ulong)..].Reverse();

		// reinterpret our span as a pair of ulongs for our bitwise ops
		// we'll just use hardware intrinsics to perform the bit twiddling (fast)
		// mask is applied to the second value for the final Hamming weight
		const ulong ecdMask = 0xFFFF_FFFF_FF03_0000;
		Span<ulong> parts = MemoryMarshal.Cast<byte, ulong>(span);
		var hammingWeight = BitOperations.PopCount(parts[0]) + BitOperations.PopCount(parts[1] & ecdMask);

		// anything other than 0x35 is invalid
		return hammingWeight == 0x35;
	}

	/// <summary>
	/// Validates the Error Correction and Detection (ECD) bits within a CPUKey by comparing them against a re-computed set.
	/// </summary>
	/// <param name="value">The CPUKey data bytes to validate.</param>
	/// <returns>True if the re-computed ECD matches the original, false otherwise.</returns>
	private static bool ValidateECD(ReadOnlySpan<byte> value)
	{
		Span<byte> span = stackalloc byte[ValidByteLen];
		value.CopyTo(span);
		ComputeECD(span);
		return span.SequenceEqual(value);
	}

	/// <summary>
	/// Calculates and updates the Error Checking and Correction (ECC) bits for the given CPUKey data bytes. These bits are used to detect
	/// and correct single-bit errors, and to detect (but not correct) double-bit errors. The data in <paramref name="value"/> is modified
	/// in-place.
	/// <para>
	/// It implements a type of binary Linear Feedback Shift Register (LFSR) for the ECC computation, relying on the Hamming weight, and
	/// notably XOR'ing each set bit with the "magic" constant 0x360325.
	/// </para>
	/// </summary>
	/// <remarks>
	/// While it is commonly believed that the Xbox 360 had a failure rate close to 30%, Microsoft "unnoficially" claimed it to be only 3-5%
	/// shortly after the initial console launch. In the context of their ECD calculation, there is actually a semblance of truth to
	/// Microsoft's claim: the ECC calculation, which is keyed against a value of 0x360325, indeed results in an 0x360 3-two-5% error rate.
	/// </remarks>
	/// <param name="value">A Span containing the CPUKey's byte data for which to recalculate the ECC bits.</param>
	private static void ComputeECD(Span<byte> value)
	{
		// accumulator vars
		uint acc1 = 0;
		uint acc2 = 0;

		for (var i = 0; i < 128; i++, acc1 >>= 1) // foreach (bit in cpukey)
		{
			var bTmp = value[i >> 3];
			uint dwTmp = (uint)((bTmp >> (i & 7)) & 1);

			if (i < 0x6A) // if (i < 106) // (hammingweight * 2)
			{
				acc1 ^= dwTmp;
				if ((acc1 & 1) > 0)
					acc1 ^= 0x360325; // easter egg magic
				acc2 ^= dwTmp;
			}
			else if (i < 0x7F) // else if (i != lastbit) // (127)
			{
				if (dwTmp != (acc1 & 1))
					value[i >> 3] = (byte)((1 << (i & 7)) ^ (bTmp & 0xFF));
				acc2 ^= (acc1 & 1);
			}
			else if (dwTmp != acc2)
			{
				value[0xF] = (byte)((0x80 ^ bTmp) & 0xFF); // ((128 ^ bTmp) & 0xFF)
			}
		}
	}

	private static bool IsHexDigit(char value) => value is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

	private static bool All<T>(ReadOnlySpan<T> span, Predicate<T> predicate)
	{
		for (int i = 0; i < span.Length; i++)
			if (!predicate(span[i]))
				return false;
		return true;
	}
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

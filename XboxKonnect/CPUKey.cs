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
/// Encapsulates a 16-byte (32-character hexidecimal) Xbox 360 CPUKey,
/// and provides parsing, validation, conversion, and utility methods.
/// </summary>
public class CPUKey : IEquatable<CPUKey>, IComparable<CPUKey>
{
	private readonly Memory<byte> data = Memory<byte>.Empty;

	private static readonly int ValidByteLen = 0x10;
	private static readonly int ValidCharLen = 0x20;
	private static readonly ulong EcdMask = 0xFFFF_FFFF_FF03_0000; // reverse endianness: 0x000003FF

	/// <summary>
	/// Returns the validation result of this CPUKey object set from the last call to <see cref="Validate"/> or <see cref="IsValid"/>.
	/// </summary>
	public CPUKeyError ErrorCode { get; private set; } = CPUKeyError.Unknown;

	/// <summary>
	/// Returns an empty CPUKey object.
	/// </summary>
	public static readonly CPUKey Empty = new();

	/// <summary>
	/// Initializes a new instance as an empty CPUKey. See also <see cref="Empty"/>.
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

		data = new byte[ValidByteLen];
		other.data.CopyTo(data);
		ErrorCode = other.ErrorCode;
	}

	/// <summary>
	/// Initializes a new CPUKey instance from a byte array.
	/// </summary>
	/// <param name="value">The <seealso cref="ReadOnlySpan{T}"/> representation of a CPUKey <seealso cref="Array"/> to validate and parse</param>
	/// <exception cref="ArgumentException"><paramref name="value"/> length is not 0x10 (16)</exception>
	public CPUKey(ReadOnlySpan<byte> value)
	{
		if (value.Length != ValidByteLen)
			throw new ArgumentException("Source length must be 0x10 bytes.", nameof(value));

		data = new byte[ValidByteLen];
		value.CopyTo(data.Span);
	}

	/// <summary>
	/// Initializes a new CPUKey instance from a hex <seealso cref="String"/> representation of the underlying 8-bit unsigned integer array.
	/// </summary>
	/// <param name="value">The <seealso cref="ReadOnlySpan{T}"/> representation of a CPUKey <seealso cref="Array"/> to validate and parse</param>
	/// <exception cref="ArgumentException"><paramref name="value"/> length is not 0x20 (32)</exception>
	public CPUKey(ReadOnlySpan<char> value)
	{
		if (!ValidateString(value))
			throw new ArgumentException("Source length must be 0x20 chars.", nameof(value));

		data = Convert.FromHexString(value);
	}

	/// <summary>
	/// Creates a new CPUKey instance from a byte array.
	/// </summary>
	/// <param name="value">The <seealso cref="ReadOnlySpan{T}"/> representation of a CPUKey <seealso cref="Array"/> to validate and parse</param>
	/// <returns>A new CPUKey object</returns>
	public static CPUKey? Parse(ReadOnlySpan<byte> value)
	{
		if (value.Length != ValidByteLen)
			return null;
		return new CPUKey(value);
	}

	/// <summary>
	/// Creates a new CPUKey instance from a <seealso cref="String"/>.
	/// </summary>
	/// <param name="value">The <seealso cref="ReadOnlySpan{T}"/> representation of a CPUKey <seealso cref="String"/> to validate and parse</param>
	/// <returns>A new CPUKey object</returns>
	public static CPUKey? Parse(ReadOnlySpan<char> value)
	{
		if (!ValidateString(value))
			return null;
		return new CPUKey(value);
	}

	/// <summary>
	/// Validates the given CPUKey byte array, initializing a new CPUKey instance at <paramref name="cpukey"/>
	/// </summary>
	/// <param name="value">The <seealso cref="ReadOnlySpan{T}"/> representation of a CPUKey <seealso cref="Array"/> to validate and parse</param>
	/// <param name="cpukey">A new CPUKey instance</param>
	/// <returns>true if <paramref name="value"/> represents a valid CPUKey, otherwise false</returns>
	public static bool TryParse(ReadOnlySpan<byte> value, [NotNullWhen(true)] out CPUKey? cpukey)
	{
		cpukey = Parse(value);
		return cpukey is not null;
	}

	/// <summary>
	/// Validates the given CPUKey <seealso cref="String"/> representation, initializing a new CPUKey instance at <paramref name="cpukey"/>
	/// </summary>
	/// <param name="value">The <seealso cref="ReadOnlySpan{T}"/> representation of a CPUKey <seealso cref="String"/> to validate and parse</param>
	/// <param name="cpukey">A new CPUKey instance</param>
	/// <returns>true if <paramref name="value"/> represents a valid CPUKey, otherwise false</returns>
	public static bool TryParse(ReadOnlySpan<char> value, [NotNullWhen(true)] out CPUKey? cpukey)
	{
		cpukey = Parse(value);
		return cpukey is not null;
	}

	/// <summary>
	/// Creates a random CPUKey.
	/// </summary>
	/// <returns>A valid, randomly generated CPUKey object</returns>
	public static CPUKey CreateRandom()
	{
		Span<byte> span = stackalloc byte[0x10];
		using var rng = RandomNumberGenerator.Create();
		do { rng.GetNonZeroBytes(span); } while (!ValidateHammingWeight(span));
		ComputeECD(span);
		return new CPUKey(span) { ErrorCode = CPUKeyError.Valid };
	}

	public byte[] GetDigest() => SHA1.Create().ComputeHash(ToArray());

	/// <summary>
	/// Validates a CPUKey object using hamming weight verification and ECD checks,
	/// and updates the <see cref="ErrorCode"/> property with the validation result.
	/// Will not throw.
	/// </summary>
	/// <returns>Returns true if the object is a valid CPUKey, otherwise false</returns>
	public bool IsValid()
	{
		if (ErrorCode != CPUKeyError.Valid)
			ValidateNoThrow();
		return ErrorCode == CPUKeyError.Valid;
	}

	/// <summary>
	/// Validates the CPUKey's data length, hamming weight, and ECD.
	/// </summary>
	/// <exception cref="CPUKeyDataInvalidException"></exception>
	/// <exception cref="CPUKeyHammingWeightInvalidException"></exception>
	/// <exception cref="CPUKeyECDInvalidException"></exception>
	public void Validate()
		{
			if (data.Span.IsEmpty)
			throw new CPUKeyDataInvalidException(this, "CPUKey Data cannot be empty.");
		if (!ValidateHammingWeight(data.Span))
				throw new CPUKeyHammingWeightInvalidException(this);
		if (!ValidateECD(data.Span))
				throw new CPUKeyECDInvalidException(this);
		ErrorCode = CPUKeyError.Valid;
	}

	/// <summary>
	/// Validates the CPUKey's data length, hamming weight, and ECD.
	/// Updates the <see cref="ErrorCode"/> property with the validation result.
	/// Will not throw.
	/// </summary>
	public void ValidateNoThrow()
	{
		if (data.Span.IsEmpty)
			ErrorCode = CPUKeyError.InvalidData;
		else if (!ValidateHammingWeight(data.Span))
			ErrorCode = CPUKeyError.InvalidHammingWeight;
		else if (!ValidateECD(data.Span))
			ErrorCode = CPUKeyError.InvalidECD;
		else ErrorCode = CPUKeyError.Valid;
	}

	/// <summary>
	/// Returns a <seealso cref="ReadOnlySpan{T}"/> from the current CPUKey instance
	/// </summary>
	/// <returns>A <seealso cref="ReadOnlySpan{T}"/> created from the CPUKey object</returns>
	public ReadOnlySpan<byte> AsSpan() => data.Span;

	/// <summary>
	/// Copies the CPUKey into a new byte array.
	/// </summary>
	/// <returns>An <seealso cref="Array"/> containing the CPUKey</returns>
	public byte[] ToArray() => data.ToArray();

	/// <summary>
	/// Returns the <seealso cref="String"/> representation of the CPUKey object.
	/// </summary>
	/// <returns>A UTF-16 encoded <seealso cref="String"/> representing the CPUKey</returns>
	public override string ToString() => Convert.ToHexString(data.Span);

	/// <inheritdoc/>
	public override int GetHashCode() => data.GetHashCode();

	/// <inheritdoc/>
	public override bool Equals([NotNullWhen(true)] object? obj) => obj switch
	{
		Byte[] arr => Equals(arr),
		String str => Equals(str),
		CPUKey cpukey => Equals(cpukey),
		_ => false
	};

	/// <summary>
	/// Indicates whether the current object is equal to <paramref name="other"/>.
	/// </summary>
	/// <param name="other">The comparand as a <see cref="CPUKey"/></param>
	/// <returns>true if the CPUKey instance is equal to <paramref name="other"/>, otherwise false</returns>
	public bool Equals([NotNullWhen(true)] CPUKey? other) => other is not null && data.Span.SequenceEqual(other.data.Span);

	/// <summary>
	/// Indicates whether the current object is equal to <paramref name="value"/>.
	/// </summary>
	/// <param name="value">The comparand <seealso cref="ReadOnlySpan{T}"/> representation of a CPUKey <seealso cref="Array"/></param>
	/// <returns>true if the CPUKey instance is equal to <paramref name="value"/>, otherwise false</returns>
	public bool Equals(ReadOnlySpan<byte> value) => data.Span.SequenceEqual(value);

	/// <summary>
	/// Indicates whether the current object is equal to <paramref name="value"/>.
	/// </summary>
	/// <param name="value">The comparand <seealso cref="ReadOnlySpan{T}"/> representation of a CPUKey <seealso cref="String"/></param>
	/// <returns>true if the CPUKey instance is equal to <paramref name="value"/>, otherwise false</returns>
	public bool Equals(ReadOnlySpan<char> value) => data.Span.SequenceEqual(Convert.FromHexString(value));

	/// <inheritdoc/>
	public int CompareTo(CPUKey? other) => other is not null ? data.Span.SequenceCompareTo(other.data.Span) : 1;

	public static bool operator ==(CPUKey lhs, CPUKey rhs) => lhs.Equals(rhs);
	public static bool operator !=(CPUKey lhs, CPUKey rhs) => !lhs.Equals(rhs);
	public static bool operator ==(CPUKey lhs, ReadOnlySpan<byte> rhs) => lhs.Equals(rhs);
	public static bool operator !=(CPUKey lhs, ReadOnlySpan<byte> rhs) => !lhs.Equals(rhs);
	public static bool operator ==(CPUKey lhs, ReadOnlySpan<char> rhs) => lhs.Equals(rhs);
	public static bool operator !=(CPUKey lhs, ReadOnlySpan<char> rhs) => !lhs.Equals(rhs);

	public static bool operator <(CPUKey lhs, CPUKey rhs) => lhs.CompareTo(rhs) < 0;
	public static bool operator <=(CPUKey lhs, CPUKey rhs) => lhs.CompareTo(rhs) <= 0;
	public static bool operator >(CPUKey lhs, CPUKey rhs) => lhs.CompareTo(rhs) > 0;
	public static bool operator >=(CPUKey lhs, CPUKey rhs) => lhs.CompareTo(rhs) >= 0;

	internal static bool IsHexDigit(char value) => value is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

	internal static bool ValidateString(ReadOnlySpan<char> value)
	{
		if (value.Length != ValidCharLen)
			return false;

		for (int i = 0; i < ValidCharLen; i++)
		{
			if (!IsHexDigit(value[i]))
				return false;
		}

		return true;
	}

	internal static bool ValidateHammingWeight(ReadOnlySpan<byte> value)
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
		Span<ulong> parts = MemoryMarshal.Cast<byte, ulong>(span);
		var hammingWeight = BitOperations.PopCount(parts[0]) + BitOperations.PopCount(parts[1] & EcdMask);

		// anything other than 0x35 is invalid
		return hammingWeight == 0x35;
	}

	internal static bool ValidateECD(ReadOnlySpan<byte> value)
	{
		Span<byte> span = stackalloc byte[ValidByteLen];
		value.CopyTo(span);
		ComputeECD(span);
		return span.SequenceEqual(value);
	}

	/// <summary>
	/// While it is commonly believed that the Xbox 360 had a failure rate close to 30%, Microsoft "unnoficially"
	/// claimed it to be only 3-5% shortly after the initial console launch. In the context of their ECD calculation,
	/// there is actually a semblance of truth to Microsoft's claim: the ECC calculation, which is keyed against a
	/// value of 0x360325, indeed results in an 0x360 3-two-5% error rate.
	/// </summary>
	internal static void ComputeECD(Span<byte> value)
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
}

public class CPUKeyException : Exception
{
	public string Name { get; init; } = nameof(CPUKeyException);
	public CPUKey CPUKey { get; init; }
	internal CPUKeyException(string name, CPUKey cpukey, string? message) : base(message) => (Name, CPUKey) = (name, cpukey);

	public override string ToString()
	{
		var sb = new StringBuilder(Message);
		sb.AppendLine($"   {Name} [{CPUKey}]");
		sb.AppendLine(StackTrace);
		return sb.ToString();
	}
}

public sealed class CPUKeyDataInvalidException : CPUKeyException
{
	internal CPUKeyDataInvalidException(CPUKey cpukey) : base("Invalid Data", cpukey, null) { }
	internal CPUKeyDataInvalidException(CPUKey cpukey, string? message) : base("Invalid Data", cpukey, message) { }
}

public sealed class CPUKeyHammingWeightInvalidException : CPUKeyException
{
	internal CPUKeyHammingWeightInvalidException(CPUKey cpukey) : base("Invalid Hamming Weight", cpukey, null) { }
	internal CPUKeyHammingWeightInvalidException(CPUKey cpukey, string? message) : base("Invalid Hamming Weight", cpukey, message) { }
}

public sealed class CPUKeyECDInvalidException : CPUKeyException
{
	internal CPUKeyECDInvalidException(CPUKey cpukey) : base("Invalid ECD", cpukey, null) { }
	internal CPUKeyECDInvalidException(CPUKey cpukey, string? message) : base("Invalid ECD", cpukey, message) { }
}

public enum CPUKeyError
{
	/// <summary>
	/// CPUKey has not been validated.
	/// </summary>
	Unknown = -1,

	/// <summary>
	/// CPUKey is valid.
	/// </summary>
	Valid,

	/// <summary>
	/// CPUKey data is empty.
	/// </summary>
	InvalidData,

	/// <summary>
	/// CPUKey failed the hamming weight validation check.
	/// </summary>
	InvalidHammingWeight,

	/// <summary>
	/// CPUKey failed the ECD validation check.
	/// </summary>
	InvalidECD,
}

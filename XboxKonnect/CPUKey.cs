/*
 * CPUKey class
 *
 * Created: 01/20/2020
 * Author:  Daniel McClintock (alias: Stelio Kontos)
 *
 * Copyright (c) 2020 Daniel McClintock
 *
 */

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace SK;

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

		data = new byte[kValidByteLen];
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
		if (value.Length != kValidByteLen)
			throw new ArgumentException("Source length is not equal to the length of a CPUKey (0x10 bytes).", nameof(value));

		data = new byte[kValidByteLen];
		value.CopyTo(data.Span);
	}

	/// <summary>
	/// Initializes a new CPUKey instance from a hex <seealso cref="String"/> representation of the underlying 8-bit unsigned integer array.
	/// </summary>
	/// <param name="value">The <seealso cref="ReadOnlySpan{T}"/> representation of a CPUKey <seealso cref="Array"/> to validate and parse</param>
	/// <exception cref="ArgumentException"><paramref name="value"/> length is not 0x20 (32)</exception>
	public CPUKey(ReadOnlySpan<char> value)
	{
		if (value.Length != kValidCharLen)
			throw new ArgumentException("Source length is not equal to the length of a CPUKey (0x20 chars).", nameof(value));

		data = Convert.FromHexString(value);
	}

	/// <summary>
	/// Creates a new CPUKey instance from a byte array.
	/// </summary>
	/// <param name="value">The <seealso cref="ReadOnlySpan{T}"/> representation of a CPUKey <seealso cref="Array"/> to validate and parse</param>
	/// <returns>A new CPUKey object</returns>
	public static CPUKey? Parse(ReadOnlySpan<byte> value)
	{
		if (value.Length != kValidByteLen)
			return default;
		return new CPUKey(value);
	}

	/// <summary>
	/// Creates a new CPUKey instance from a <seealso cref="String"/>.
	/// </summary>
	/// <param name="value">The <seealso cref="ReadOnlySpan{T}"/> representation of a CPUKey <seealso cref="String"/> to validate and parse</param>
	/// <returns>A new CPUKey object</returns>
	public static CPUKey? Parse(ReadOnlySpan<char> value)
	{
		if (!CPUKeyExtensions.ValidateString(value))
			return default;
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
		do { rng.GetNonZeroBytes(span); } while (!CPUKeyExtensions.ValidateHammingWeight(span));
		CPUKeyExtensions.ComputeECD(span);
		return new CPUKey(span) { ErrorCode = CPUKeyError.Valid };
	}

	/// <summary>
	/// Validates the CPUKey's data length, hamming weight, and ECD.
	/// </summary>
	/// <exception cref="CPUKeyDataInvalidException"></exception>
	/// <exception cref="CPUKeyHammingWeightInvalidException"></exception>
	/// <exception cref="CPUKeyECDInvalidException"></exception>
	public void Validate()
	{
		if (ErrorCode == CPUKeyError.Unknown)
		{
			if (data.Span.IsEmpty)
			{
				ErrorCode = CPUKeyError.InvalidData;
				throw new CPUKeyDataInvalidException(this);
			}

			if (!CPUKeyExtensions.ValidateHammingWeight(data.Span))
			{
				ErrorCode = CPUKeyError.InvalidHammingWeight;
				throw new CPUKeyHammingWeightInvalidException(this);
			}

			if (!CPUKeyExtensions.ValidateECD(data.Span))
			{
				ErrorCode = CPUKeyError.InvalidECD;
				throw new CPUKeyECDInvalidException(this);
			}
		}

		ErrorCode = CPUKeyError.Valid;
	}

	/// <summary>
	/// Validates a CPUKey object using hamming weight verification and ECD checks.
	/// </summary>
	/// <returns>Returns true if the object is a valid CPUKey, otherwise false</returns>
	public bool IsValid()
	{
		if (ErrorCode == CPUKeyError.Unknown)
		{
			try { Validate(); }
			catch (CPUKeyException ex) { Trace.WriteLine(ex); }
		}

		return ErrorCode == CPUKeyError.Valid;
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
		null => false,
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

	public static bool operator ==(CPUKey lhs, CPUKey rhs) => lhs.Equals(rhs);
	public static bool operator !=(CPUKey lhs, CPUKey rhs) => !lhs.Equals(rhs);
	public static bool operator ==(CPUKey lhs, ReadOnlySpan<byte> rhs) => lhs.Equals(rhs);
	public static bool operator !=(CPUKey lhs, ReadOnlySpan<byte> rhs) => !lhs.Equals(rhs);
	public static bool operator ==(CPUKey lhs, ReadOnlySpan<char> rhs) => lhs.Equals(rhs);
	public static bool operator !=(CPUKey lhs, ReadOnlySpan<char> rhs) => !lhs.Equals(rhs);
}

/// <summary>
/// Extension methods for CPUKey.
/// </summary>
public static class CPUKeyExtensions
{
	public static byte[] GetDigest(this CPUKey cpukey) => SHA1.Create().ComputeHash(cpukey.ToArray());

	internal static bool ValidateString(ReadOnlySpan<char> value)
	{
		if (value.Length != CPUKey.kValidCharLen)
			return false;

		for (int i = 0; i < CPUKey.kValidCharLen; i++)
		{
			if (!IsHexDigit(value[i]))
				return false;
		}

		return true;
	}

	internal static bool ValidateHammingWeight(ReadOnlySpan<byte> cpukeyData)
	{
		Span<byte> span = stackalloc byte[CPUKey.kValidByteLen];
		cpukeyData.CopyTo(span);
		span[..sizeof(ulong)].Reverse();
		span[sizeof(ulong)..].Reverse();

		Span<ulong> parts = MemoryMarshal.Cast<byte, ulong>(span);
		var hammingWeight = BitOperations.PopCount(parts[0]) + BitOperations.PopCount(parts[1] & CPUKey.kECDMask);

		return hammingWeight == 53;
	}

	internal static bool ValidateECD(ReadOnlySpan<byte> cpukeyData)
	{
		Span<byte> span = stackalloc byte[CPUKey.kValidByteLen];
		cpukeyData.CopyTo(span);
		ComputeECD(span);
		return span.SequenceEqual(cpukeyData);
	}

	internal static void ComputeECD(Span<byte> cpukeyData)
	{
		//uint mask  = 000003FF; // 0xFFFFFFFFFF030000 reversed

		// accumulator vars
		uint acc1 = 0;
		uint acc2 = 0;

		for (var i = 0; i < 128; i++, acc1 >>= 1) // foreach (bit in cpukey)
		{
			var bTmp = cpukeyData[i >> 3];
			uint dwTmp = (uint)((bTmp >> (i & 7)) & 1);

			if (i < 0x6A) // if (i < 106) // (hammingweight * 2)
			{
				acc1 ^= dwTmp;
				if ((acc1 & 1) > 0)
					acc1 ^= 0x360325;
				acc2 ^= dwTmp;
			}
			else if (i < 0x7F) // else if (i != lastbit) // (127)
			{
				if (dwTmp != (acc1 & 1))
					cpukeyData[i >> 3] = (byte)((1 << (i & 7)) ^ (bTmp & 0xFF));
				acc2 ^= (acc1 & 1);
			}
			else if (dwTmp != acc2)
			{
				cpukeyData[0xF] = (byte)((0x80 ^ bTmp) & 0xFF); // ((128 ^ bTmp) & 0xFF)
			}
		}
	}

	internal static bool IsHexDigit(char value) => value is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
}

public class CPUKeyException : Exception
{
	public string Name { get; init; } = nameof(CPUKeyException);
	public CPUKey CPUKey { get; init; }
	internal CPUKeyException(string name, CPUKey cpukey) : base() => (Name, CPUKey) = (name, cpukey);
	internal CPUKeyException(string name, CPUKey cpukey, string message) : base(message) => (Name, CPUKey) = (name, cpukey);

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
	internal CPUKeyDataInvalidException(CPUKey cpukey) : base("Invalid Data", cpukey) { }
	internal CPUKeyDataInvalidException(CPUKey cpukey, string message) : base("Invalid Data", cpukey, message) { }
}

public sealed class CPUKeyHammingWeightInvalidException : CPUKeyException
{
	internal CPUKeyHammingWeightInvalidException(CPUKey cpukey) : base("Invalid Hamming Weight", cpukey) { }
	internal CPUKeyHammingWeightInvalidException(CPUKey cpukey, string message) : base("Invalid Hamming Weight", cpukey, message) { }
}

public sealed class CPUKeyECDInvalidException : CPUKeyException
{
	internal CPUKeyECDInvalidException(CPUKey cpukey) : base("Invalid ECD", cpukey) { }
	internal CPUKeyECDInvalidException(CPUKey cpukey, string message) : base("Invalid ECD", cpukey, message) { }
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

using SK;

namespace XboxKonnect.Tests;

public class CPUKeyTests
{
	#region Test Data

	private static bool IsHexString(string value) => value.Length % 2 == 0
												  && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');

	private static readonly List<(string Data, string Info)> _validDataSource = new()
	{
		("C0DE8DAAE05493BCB0F1664FB1751F00", "uppercase"),
		("c0de8daae05493bcb0f1664fb1751f00", "lowercase"),
		("C0DE8daae05493bcb0f1664fb1751F00", "mixed case"),
	};

	private static readonly List<(string Data, string Info)> _invalidDataSource = new()
	{
		("C0DE DAAE05493BCB0F1664FB175 F00",   "with spaces"),
		("C0DE8DAAE05493BCB0F1664FB1751F",     "< valid length"),
		("C0DE8DAAE05493BCB0F1664FB1751F00FF", "> valid length"),
		("00000000000000000000000000000000",   "all zeros"),
		("12345678901234567890123456789012",   "all numbers"),
		("!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|",   "all symbols"),
		("STELIOKONTOSCANTC0DECLIFTONMSAID",   "non-hex chars"),
		("C0DE8DAAE05493BCB0F1664FB1751F0",    "not a multiple of 2"),
		("",                                   "empty"),
	};

	/// <remarks>
	/// The ECD is invalidated by flipping one of the 22 bits designated for Error Correction and Detection within the CPUKey string.
	/// An invalid Hamming Weight is created by flipping one of the remaining 106 bits to ensure a popcount other than 53.
	/// </remarks>
	/// <seealso cref="CPUKey.ValidateHammingWeight"/>
	/// <seealso cref="CPUKey.ComputeECD"/>
	private static readonly List<(string Data, bool ExpectedHammingWeight, bool ExpectedECD, string Info)> _exceptionDataSource = new()
	{
		("C0DE8DAAE05493BCB0F1664FB1751F00", ExpectedHammingWeight: true,  ExpectedECD: true,  "Hamming Weight: valid, ECD: valid"),
		("C0DE8DAAE05493BCB0F1664FB1751F10", ExpectedHammingWeight: true,  ExpectedECD: false, "Hamming Weight: valid, ECD: invalid"),
		("C1DE8DAAE05493BCB0F1664FB1751F10", ExpectedHammingWeight: false, ExpectedECD: false, "Hamming Weight: invalid, ECD: invalid"),
		("C1DE8DAAE05493BCB0F1664FB1751F00", ExpectedHammingWeight: false, ExpectedECD: true,  "Hamming Weight: invalid, ECD: valid"),
	};

	private static void InvalidateHammingWeight(Span<byte> span) => span[1] ^= 0x01;
	private static void InvalidateECD(Span<byte> span) => span[^1] ^= 0x01;

	public static IEnumerable<object[]> ValidDataGenerator(Type type)
		=> from x in _validDataSource
		   select new object[] { type switch {
			   Type t when t == typeof(string) => x.Data,
			   Type t when t == typeof(byte[]) => Convert.FromHexString(x.Data),
			   _ => throw new NotImplementedException() }, x.Info };

	public static IEnumerable<object[]> InvalidDataGenerator(Type type)
		=> from x in _invalidDataSource
		   where type != typeof(byte[]) || IsHexString(x.Data)
		   select new object[] { type switch {
			   Type t when t == typeof(string) => x.Data,
			   Type t when t == typeof(byte[]) => Convert.FromHexString(x.Data),
			   _ => throw new NotImplementedException() }, x.Info };

	public static IEnumerable<object[]> ExceptionDataGenerator(Type type)
		=> from x in _exceptionDataSource
		   select new object[] { type switch {
			   Type t when t == typeof(string) => x.Data,
			   Type t when t == typeof(byte[]) => Convert.FromHexString(x.Data),
			   _ => throw new NotImplementedException() }, x.ExpectedHammingWeight, x.ExpectedECD, x.Info };

	public static IEnumerable<object[]> MalformedDataGenerator(Type type)
	{
		if (type == typeof(byte[]))
		{
			yield return new object[] { Array.Empty<byte>(), "empty array" };
			yield return new object[] { Enumerable.Repeat(0x0, 16).Select(i => (byte)i).ToArray(), "all zeroes" };
			yield return new object[] { Enumerable.Repeat(0x1, 15).Select(i => (byte)i).ToArray(), "too short" };
			yield return new object[] { Enumerable.Repeat(0x1, 17).Select(i => (byte)i).ToArray(), "too long" };
		}
		else if (type == typeof(string))
		{
			yield return new object[] { String.Empty, "empty string" };
			yield return new object[] { new string('Z', 32), "non-hex characters" };
			yield return new object[] { new string('0', 32), "all zeroes" };
			yield return new object[] { new string('1', 31), "too short" };
			yield return new object[] { new string('1', 33), "too long" };
		}
	}

	// TODO: Distinguish between malformed (format) and invalid (content) data

	#endregion

	#region Creation Tests

	[Fact, Trait("Category", "Constructor")]
	public void DefaultConstructor_ShouldCreateEmptyCPUKey()
	{
		var cpukey = new CPUKey();
		cpukey.ShouldNotBeNull();
		cpukey.ShouldBe(CPUKey.Empty);
		cpukey.ShouldNotBeSameAs(CPUKey.Empty);
		cpukey.IsValid().ShouldBeFalse();
	}

	[Fact, Trait("Category", "Constructor")]
	public void CopyConstructor_ShouldCreateIdenticalCPUKey()
	{
		var cpukey = new CPUKey("C0DE8DAAE05493BCB0F1664FB1751F00");
		var copy = new CPUKey(cpukey);
		copy.ShouldNotBeNull();
		copy.ShouldBe(cpukey);
		copy.ShouldNotBeSameAs(cpukey);
	}

	[Fact, Trait("Category", "Constructor")]
	public void CopyConstructor_ShouldThrowWhenNull()
	{
		Should.Throw<ArgumentNullException>(() => new CPUKey(default(CPUKey)));
	}

	[Fact, Trait("Category", "Constructor")]
	public void Constructor_ByteArray_ShouldCreateValidCPUKey()
	{
		Byte[] array = new byte[] { 0xC0, 0xDE, 0x8D, 0xAA, 0xE0, 0x54, 0x93, 0xBC, 0xB0, 0xF1, 0x66, 0x4F, 0xB1, 0x75, 0x1F, 0x00 };
		var cpukey = new CPUKey(array);
		cpukey.ShouldNotBeNull();
		cpukey.IsValid().ShouldBeTrue();
		cpukey.ToArray().ShouldBe(array);
	}

	[Fact, Trait("Category", "Constructor")]
	public void Constructor_String_ShouldCreateValidCPUKey()
	{
		String str = "C0DE8DAAE05493BCB0F1664FB1751F00";
		var cpukey = new CPUKey(str);
		cpukey.ShouldNotBeNull();
		cpukey.IsValid().ShouldBeTrue();
		cpukey.ToString().ShouldBe(str);
	}

	[Fact, Trait("Category", "Constructor")]
	public void Constructor_ByteSpan_ShouldCreateValidCPUKey()
	{
		ReadOnlySpan<byte> span = stackalloc byte[] { 0xC0, 0xDE, 0x8D, 0xAA, 0xE0, 0x54, 0x93, 0xBC, 0xB0, 0xF1, 0x66, 0x4F, 0xB1, 0x75, 0x1F, 0x00 };
		var cpukey = new CPUKey(span);
		cpukey.ShouldNotBeNull();
		cpukey.IsValid().ShouldBeTrue();
		cpukey.AsSpan().SequenceEqual(span).ShouldBeTrue();
	}

	[Fact, Trait("Category", "Constructor")]
	public void Constructor_CharSpan_ShouldCreateValidCPUKey()
	{
		ReadOnlySpan<char> span = stackalloc char[] { 'C', '0', 'D', 'E', '8', 'D', 'A', 'A', 'E', '0', '5', '4', '9', '3', 'B', 'C', 'B', '0', 'F', '1', '6', '6', '4', 'F', 'B', '1', '7', '5', '1', 'F', '0', '0' };
		var cpukey = new CPUKey(span);
		cpukey.ShouldNotBeNull();
		cpukey.IsValid().ShouldBeTrue();
		cpukey.ToString().AsSpan().SequenceEqual(span).ShouldBeTrue();
	}

	[Fact, Trait("Category", "Constructor")]
	public void Constructor_ShouldThrowOnEmptySpan()
	{
		Should.Throw<ArgumentException>(() => new CPUKey(ReadOnlySpan<byte>.Empty));
		Should.Throw<ArgumentException>(() => new CPUKey(ReadOnlySpan<char>.Empty));
	}

	[Fact, Trait("Category", "Constructor")]
	public void Constructor_ShouldThrowOnLessThanValidLength()
	{
		// Less than 16 bytes (32 hex chars) is invalid
		Should.Throw<ArgumentOutOfRangeException>(() => new CPUKey(new byte[15]));
		Should.Throw<ArgumentOutOfRangeException>(() => new CPUKey(new char[31]));
	}

	[Fact, Trait("Category", "Constructor")]
	public void Constructor_ShouldThrowOnGreaterThanValidLength()
	{
		// More than 16 bytes (32 hex chars) is invalid
		Should.Throw<ArgumentOutOfRangeException>(() => new CPUKey(new byte[17]));
		Should.Throw<ArgumentOutOfRangeException>(() => new CPUKey(new char[33]));
	}

	[Fact, Trait("Category", "Constructor")]
	public void Constructor_ShouldThrowOnAllZeroes()
	{
		Should.Throw<FormatException>(() => new CPUKey(Enumerable.Repeat<byte>(0x00, 16).ToArray()));
		Should.Throw<FormatException>(() => new CPUKey(Enumerable.Repeat<char>('0', 32).ToArray()));
	}

	[Fact, Trait("Category", "Constructor")]
	public void Constructor_ShouldThrowOnNonHexChars()
	{
		Should.Throw<FormatException>(() => new CPUKey(Enumerable.Range(0, 32).Select(_ => (char)Random.Shared.Next('G', 'Z' + 1)).ToArray()));
	}

	[Fact, Trait("Category", "Factory Method")]
	public void CreateRandom_ShouldReturnValidCPUKey()
	{
		var cpukey = CPUKey.CreateRandom();
		cpukey.ShouldNotBeNull();
		cpukey.ShouldNotBe(CPUKey.Empty);
		cpukey.IsValid().ShouldBeTrue();
	}

	[Fact, Trait("Category", "Factory Method")]
	public void CreateRandom_ShouldReturnRandomCPUKey()
	{
		var cpukeys = Enumerable.Range(0, 100).Select(_ => CPUKey.CreateRandom()).ToList();
		cpukeys.ShouldAllBe(key => cpukeys.Count(k => k == key) == 1);
	}

	[Theory, Trait("Category", "Parse")]
	[MemberData(nameof(ValidDataGenerator), typeof(byte[]))]
	public void Parse_Bytes_ShouldReturnValidCPUKey(byte[] data, string info)
	{
		var cpukey = Should.NotThrow(() => CPUKey.Parse(data));
		cpukey.ShouldNotBeNull();
		cpukey.IsValid().ShouldBeTrue(info);
		cpukey.ToArray().ShouldBe(data);
	}

	[Theory, Trait("Category", "Parse")]
	[MemberData(nameof(ValidDataGenerator), typeof(string))]
	public void Parse_String_ShouldReturnValidCPUKey(string data, string info)
	{
		var cpukey = Should.NotThrow(() => CPUKey.Parse(data));
		cpukey.ShouldNotBeNull();
		cpukey.IsValid().ShouldBeTrue(info);
		cpukey.ToString().ShouldBe(data.ToUpper());
		String.Equals(cpukey.ToString(), data, StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
	}

	[Theory, Trait("Category", "Parse")]
	[MemberData(nameof(InvalidDataGenerator), typeof(byte[]))]
	public void Parse_Bytes_ShouldThrowOnInvalidInput(byte[] data, string info)
	{
		Should.Throw<Exception>(() => CPUKey.Parse(data)).Message.ShouldNotBeNull(info);
	}

	[Theory, Trait("Category", "Parse")]
	[MemberData(nameof(InvalidDataGenerator), typeof(string))]
	public void Parse_String_ShouldThrowOnInvalidInput(string data, string info)
	{
		Should.Throw<Exception>(() => CPUKey.Parse(data)).Message.ShouldNotBeNull(info);
	}

	[Theory, Trait("Category", "TryParse")]
	[MemberData(nameof(ValidDataGenerator), typeof(byte[]))]
	public void TryParse_Bytes_ShouldReturnTrueAndValidCPUKey(byte[] data, string info)
	{
		var result = CPUKey.TryParse(data, out var cpukey);
		result.ShouldBeTrue();
		cpukey.ShouldNotBeNull();
		cpukey.ShouldNotBe(CPUKey.Empty);
		cpukey.IsValid().ShouldBeTrue(info);
		cpukey.ToArray().ShouldBe(data);
	}

	[Theory, Trait("Category", "TryParse")]
	[MemberData(nameof(ValidDataGenerator), typeof(string))]
	public void TryParse_String_ShouldReturnTrueAndValidCPUKey(string data, string info)
	{
		var result = CPUKey.TryParse(data, out var cpukey);
		result.ShouldBeTrue();
		cpukey.ShouldNotBeNull();
		cpukey.ShouldNotBe(CPUKey.Empty);
		cpukey.IsValid().ShouldBeTrue(info);
		cpukey.ToString().ShouldBe(data.ToUpper());
		String.Equals(cpukey.ToString(), data, StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
	}

	[Theory, Trait("Category", "TryParse")]
	[MemberData(nameof(MalformedDataGenerator), typeof(byte[]))]
	public void TryParse_Bytes_ShouldReturnFalseAndNullCPUKeyOnMalformedInput(byte[] data, string info)
	{
		var result = CPUKey.TryParse(data, out var cpukey);
		result.ShouldBeFalse();
		cpukey.ShouldBeNull(info);
	}

	[Theory, Trait("Category", "TryParse")]
	[MemberData(nameof(MalformedDataGenerator), typeof(string))]
	public void TryParse_String_ShouldReturnFalseAndNullCPUKeyOnMalformedInput(string data, string info)
	{
		var result = CPUKey.TryParse(data, out var cpukey);
		result.ShouldBeFalse();
		cpukey.ShouldBeNull(info);
	}

	[Theory, Trait("Category", "TryParse")]
	[MemberData(nameof(ExceptionDataGenerator), typeof(byte[]))]
	public void TryParse_Bytes_ShouldReturnFalseAndEmptyCPUKeyOnInvalidInput(byte[] data, bool expectedHammingWeight, bool expectedECD, string info)
	{
		CPUKey? cpukey = default;
		var result = Should.NotThrow(() => CPUKey.TryParse(data, out cpukey));
		cpukey.ShouldNotBeNull();

		if (!expectedHammingWeight || !expectedECD)
		{
			result.ShouldBeFalse();
			cpukey.ShouldBe(CPUKey.Empty);
			cpukey.IsValid().ShouldBeFalse();
		}
		else
		{
			result.ShouldBeTrue();
			cpukey.IsValid().ShouldBeTrue(info);
		}
	}

	[Theory, Trait("Category", "TryParse")]
	[MemberData(nameof(ExceptionDataGenerator), typeof(string))]
	public void TryParse_String_ShouldReturnFalseAndEmptyCPUKeyOnInvalidInput(string data, bool expectedHammingWeight, bool expectedECD, string info)
	{
		CPUKey? cpukey = default;
		var result = Should.NotThrow(() => CPUKey.TryParse(data, out cpukey));
		cpukey.ShouldNotBeNull();

		if (!expectedHammingWeight || !expectedECD)
		{
			result.ShouldBeFalse();
			cpukey.ShouldBe(CPUKey.Empty);
			cpukey.IsValid().ShouldBeFalse();
		}
		else
		{
			result.ShouldBeTrue();
			cpukey.IsValid().ShouldBeTrue(info);
		}
	}

	#endregion

	#region Validation Tests

	[Fact, Trait("Category", "Validation")]
	public void IsValid_ShouldReturnTrueForValidCPUKey()
	{
		new CPUKey("C0DE8DAAE05493BCB0F1664FB1751F00").IsValid().ShouldBeTrue();
	}

	[Fact, Trait("Category", "Validation")]
	public void IsValid_ShouldReturnFalseForEmptyCPUKey()
	{
		CPUKey.Empty.IsValid().ShouldBeFalse();
		new CPUKey().IsValid().ShouldBeFalse();
	}

	[Theory, Trait("Category", "Validation")]
	[MemberData(nameof(ExceptionDataGenerator), typeof(byte[]))]
	public void InvalidByteArrays_ShouldThrowCorrectExceptionType(byte[] data, bool expectedHammingWeight, bool expectedECD, string info)
	{
		if (!expectedHammingWeight || !expectedECD)
		{
			var exception = Should.Throw<CPUKeyException>(() => new CPUKey(data));
			exception.ShouldBeOfType(GetCPUKeyExceptionType(expectedHammingWeight, expectedECD));
		}
		else
		{
			var cpukey = Should.NotThrow(() => new CPUKey(data));
			cpukey.IsValid().ShouldBeTrue(info);
		}
	}

	[Theory, Trait("Category", "Validation")]
	[MemberData(nameof(ExceptionDataGenerator), typeof(string))]
	public void InvalidStrings_ShouldThrowCorrectExceptionType(string data, bool expectedHammingWeight, bool expectedECD, string info)
	{
		if (!expectedHammingWeight || !expectedECD)
		{
			var exception = Should.Throw<CPUKeyException>(() => new CPUKey(data));
			exception.ShouldBeOfType(GetCPUKeyExceptionType(expectedHammingWeight, expectedECD));
		}
		else
		{
			var cpukey = Should.NotThrow(() => new CPUKey(data));
			cpukey.IsValid().ShouldBeTrue(info);
		}
	}

	private static Type GetCPUKeyExceptionType(bool expectedHammingWeight, bool expectedECD) => (expectedHammingWeight, expectedECD) switch
	{
		(false, _) => typeof(CPUKeyHammingWeightException),
		(_, false) => typeof(CPUKeyECDException),
		_ => typeof(CPUKeyException)
	};

	#endregion

	#region Equality & Comparison Tests

	[Fact, Trait("Category", "Equality")]
	public void Equals_WithNullObject_ShouldReturnFalse()
	{
		CPUKey? nullcpukey = default;
		CPUKey.Empty.Equals(nullcpukey).ShouldBeFalse();
		CPUKey.Empty.Equals(null as object).ShouldBeFalse();
		new CPUKey("C0DE8DAAE05493BCB0F1664FB1751F00").Equals(null as object).ShouldBeFalse();
	}

	[Fact, Trait("Category", "Equality")]
	public void Equals_WithItself_ShouldReturnTrue()
	{
		var cpukey = new CPUKey("C0DE8DAAE05493BCB0F1664FB1751F00");
		var empty = new CPUKey();
		cpukey.Equals(cpukey).ShouldBeTrue();
		empty.Equals(empty).ShouldBeTrue();
	}

	[Fact, Trait("Category", "Equality")]
	public void Equals_WithIdenticalCPUKey_ShouldReturnTrue()
	{
		var hexstring = "C0DE8DAAE05493BCB0F1664FB1751F00";
		var cpukey = new CPUKey(hexstring);
		var copy = new CPUKey(hexstring);
		cpukey.ToString().ShouldBe(copy.ToString());
		cpukey.Equals(copy).ShouldBeTrue();
	}

	[Fact, Trait("Category", "Equality")]
	public void Equals_WithDifferentCPUKey_ShouldReturnFalse()
	{
		var cpukey1 = CPUKey.CreateRandom();
		var cpukey2 = CPUKey.CreateRandom();
		cpukey1.ToString().ShouldNotBe(cpukey2.ToString());
		cpukey1.Equals(cpukey2).ShouldBeFalse();
	}

	[Fact, Trait("Category", "Equality")]
	public void Equals_WithByteArrayHavingSameData_ShouldReturnTrue()
	{
		var array1 = new byte[] { 0xC0, 0xDE, 0x8D, 0xAA, 0xE0, 0x54, 0x93, 0xBC, 0xB0, 0xF1, 0x66, 0x4F, 0xB1, 0x75, 0x1F, 0x00 };
		var array2 = new byte[] { 0xC0, 0xDE, 0x8D, 0xAA, 0xE0, 0x54, 0x93, 0xBC, 0xB0, 0xF1, 0x66, 0x4F, 0xB1, 0x75, 0x1F, 0x00 };
		array1.ShouldBe(array2);
		new CPUKey(array1).Equals(array2).ShouldBeTrue();
	}

	[Fact, Trait("Category", "Equality")]
	public void Equals_WithByteArrayHavingDifferentData_ShouldReturnFalse()
	{
		var array1 = new byte[] { 0xC0, 0xDE, 0x8D, 0xAA, 0xE0, 0x54, 0x93, 0xBC, 0xB0, 0xF1, 0x66, 0x4F, 0xB1, 0x75, 0x1F, 0x00 };
		var array2 = new byte[] { 0xC0, 0xDE, 0x8D, 0xAA, 0xE0, 0x54, 0x93, 0xBC, 0xB0, 0xF1, 0x66, 0x4F, 0xB1, 0x75, 0x1F, 0x01 };
		array1.ShouldNotBe(array2);
		new CPUKey(array1).Equals(array2).ShouldBeFalse();
	}

	[Fact, Trait("Category", "Equality")]
	public void Equals_WithStringHavingSameData_ShouldReturnTrue()
	{
		var hexstring = "C0DE8DAAE05493BCB0F1664FB1751F00";
		var cpukey = new CPUKey(hexstring);
		cpukey.ToString().ShouldBe(hexstring);
		cpukey.Equals(hexstring).ShouldBeTrue();
	}

	[Fact, Trait("Category", "Equality")]
	public void Equals_WithStringHavingSameDataDifferentCase_ShouldReturnTrue()
	{
		var hexupper = "C0DE8DAAE05493BCB0F1664FB1751F00";
		var hexlower = hexupper.ToLowerInvariant();
		new CPUKey(hexupper).Equals(hexlower).ShouldBeTrue();
	}

	[Fact, Trait("Category", "Equality")]
	public void Equals_WithStringHavingDifferentData_ShouldReturnFalse()
	{
		var hexstring1 = "C0DE8DAAE05493BCB0F1664FB1751F00";
		var hexstring2 = "C0DE8DAAE05493BCB0F1664FB1751F0F";
		hexstring1.ShouldNotBe(hexstring2);
		new CPUKey(hexstring1).Equals(hexstring2).ShouldBeFalse();
	}

	[Fact, Trait("Category", "Equality")]
	public void Equals_WithNonCPUKeyObject_ShouldReturnFalse()
	{
		var cpukey = new CPUKey("C0DE8DAAE05493BCB0F1664FB1751F00");
		cpukey.Equals(new object()).ShouldBeFalse();
		cpukey.Equals(new byte[16]).ShouldBeFalse();
		cpukey.Equals(new char[32]).ShouldBeFalse();
		cpukey.Equals(new List<byte>()).ShouldBeFalse();
		cpukey.Equals(new List<char>()).ShouldBeFalse();
		cpukey.Equals(new List<object>()).ShouldBeFalse();
		cpukey.Equals(new List<string>()).ShouldBeFalse();
		cpukey.Equals(new List<CPUKey>()).ShouldBeFalse();
		cpukey.Equals(new object[] { }).ShouldBeFalse();
	}

	[Fact, Trait("Category", "IComparable")]
	public void CompareTo_WithNull_ShouldReturnPositive()
	{
		var cpukey = new CPUKey("C0DE8DAAE05493BCB0F1664FB1751F00");
		cpukey.CompareTo(null).ShouldBePositive();
	}

	[Fact, Trait("Category", "IComparable")]
	public void CompareTo_WithItself_ShouldReturnZero()
	{
		var cpukey = new CPUKey("C0DE8DAAE05493BCB0F1664FB1751F00");
		cpukey.CompareTo(cpukey).ShouldBe(0);
	}

	[Fact, Trait("Category", "IComparable")]
	public void CompareTo_WithSmallerCPUKey_ShouldReturnPositive()
	{
		var cpukey1 = new CPUKey("C0DE8DAAE05493BCB0F1664FB1751F00");
		var cpukey2 = new CPUKey("C0B33D79A74BE3832B0E6172AC491F00");
		cpukey1.CompareTo(cpukey2).ShouldBePositive();
	}

	[Fact, Trait("Category", "IComparable")]
	public void CompareTo_WithLargerCPUKey_ShouldReturnNegative()
	{
		var cpukey1 = new CPUKey("C0B33D79A74BE3832B0E6172AC491F00");
		var cpukey2 = new CPUKey("C0DE8DAAE05493BCB0F1664FB1751F00");
		cpukey1.CompareTo(cpukey2).ShouldBeNegative();
	}

	[Fact, Trait("Category", "IComparable")]
	public void CompareTo_WithEqualCPUKey_ShouldReturnZero()
	{
		var cpukey1 = new CPUKey("C0DE8DAAE05493BCB0F1664FB1751F00");
		var cpukey2 = new CPUKey("C0DE8DAAE05493BCB0F1664FB1751F00");
		cpukey1.CompareTo(cpukey2).ShouldBe(0);
	}

	#endregion

	#region Operators Tests

	[Fact, Trait("Category", "Operators")]
	public void EqualityOperator_WithEqualCPUKeys_ShouldReturnTrue()
	{
		var cpukey1 = new CPUKey("C0DE8DAAE05493BCB0F1664FB1751F00");
		var cpukey2 = new CPUKey("C0DE8DAAE05493BCB0F1664FB1751F00");
		(cpukey1 == cpukey2).ShouldBeTrue();
	}

	[Fact, Trait("Category", "Operators")]
	public void InequalityOperator_WithDifferentCPUKeys_ShouldReturnTrue()
	{
		var cpukey1 = new CPUKey("C0DE8DAAE05493BCB0F1664FB1751F00");
		var cpukey2 = new CPUKey("C0B33D79A74BE3832B0E6172AC491F00");
		(cpukey1 != cpukey2).ShouldBeTrue();
	}

	[Fact, Trait("Category", "Operators")]
	public void EqualityOperator_WithEqualByteArray_ShouldReturnTrue()
	{
		var cpukey = new CPUKey("C0DE8DAAE05493BCB0F1664FB1751F00");
		var array = new byte[] { 0xC0, 0xDE, 0x8D, 0xAA, 0xE0, 0x54, 0x93, 0xBC, 0xB0, 0xF1, 0x66, 0x4F, 0xB1, 0x75, 0x1F, 0x00 };
		(cpukey == array).ShouldBeTrue();
	}

	[Fact, Trait("Category", "Operators")]
	public void InequalityOperator_WithDifferentByteArray_ShouldReturnTrue()
	{
		var cpukey = new CPUKey("C0DE8DAAE05493BCB0F1664FB1751F00");
		var array = new byte[] { 0xC0, 0xB3, 0x3D, 0x79, 0xA7, 0x4B, 0xE3, 0x83, 0x2B, 0x0E, 0x61, 0x72, 0xAC, 0x49, 0x1F, 0x00 };
		(cpukey != array).ShouldBeTrue();
	}

	[Fact, Trait("Category", "Operators")]
	public void EqualityOperator_WithEqualString_ShouldReturnTrue()
	{
		var cpukey = new CPUKey("C0DE8DAAE05493BCB0F1664FB1751F00");
		var str = "C0DE8DAAE05493BCB0F1664FB1751F00";
		(cpukey == str).ShouldBeTrue();
	}

	[Fact, Trait("Category", "Operators")]
	public void InequalityOperator_WithDifferentString_ShouldReturnTrue()
	{
		var cpukey = new CPUKey("C0DE8DAAE05493BCB0F1664FB1751F00");
		var str = "C0B33D79A74BE3832B0E6172AC491F00";
		(cpukey != str).ShouldBeTrue();
	}

	[Fact, Trait("Category", "Operators")]
	public void LessThanOperator_WithSmallerCPUKey_ShouldReturnTrue()
	{
		var cpukey1 = new CPUKey("C0B33D79A74BE3832B0E6172AC491F00");
		var cpukey2 = new CPUKey("C0DE8DAAE05493BCB0F1664FB1751F00");
		(cpukey1 < cpukey2).ShouldBeTrue();
	}

	[Fact, Trait("Category", "Operators")]
	public void LessThanOrEqualToOperator_WithSmallerOrEqualCPUKey_ShouldReturnTrue()
	{
		var cpukey1 = new CPUKey("C0B33D79A74BE3832B0E6172AC491F00");
		var cpukey2 = new CPUKey("C0DE8DAAE05493BCB0F1664FB1751F00");
		var cpukey3 = new CPUKey("C0DE8DAAE05493BCB0F1664FB1751F00");
		(cpukey1 <= cpukey2).ShouldBeTrue();
		(cpukey2 <= cpukey3).ShouldBeTrue();
	}

	[Fact, Trait("Category", "Operators")]
	public void GreaterThanOperator_WithLargerCPUKey_ShouldReturnTrue()
	{
		var cpukey1 = new CPUKey("C0DE8DAAE05493BCB0F1664FB1751F00");
		var cpukey2 = new CPUKey("C0B33D79A74BE3832B0E6172AC491F00");
		(cpukey1 > cpukey2).ShouldBeTrue();
	}

	[Fact, Trait("Category", "Operators")]
	public void GreaterThanOrEqualToOperator_WithLargerOrEqualCPUKey_ShouldReturnTrue()
	{
		var cpukey1 = new CPUKey("C0DE8DAAE05493BCB0F1664FB1751F00");
		var cpukey2 = new CPUKey("C0B33D79A74BE3832B0E6172AC491F00");
		var cpukey3 = new CPUKey("C0B33D79A74BE3832B0E6172AC491F00");
		(cpukey1 >= cpukey2).ShouldBeTrue();
		(cpukey2 >= cpukey3).ShouldBeTrue();
	}

	#endregion

	#region Scratch

	protected static CPUKey GenValidCPUKey
	{
		get
		{
			CPUKey cpukey;
			do { cpukey = CPUKey.CreateRandom(); }
			while (!cpukey.ToString()[..2].Equals("C0") || !cpukey.ToString()[^3..].Equals("F00"));
			return cpukey;
		}
	}

	#endregion
}

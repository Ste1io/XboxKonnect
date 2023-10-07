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
		("C0DE DAAE05493BCB0F1664FB175 F00",  "with spaces"),
		("C0DE8DAAE05493BCB0F1664FB1751F0",   "< valid length"),
		("C0DE8DAAE05493BCB0F1664FB1751F00F", "> valid length"),
		("00000000000000000000000000000000",  "all zeros"),
		("STELIOKONTOSCANTC0DECLIFTONMSAID",  "non-hex chars"),
		("12345678901234567890123456789012",  "all numbers"),
		("!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|",  "all symbols"),
		("",                                  "empty string"),
	};

	/// <remarks>
	/// The ECD is invalidated by flipping one of the 22 bits designated for Error Correction and Detection within the CPUKey string. An
	/// invalid Hamming Weight is created by flipping one of the remaining 106 bits to ensure a popcount other than 53.
	/// </remarks>
	/// <seealso cref="CPUKey.ValidateHammingWeight"/>
	/// <seealso cref="CPUKey.ComputeECD"/>
	private static readonly List<(string Data, bool ExpectedHammingWeight, bool ExpectedECD, string Info)> _exceptionDataSource = new()
	{
		("C0DE8DAAE05493BCB0F1664FB1751F00", ExpectedHammingWeight: true,  ExpectedECD: true,  "valid Hamming Weight and ECD"),
		("C0DE8DAAE05493BCB0F1664FB1751F10", ExpectedHammingWeight: true,  ExpectedECD: false, "valid Hamming Weight, invalid ECD"),
		("C1DE8DAAE05493BCB0F1664FB1751F00", ExpectedHammingWeight: false, ExpectedECD: true,  "invalid Hamming Weight, valid ECD"),
		("C1DE8DAAE05493BCB0F1664FB1751F10", ExpectedHammingWeight: false, ExpectedECD: false, "invalid Hamming Weight and ECD"),
	};

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

	#endregion

	[Fact]
	[Trait("Category", "Constructor")]
	public void DefaultConstructor_ShouldCreateEmptyCPUKey()
	{
		var cpukey = new CPUKey();
		cpukey.ShouldNotBeNull();
		cpukey.ShouldBe(CPUKey.Empty);
		cpukey.ShouldNotBeSameAs(CPUKey.Empty);
		cpukey.IsValid().ShouldBeFalse();
	}

	[Fact]
	[Trait("Category", "Constructor")]
	public void CopyConstructor_ShouldCreateIdenticalCPUKey()
	{
		var cpukey = new CPUKey("C0DE8DAAE05493BCB0F1664FB1751F00");
		var copy = new CPUKey(cpukey);
		copy.ShouldNotBeNull();
		copy.ShouldBe(cpukey);
		copy.ShouldNotBeSameAs(cpukey);
	}

	[Fact]
	[Trait("Category", "Constructor")]
	public void CopyConstructor_ShouldThrowWhenNull()
	{
		Should.Throw<ArgumentNullException>(() => new CPUKey(default(CPUKey)));
	}

	[Fact]
	[Trait("Category", "Constructor")]
	public void Constructor_ByteArray_ShouldCreateValidCPUKey()
	{
		Byte[] array = new byte[] { 0xC0, 0xDE, 0x8D, 0xAA, 0xE0, 0x54, 0x93, 0xBC, 0xB0, 0xF1, 0x66, 0x4F, 0xB1, 0x75, 0x1F, 0x00 };
		var cpukey = new CPUKey(array);
		cpukey.ShouldNotBeNull();
		cpukey.IsValid().ShouldBeTrue();
		cpukey.ToArray().ShouldBe(array);
	}

	[Fact]
	[Trait("Category", "Constructor")]
	public void Constructor_String_ShouldCreateValidCPUKey()
	{
		String str = "C0DE8DAAE05493BCB0F1664FB1751F00";
		var cpukey = new CPUKey(str);
		cpukey.ShouldNotBeNull();
		cpukey.IsValid().ShouldBeTrue();
		cpukey.ToString().ShouldBe(str);
	}

	[Fact]
	[Trait("Category", "Constructor")]
	public void Constructor_ByteSpan_ShouldCreateValidCPUKey()
	{
		ReadOnlySpan<byte> span = stackalloc byte[] { 0xC0, 0xDE, 0x8D, 0xAA, 0xE0, 0x54, 0x93, 0xBC, 0xB0, 0xF1, 0x66, 0x4F, 0xB1, 0x75, 0x1F, 0x00 };
		var cpukey = new CPUKey(span);
		cpukey.ShouldNotBeNull();
		cpukey.IsValid().ShouldBeTrue();
		cpukey.AsSpan().SequenceEqual(span).ShouldBeTrue();
	}

	[Fact]
	[Trait("Category", "Constructor")]
	public void Constructor_CharSpan_ShouldCreateValidCPUKey()
	{
		ReadOnlySpan<char> span = stackalloc char[] { 'C', '0', 'D', 'E', '8', 'D', 'A', 'A', 'E', '0', '5', '4', '9', '3', 'B', 'C', 'B', '0', 'F', '1', '6', '6', '4', 'F', 'B', '1', '7', '5', '1', 'F', '0', '0' };
		var cpukey = new CPUKey(span);
		cpukey.ShouldNotBeNull();
		cpukey.IsValid().ShouldBeTrue();
		cpukey.ToString().AsSpan().SequenceEqual(span).ShouldBeTrue();
	}

	[Fact]
	[Trait("Category", "Constructor")]
	public void Constructor_ShouldThrowOnEmptySpan()
	{
		Should.Throw<ArgumentException>(() => new CPUKey(ReadOnlySpan<byte>.Empty));
		Should.Throw<ArgumentException>(() => new CPUKey(ReadOnlySpan<char>.Empty));
	}

	[Fact]
	[Trait("Category", "Constructor")]
	public void Constructor_ShouldThrowOnLessThanValidLength()
	{
		// Less than 16 bytes (32 hex chars) is invalid
		Should.Throw<ArgumentOutOfRangeException>(() => new CPUKey(new byte[15]));
		Should.Throw<ArgumentOutOfRangeException>(() => new CPUKey(new char[31]));
	}

	[Fact]
	[Trait("Category", "Constructor")]
	public void Constructor_ShouldThrowOnGreaterThanValidLength()
	{
		// More than 16 bytes (32 hex chars) is invalid
		Should.Throw<ArgumentOutOfRangeException>(() => new CPUKey(new byte[17]));
		Should.Throw<ArgumentOutOfRangeException>(() => new CPUKey(new char[33]));
	}

	[Fact]
	[Trait("Category", "Constructor")]
	public void Constructor_ShouldThrowOnAllZeroes()
	{
		Should.Throw<FormatException>(() => new CPUKey(Enumerable.Repeat<byte>(0x00, 16).ToArray()));
		Should.Throw<FormatException>(() => new CPUKey(Enumerable.Repeat<char>('0', 32).ToArray()));
	}

	[Fact]
	[Trait("Category", "Constructor")]
	public void Constructor_ShouldThrowOnNonHexChars()
	{
		Should.Throw<FormatException>(() => new CPUKey(Enumerable.Range(0, 32).Select(_ => (char)Random.Shared.Next('G', 'Z' + 1)).ToArray()));
	}

	[Fact]
	[Trait("Category", "Factory Method")]
	public void CreateRandom_ShouldReturnValidCPUKey()
	{
		var cpukey = CPUKey.CreateRandom();
		cpukey.ShouldNotBeNull();
		cpukey.ShouldNotBe(CPUKey.Empty);
		cpukey.IsValid().ShouldBeTrue();
	}

	[Fact]
	[Trait("Category", "Factory Method")]
	public void CreateRandom_ShouldReturnRandomCPUKey()
	{
		var cpukeys = Enumerable.Range(0, 100).Select(_ => CPUKey.CreateRandom()).ToList();
		cpukeys.ShouldAllBe(key => cpukeys.Count(k => k == key) == 1);
	}

	[Theory]
	[Trait("Category", "Parse")]
	[MemberData(nameof(ValidDataGenerator), typeof(byte[]))]
	public void Parse_Bytes_ShouldReturnValidCPUKey(byte[] data, string info)
	{
		var cpukey = CPUKey.Parse(data);
		cpukey.ShouldNotBeNull();
		cpukey.IsValid().ShouldBeTrue(info);
		cpukey.ToArray().ShouldBe(data);
	}

	[Theory]
	[Trait("Category", "Parse")]
	[MemberData(nameof(ValidDataGenerator), typeof(string))]
	public void Parse_String_ShouldReturnValidCPUKey(string data, string info)
	{
		var cpukey = CPUKey.Parse(data);
		cpukey.ShouldNotBeNull();
		cpukey.IsValid().ShouldBeTrue(info);
		cpukey.ToString().ShouldBe(data.ToUpper());
		String.Equals(cpukey.ToString(), data, StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
	}

	[Theory]
	[Trait("Category", "Parse")]
	[MemberData(nameof(InvalidDataGenerator), typeof(byte[]))]
	public void Parse_Bytes_ShouldReturnNullOnInvalidInput(byte[] data, string info)
	{
		var cpukey = CPUKey.Parse(data);
		cpukey.ShouldBeNull(info);
	}

	[Theory]
	[Trait("Category", "Parse")]
	[MemberData(nameof(InvalidDataGenerator), typeof(string))]
	public void Parse_String_ShouldReturnNullOnInvalidInput(string data, string info)
	{
		var cpukey = CPUKey.Parse(data);
		cpukey.ShouldBeNull(info);
	}

	[Theory]
	[Trait("Category", "Parse")]
	[MemberData(nameof(ExceptionDataGenerator), typeof(byte[]))]
	public void Parse_Bytes_ShouldNotThrow(byte[] data, bool expectedHammingWeight, bool expectedECD, string info)
	{
		var cpukey = Should.NotThrow(() => CPUKey.Parse(data));
		if (!expectedHammingWeight || !expectedECD)
			cpukey.ShouldBeNull(info);
	}

	[Theory]
	[Trait("Category", "Parse")]
	[MemberData(nameof(ExceptionDataGenerator), typeof(string))]
	public void Parse_String_ShouldNotThrow(string data, bool expectedHammingWeight, bool expectedECD, string info)
	{
		var cpukey = Should.NotThrow(() => CPUKey.Parse(data));
		if (!expectedHammingWeight || !expectedECD)
			cpukey.ShouldBeNull(info);
	}

	[Theory]
	[Trait("Category", "TryParse")]
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

	[Theory]
	[Trait("Category", "TryParse")]
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

	[Theory]
	[Trait("Category", "TryParse")]
	[MemberData(nameof(InvalidDataGenerator), typeof(byte[]))]
	public void TryParse_Bytes_ShouldReturnFalseAndEmptyCPUKey(byte[] data, string info)
	{
		var result = CPUKey.TryParse(data, out var cpukey);
		result.ShouldBeFalse();
		cpukey.ShouldNotBeNull();
		cpukey.ShouldBe(CPUKey.Empty);
		cpukey.IsValid().ShouldBeFalse(info);
	}

	[Theory]
	[Trait("Category", "TryParse")]
	[MemberData(nameof(InvalidDataGenerator), typeof(string))]
	public void TryParse_String_ShouldReturnFalseAndEmptyCPUKey(string data, string info)
	{
		var result = CPUKey.TryParse(data, out var cpukey);
		result.ShouldBeFalse();
		cpukey.ShouldNotBeNull();
		cpukey.ShouldBe(CPUKey.Empty);
		cpukey.IsValid().ShouldBeFalse(info);
	}

	[Fact]
	[Trait("Category", "Validation")]
	public void IsValid_ShouldReturnTrueForValidCPUKey()
	{
		new CPUKey("C0DE8DAAE05493BCB0F1664FB1751F00").IsValid().ShouldBeTrue();
	}

	[Fact]
	[Trait("Category", "Validation")]
	public void IsValid_ShouldReturnFalseForEmptyCPUKey()
	{
		CPUKey.Empty.IsValid().ShouldBeFalse();
		new CPUKey().IsValid().ShouldBeFalse();
	}

	[Theory]
	[Trait("Category", "Validation")]
	[MemberData(nameof(ExceptionDataGenerator), typeof(byte[]))]
	public void InvalidByteArrays_ShouldThrowCorrectExceptionType(byte[] data, bool expectedHammingWeight, bool expectedECD, string info)
	{
		if (expectedHammingWeight && expectedECD)
		{
			var cpuKey = Should.NotThrow(() => new CPUKey(data));
			cpuKey.IsValid().ShouldBeTrue(info);
		}
		else
		{
			var exception = Should.Throw<CPUKeyException>(() => new CPUKey(data));
			exception.ShouldBeOfType(GetCPUKeyExceptionType(expectedHammingWeight, expectedECD));
		}
	}

	[Theory]
	[Trait("Category", "Validation")]
	[MemberData(nameof(ExceptionDataGenerator), typeof(string))]
	public void InvalidStrings_ShouldThrowCorrectExceptionType(string data, bool expectedHammingWeight, bool expectedECD, string info)
	{
		if (expectedHammingWeight && expectedECD)
		{
			var cpuKey = Should.NotThrow(() => new CPUKey(data));
			cpuKey.IsValid().ShouldBeTrue(info);
		}
		else
		{
			var exception = Should.Throw<CPUKeyException>(() => new CPUKey(data));
			exception.ShouldBeOfType(GetCPUKeyExceptionType(expectedHammingWeight, expectedECD));
		}
	}

	private static Type GetCPUKeyExceptionType(bool expectedHammingWeight, bool expectedECD) => (expectedHammingWeight, expectedECD) switch
	{
		(false, _) => typeof(CPUKeyHammingWeightException),
		(_, false) => typeof(CPUKeyECDException),
		_ => typeof(CPUKeyException)
	};

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

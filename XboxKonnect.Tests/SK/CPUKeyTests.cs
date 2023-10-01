using SK;

namespace XboxKonnect.Tests;

public class CPUKeyTests
{
	#region Test Data

	public record CPUKeyExceptionData(string HexString, bool ExpectedHammingWeight, bool ExpectedECD)
	{
		public static implicit operator object[](CPUKeyExceptionData data)
			=> new object[] { data.HexString, data.ExpectedHammingWeight, data.ExpectedECD };
	}

	/// <summary>
	/// Generates a set of test data strings for CPUKeyException tests. The ECD is invalidated by flipping one of the 22 bits designated for
	/// Error Correction and Detection within the CPUKey string. An invalid Hamming Weight is created by flipping one of the remaining 106
	/// bits to ensure a popcount other than 53.
	/// </summary>
	public static IEnumerable<object[]> CPUKeyExceptionTestData()
	{
		yield return new CPUKeyExceptionData(HexString: "C0DE8DAAE05493BCB0F1664FB1751F00", ExpectedHammingWeight: true, ExpectedECD: true);
		yield return new CPUKeyExceptionData(HexString: "C0DE8DAAE05493BCB0F1664FB1751F10", ExpectedHammingWeight: true, ExpectedECD: false);  // flip one bit inside our ecd mask (F00 -> F10)
		yield return new CPUKeyExceptionData(HexString: "C1DE8DAAE05493BCB0F1664FB1751F00", ExpectedHammingWeight: false, ExpectedECD: true);  // flip one bit outside our ecd mask (C0DE -> C1DE)
		yield return new CPUKeyExceptionData(HexString: "C1DE8DAAE05493BCB0F1664FB1751F10", ExpectedHammingWeight: false, ExpectedECD: false); // flip both bits (C0DE -> C1DE, F00 -> F10)
	}

	public static string ValidCPUKeyString => "C0DE8DAAE05493BCB0F1664FB1751F00";

	#endregion

	[Theory]
	[MemberData(nameof(CPUKeyExceptionTestData))]
	public void CPUKeyException_GivenInvalidCPUKeyString_ShouldThrowCorrectExceptionType(string hexString, bool expectedHammingWeight, bool expectedECD)
	{
		if (expectedHammingWeight && expectedECD)
		{
			var cpuKey = new CPUKey(hexString);
			cpuKey.IsValid().ShouldBeTrue();
		}
		else
		{
			var exception = Should.Throw<CPUKeyException>(() => new CPUKey(hexString));
			var expectedExceptionType = (expectedHammingWeight, expectedECD) switch
			{
				(false, _) => typeof(CPUKeyHammingWeightException),
				(_, false) => typeof(CPUKeyECDException),
				_ => typeof(CPUKeyException)
			};
			exception.ShouldBeOfType(expectedExceptionType);
		}
	}

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

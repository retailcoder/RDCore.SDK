using RDCore.SDK.Model.Values.Intrinsic;
using RDCore.SDK.Model.Values.Runtime;

namespace RDCore.Tests.Model.Values;

/// <summary>
/// The data of a <c>String</c> is its bytes (<strong>MS-VBAL §5.5.1.2.6</strong>'s "implementation-defined binary format used
/// to store <c>String</c> data"): two per character, little-endian, and - when there is an odd number of them - one byte left
/// over after the last whole character, which a <see cref="string"/> cannot hold and <see cref="VBRuntimeByteStringValue"/>
/// holds beside it.
/// </summary>
[TestClass]
[TestCategory("RD-VBAL §2.5 Runtime Values")]
public sealed class VBStringValueByteTests
{
    #region from bytes

    [TestMethod]
    public void FromBytes_AnOddCount_KeepsTheTrailingByte()
    {
        var sut = VBStringValue.FromBytes([0x41, 0x00, 0x42]);

        Assert.AreEqual("A", sut.Value);
        Assert.AreEqual(1, sut.Length);
        Assert.AreEqual((byte)0x42, sut.TrailingByte);
        Assert.AreEqual(3, sut.ByteLength);
        CollectionAssert.AreEqual(new byte[] { 0x41, 0x00, 0x42 }, sut.GetBytes());
    }

    [TestMethod]
    public void FromBytes_OneByte_HasNoCharacters_ButIsNotEmpty()
    {
        // what ChrB(65) is: LenB 1, Len 0.
        var sut = VBStringValue.FromBytes([0x41]);

        Assert.AreEqual(string.Empty, sut.Value);
        Assert.AreEqual(0, sut.Length);
        Assert.AreEqual(1, sut.ByteLength);
        Assert.IsFalse(sut.IsNullString);
    }

    [TestMethod]
    public void FromBytes_AnEvenCount_IsAnOrdinaryString()
    {
        // only an odd string is a VBRuntimeByteStringValue: nothing that never meets one sees anything new.
        var sut = VBStringValue.FromBytes([0x41, 0x00, 0x42, 0x00]);

        Assert.IsInstanceOfType<VBRuntimeValue<string>>(sut.RuntimeValue);
        Assert.AreEqual("AB", sut.Value);
        Assert.IsNull(sut.TrailingByte);
    }

    [TestMethod]
    public void FromBytes_NoBytes_IsAZeroLengthString()
    {
        var sut = VBStringValue.FromBytes([]);

        Assert.AreEqual(string.Empty, sut.Value);
        Assert.IsFalse(sut.IsNullString, "no bytes is a zero-length string, which has data; vbNullString has none");
    }

    [TestMethod]
    public void BoxedValue_OfAnOddString_IsItsWholeCharacters()
        // a reader that only knows the managed value sees the characters, never nothing.
        => Assert.AreEqual("A", VBStringValue.FromBytes([0x41, 0x00, 0x42]).RuntimeValue.BoxedValue);

    #endregion

    #region to bytes

    [TestMethod]
    public void GetBytes_ALoneSurrogate_RoundTrips()
    {
        // every pair of bytes is a code unit; Encoding.Unicode would have read these as U+FFFD, and written FD FF.
        var sut = VBStringValue.FromBytes([0x00, 0xD8]);

        Assert.AreEqual("\uD800", sut.Value);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0xD8 }, sut.GetBytes());
    }

    [TestMethod]
    public void GetBytes_OfTheNullString_IsEmpty()
    {
        Assert.IsEmpty(VBStringValue.VBNullString.GetBytes());
        Assert.AreEqual(0, VBStringValue.VBNullString.ByteLength);
    }

    [TestMethod]
    public void GetBytes_OfAString_IsTwoBytesPerCharacter_LittleEndian()
        => CollectionAssert.AreEqual(new byte[] { 0x41, 0x00, 0x41, 0x01 }, new VBStringValue("AŁ").GetBytes());

    #endregion

    #region slices of bytes

    // every part of an even string, a lone surrogate in it, and of an odd one: the String FromBytes makes of the same bytes of GetBytes.
    [TestMethod]
    public void SliceBytes_OfEveryPart_IsWhatFromBytesMakesOfThoseBytes()
    {
        foreach (var sut in new[] { new VBStringValue("AŁ\uD800z"), VBStringValue.FromBytes([0x41, 0x00, 0x41, 0x01, 0x42]), VBStringValue.FromBytes([0x43]) })
        {
            var bytes = sut.GetBytes();
            for (var index = 0; index <= bytes.Length; index++)
            {
                for (var count = 0; count <= bytes.Length - index; count++)
                {
                    var expected = VBStringValue.FromBytes(bytes.AsSpan(index, count));
                    var slice = sut.SliceBytes(index, count);
                    var at = $"{count} bytes from {index} of {bytes.Length}";
                    Assert.AreEqual(expected.Value, slice.Value, at);
                    Assert.AreEqual(expected.TrailingByte, slice.TrailingByte, at);
                    CollectionAssert.AreEqual(bytes[index..(index + count)], slice.GetBytes(), at);
                }
            }
        }
    }

    [TestMethod]
    [DataRow(-1, 1)]
    [DataRow(0, -1)]
    [DataRow(0, 6)]
    [DataRow(5, 1)]
    [DataRow(6, 0)]
    public void SliceBytes_OfBytesNotInTheData_Throws(int index, int count)
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => VBStringValue.FromBytes([0x41, 0x00, 0x42, 0x00, 0x43]).SliceBytes(index, count));

    // what MidB of a long String is: two bytes of it, and no copy of the rest.
    [TestMethod]
    public void SliceBytes_OfALongString_CopiesNoneOfTheRest()
    {
        var sut = new VBStringValue(new string('x', 10_000));
        _ = sut.SliceBytes(1, 2); // the first call pays for what it loads, once

        var before = GC.GetAllocatedBytesForCurrentThread();
        var slice = sut.SliceBytes(1, 2);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.AreEqual(2, slice.ByteLength);
        Assert.IsLessThan(1_000L, allocated, $"two bytes of a 20,000-byte String allocated {allocated:N0} bytes");
    }

    #endregion

    #region size

    [TestMethod]
    [DataRow(new byte[] { 0x41 }, 3)]
    [DataRow(new byte[] { 0x41, 0x00, 0x42 }, 5)]
    [DataRow(new byte[0], 2)]
    public void Size_IsTheByteCountPlusItsHeader(byte[] bytes, int expected)
        => Assert.AreEqual(expected, VBStringValue.FromBytes(bytes).Size);

    [TestMethod]
    public void Size_OfTheNullString_IsNothing()
        => Assert.AreEqual(0, VBStringValue.VBNullString.Size);

    [TestMethod]
    public void ByteLength_OfAFixedLengthString_IsTwoPerCharacter()
    {
        var sut = new VBFixedStringValue(4);

        Assert.AreEqual(8, sut.ByteLength);
        Assert.AreEqual(10, sut.Size);
    }

    #endregion

    #region concatenation

    [TestMethod]
    public void Concat_TwoOddStrings_PairTheirBytes()
    {
        // 41 & 42 is the one code unit 42 41.
        var sut = VBStringValue.Concat(VBStringValue.FromBytes([0x41]), VBStringValue.FromBytes([0x42]));

        Assert.AreEqual("䉁", sut.Value);
        Assert.IsNull(sut.TrailingByte);
    }

    [TestMethod]
    public void Concat_AnOddStringThenText_ShiftsTheText()
    {
        // the text's bytes move up by one: 41 then 78 00 is the code unit 78 41 and the byte 00 left over.
        var sut = VBStringValue.Concat(VBStringValue.FromBytes([0x41]), new VBStringValue("x"));

        CollectionAssert.AreEqual(new byte[] { 0x41, 0x78, 0x00 }, sut.GetBytes());
        Assert.AreEqual("硁", sut.Value);
        Assert.AreEqual((byte)0x00, sut.TrailingByte);
    }

    [TestMethod]
    public void Concat_TwoEvenStrings_JoinsTheirText()
    {
        var sut = VBStringValue.Concat(new VBStringValue("ab"), new VBStringValue("c"));

        Assert.IsInstanceOfType<VBRuntimeValue<string>>(sut.RuntimeValue);
        Assert.AreEqual("abc", sut.Value);
    }

    #endregion
}

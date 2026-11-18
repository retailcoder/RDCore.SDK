using System.Buffers.Binary;
using System.Collections;
using System.Globalization;
using System.Text;
using RDCore.SDK.Forms;

namespace RDCore.Tests.Forms;

/// <remarks>
/// Each rename is read back with <see cref="MSFormsReader"/>, whose tree must be the original's with one name changed, and
/// every stream of the compound file but the renamed control's form stream must be byte-identical to the original's. The
/// fixtures are those of <see cref="MSFormsReaderTests"/>.
/// </remarks>
[TestClass]
public sealed class MSFormsWriterTests
{
    private const int BlobHeaderSize = 24;

    private static byte[] Fixture(string fileName) => MSFormsReaderTests.Fixture(fileName);

    // every control of the form with everything the reader gives of it, one line each, under the name `rename` gives it
    private static string Tree(byte[] frx, Func<string, string>? rename = null)
        => string.Concat(MSFormsReader.Read(frx).Form.DescendantsAndSelf().Select(control =>
            $"{(rename ?? (name => name))(control.Name)} #{control.Id} {control.Kind} {control.ClassId} {control.Site} " +
            $"[{string.Join(", ", control.Properties.Names.Order(StringComparer.OrdinalIgnoreCase).Select(name => $"{name}={Text(control.Properties.Get<object>(name))}{(control.Properties.IsStored(name) ? "" : "?")}"))}] " +
            $"<{string.Join(", ", control.Children.Select(child => child.Name).Select(rename ?? (name => name)))}>\n"));

    private static string Text(object value) => value switch
    {
        MSFormsPicture picture => $"{picture.Format}:{picture.Data.Length}:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(picture.Data.Span))}",
        string text => text,
        IEnumerable items => $"({string.Join("; ", items.Cast<object>().Select(Text))})",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    private static ReadOnlySpan<byte> CompoundFileOf(byte[] frx) => frx.AsSpan(frx[0] == 0xD0 ? 0 : BlobHeaderSize);

    // every stream of the compound file by its path
    private static Dictionary<string, byte[]> Streams(byte[] frx)
    {
        var file = CompoundFile.Open(CompoundFileOf(frx).ToArray());
        var result = new Dictionary<string, byte[]>();
        Walk(file.Root, string.Empty);
        return result;

        void Walk(CompoundFileEntry storage, string path)
        {
            foreach (var entry in file.EntriesOf(storage))
            {
                if (entry.Type == CompoundFileEntryType.Stream)
                {
                    result[$"{path}/{entry.Name}"] = file.Read(entry);
                }
                else if (entry.Type == CompoundFileEntryType.Storage)
                {
                    Walk(entry, $"{path}/{entry.Name}");
                }
            }
        }
    }

    // renames the control, and checks what the reader reads back and that the form stream of the control is the only stream
    // that changed; the form stream's path and its lengths, before and after
    private static (byte[] Renamed, string Path, int OldLength, int NewLength) RenameAndCheck(byte[] frx, string name, string newName)
    {
        var renamed = MSFormsWriter.RenameControl(frx, name, newName);

        Assert.AreEqual(Tree(frx, control => string.Equals(control, name, StringComparison.OrdinalIgnoreCase) ? newName : control), Tree(renamed));
        var (before, after) = (Streams(frx), Streams(renamed));
        CollectionAssert.AreEquivalent(before.Keys, after.Keys);
        var changed = before.Keys.Where(path => !before[path].AsSpan().SequenceEqual(after[path])).ToList();
        Assert.HasCount(1, changed);
        Assert.IsTrue(changed[0].EndsWith("/f", StringComparison.Ordinal), changed[0]);
        if (frx[0] != 0xD0)
        {
            Assert.IsTrue(renamed.AsSpan(0, BlobHeaderSize).SequenceEqual(Header(frx, renamed.Length - BlobHeaderSize)), "the blob header gives the new length");
        }

        return (renamed, changed[0], before[changed[0]].Length, after[changed[0]].Length);
    }

    private static byte[] Header(byte[] frx, int length)
    {
        var header = frx.AsSpan(0, BlobHeaderSize).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), (uint)length);
        return header;
    }

    [TestMethod]
    [DataRow("ExampleView.frx", "AmountBox", "AmountBix", DisplayName = "a control of the form")]
    [DataRow("ExampleView.frx", "OptionButton1", "OptionButton9", DisplayName = "a control of a Frame's own storage")]
    [DataRow("ExampleView.frx", "OptionsFrame", "ChoicesFrame", DisplayName = "a Frame")]
    [DataRow("AllControls.storage", "InnerCheck", "InnerChecX", DisplayName = "a control of a Frame in a Frame")]
    [DataRow("AllControls.storage", "Page1", "Page9", DisplayName = "a page of a MultiPage")]
    [DataRow("AllControls.storage", "FirstPageEntry", "FirstPageEntrX", DisplayName = "a control of a page")]
    [DataRow("ExampleView.frx", "AmountBox", "amountbox", DisplayName = "a name in other cases")]
    public void ANameOfTheSameLength_ChangesNoByteButTheName(string fixture, string name, string newName)
    {
        var frx = Fixture(fixture);

        var renamed = MSFormsWriter.RenameControl(frx, name, newName);

        // the name's bytes, compressed, at exactly one of the places the original has them
        var (old, @new) = (Encoding.Latin1.GetBytes(name), Encoding.Latin1.GetBytes(newName));
        var matches = Enumerable.Range(0, frx.Length - old.Length + 1)
            .Where(at => frx.AsSpan(at, old.Length).SequenceEqual(old))
            .Count(at =>
            {
                var expected = frx.ToArray();
                @new.CopyTo(expected, at);
                return expected.AsSpan().SequenceEqual(renamed);
            });
        Assert.AreEqual(1, matches);
        Assert.AreEqual(Tree(frx, control => control == name ? newName : control), Tree(renamed));
    }

    [TestMethod]
    [DataRow("ExampleView.frx", "AmountBox", "Amount", DisplayName = "shorter, a control of the form")]
    [DataRow("ExampleView.frx", "AmountBox", "TheAmountToPay", DisplayName = "longer, a control of the form")]
    [DataRow("ExampleView.frx", "OptionButton1", "FirstOptionOfTheFrame", DisplayName = "longer, a control of a Frame's own storage")]
    [DataRow("AllControls.storage", "InnerCheck", "C", DisplayName = "shorter, a control of a Frame in a Frame")]
    [DataRow("AllControls.storage", "Page2", "TheSecondPage", DisplayName = "longer, a page of a MultiPage")]
    [DataRow("AllControls.storage", "FirstPageEntry", "Entry1", DisplayName = "shorter, a control of a page")]
    public void ANameOfAnotherLength_ChangesOnlyTheFormStreamThatHoldsIt(string fixture, string name, string newName)
    {
        var (_, _, oldLength, newLength) = RenameAndCheck(Fixture(fixture), name, newName);

        Assert.AreNotEqual(oldLength, newLength);
    }

    [TestMethod]
    [DataRow("ExampleView.frx", "AmountBox", 70, DisplayName = "by more than a mini sector")]
    [DataRow("AllControls.storage", "FirstPageEntry", 70, DisplayName = "by more than a mini sector, a control of a page")]
    [DataRow("ExampleView.frx", "OptionButton1", 2000, DisplayName = "by more than the mini stream's last sector holds")]
    public void ANameThatGrowsItsFormStreamPastAMiniSector_TakesMoreMiniSectors(string fixture, string name, int growth)
    {
        var (_, _, oldLength, newLength) = RenameAndCheck(Fixture(fixture), name, name + new string('x', growth));

        Assert.IsLessThan(4096, newLength, "the stream stays in the mini stream");
        Assert.IsGreaterThan((oldLength + 63) / 64, (newLength + 63) / 64);
    }

    [TestMethod]
    [DataRow("ExampleView.frx", "AmountBox", DisplayName = "a control of the form")]
    [DataRow("ExampleView.frx", "OptionButton2", DisplayName = "a control of a Frame's own storage")]
    [DataRow("AllControls.storage", "SecondPageGo", DisplayName = "a control of a page")]
    public void ANameThatGrowsItsFormStreamPastTheMiniStreamCutoff_MovesItToRegularSectors_AndBack(string fixture, string name)
    {
        var frx = Fixture(fixture);
        var longName = name + new string('x', 4100);

        var (renamed, path, oldLength, newLength) = RenameAndCheck(frx, name, longName);
        var (back, _, _, backLength) = RenameAndCheck(renamed, longName, name);

        Assert.IsLessThan(4096, oldLength);
        Assert.IsGreaterThanOrEqualTo(4096, newLength);
        Assert.AreEqual(oldLength, backLength);
        Assert.AreEqual(Tree(frx), Tree(back));
        var (before, after) = (Streams(frx), Streams(back));
        Assert.IsTrue(before.Where(stream => stream.Key != path).All(stream => stream.Value.AsSpan().SequenceEqual(after[stream.Key])), "every other stream is as it was");

        // the form stream is as it was but for the name's padding, which a name of another length writes as zeros: nothing reads it, and the
        // original's may hold other bytes
        var padding = PaddingOf(before[path], path, name);
        var differences = Enumerable.Range(0, oldLength).Where(at => before[path][at] != after[path][at]).ToList();
        Assert.IsTrue(differences.All(at => at >= padding.Start.Value && at < padding.End.Value), $"bytes {string.Join(", ", differences)} differ; the padding is {padding}");
        Assert.IsTrue(differences.All(at => after[path][at] == 0));
    }

    // the bytes that pad the site's Name to 4 bytes from the site's first byte ([MS-OFORMS] 2.2.10.12.1)
    private static Range PaddingOf(byte[] stream, string path, string name)
    {
        var form = FormStream.Read(stream, path == "/f" ? MSFormsControlKind.Form : MSFormsControlKind.Frame);
        var site = form.Sites.Single(candidate => candidate.Name == name);
        var (countAt, textAt) = site.Block.StringsAt["Name"];
        var end = textAt + (int)(BinaryPrimitives.ReadUInt32LittleEndian(stream.AsSpan(countAt)) & 0x7FFFFFFF);
        return new Range(end, site.Block.Start + ((end - site.Block.Start + 3) & ~3));
    }

    [TestMethod]
    [DataRow("Montée", DisplayName = "one byte a character: compressed")]
    [DataRow("Montantŝ", DisplayName = "a character of two bytes: UTF-16")]
    public void ANameOfCharactersBeyondAscii_ReadsBack(string newName)
    {
        RenameAndCheck(Fixture("ExampleView.frx"), "AmountBox", newName);
    }

    [TestMethod]
    [DataRow("NoSuchControl", "Other", DisplayName = "no control has the name")]
    [DataRow("AmountBox", "CancelButton", DisplayName = "another control has the new name")]
    [DataRow("AmountBox", "cancelbutton", DisplayName = "another control has the new name, in other cases")]
    [DataRow("AmountBox", "", DisplayName = "the new name is empty")]
    public void ARenameThatNamesNoControl_OrTwo_IsRefused(string name, string newName)
    {
        Assert.Throws<ArgumentException>(() => MSFormsWriter.RenameControl(Fixture("ExampleView.frx"), name, newName));
    }

    [TestMethod]
    public void DataAfterTheCompoundFile_IsKept_WhenItDoesNotMove_AndRefusedWhenItWould()
    {
        byte[] frx = [.. Fixture("ExampleView.frx"), 1, 2, 3];

        var renamed = MSFormsWriter.RenameControl(frx, "AmountBox", "AmountBix");

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, renamed[^3..]);
        // a longer name that the form stream's last mini sector holds moves nothing; one past the mini stream cutoff grows the file
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, MSFormsWriter.RenameControl(frx, "AmountBox", "TheAmountToPay")[^3..]);
        Assert.ThrowsExactly<NotSupportedException>(() => MSFormsWriter.RenameControl(frx, "AmountBox", "AmountBox" + new string('x', 4100)));
    }

    [TestMethod]
    [DataRow("ExampleView.frx")]
    [DataRow("AllControls.storage")]
    [DataRow("Resources.frx")]
    [DataRow("ExploreTextboxEvents.frx")]
    public void CountOfBytes_CountsTheDepthsTheirPaddingAndTheSites(string fixture)
    {
        var forms = Streams(Fixture(fixture)).Where(stream => stream.Key.EndsWith("/f", StringComparison.Ordinal)).ToList();

        Assert.IsNotEmpty(forms);
        foreach (var (path, bytes) in forms)
        {
            // the storage's kind only picks the FormControl's slots, which have the same layout
            var form = FormStream.Read(bytes, path == "/f" ? MSFormsControlKind.Form : MSFormsControlKind.Frame);
            Assert.AreEqual((uint)(form.SitesEnd - form.DepthsAt), BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(form.CountOfBytesAt)), path);
        }
    }
}

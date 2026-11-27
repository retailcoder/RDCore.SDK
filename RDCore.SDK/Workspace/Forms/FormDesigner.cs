using System.Buffers.Binary;
using System.Globalization;
using System.Text.RegularExpressions;
using RDCore.SDK.Forms;
using RDCore.SDK.Model.Source;

namespace RDCore.SDK.Workspace.Forms;

/// <summary>
/// Where a form module's designer data is, in the module's companion file.
/// </summary>
/// <param name="FileName">The companion file's name, as the module writes it (<c>ExampleView.frx</c>): relative to the
/// module's own file.</param>
/// <param name="Offset">The offset of the designer data in the companion file: <c>0</c> for a Visual Basic 6.0 form, whose
/// designer data is the whole file.</param>
public sealed record class FormDesignerReference(string FileName, long Offset);

/// <summary>
/// A string property of a Visual Basic 6.0 form or of one of its controls whose value is the name of a control of the form
/// (<see cref="FormDesigner.PropertiesNaming"/>).
/// </summary>
/// <param name="Owner">The name of the control that has the property, or of the form.</param>
/// <param name="Property">The name of the property, as the designer block writes it.</param>
/// <param name="Range">Where the value is written in the form module's source, inside its quotes: zero-based lines and characters.</param>
public sealed record class FormControlNameReference(string Owner, string Property, SourceRange Range);

/// <summary>
/// Reads what a form module's designer declares: the form's class and designer properties, and its controls.
/// </summary>
/// <remarks>
/// A form module (<c>.frm</c>) opens on a <c>VERSION 5.00</c> header line and a designer block:
/// <c>Begin &lt;class&gt; &lt;name&gt;</c> … <c>End</c>, before its code-behind. What the block holds depends on the
/// designer that wrote it:
/// <list type="bullet">
/// <item>A <b>VBA UserForm</b> (<c>Begin {C62A69F0-16DC-11CE-9E98-00AA00574A4F} UserForm1</c>) holds the form's own
/// properties only. Its controls are in the designer data Microsoft Forms persisted, which the VBE exports to the
/// companion <c>.frx</c> file at the <c>OleObjectBlob</c> property's offset: a 24-byte blob header, whose bytes 4 to 7 are
/// the length of what follows it, then a compound file ([MS-CFB]) holding the form's storage ([MS-OFORMS] 2.1.2). The blob
/// header is not documented; its layout is what the VBE writes.</item>
/// <item>A <b>Visual Basic 6.0</b> form (<c>Begin VB.Form Form1</c>) holds its controls too, as nested <c>Begin</c> …
/// <c>End</c> blocks, each with its properties; <c>BeginProperty</c> … <c>EndProperty</c> blocks group a property's own
/// properties (a <c>Font</c>). Its <c>.frx</c> file holds binary property values (pictures, lists), which a property
/// cites as <c>"Form1.frx":0000</c>. The controls' declarations do not need it; the library that hosts the form reads it
/// with the designer block's text (<see cref="FormDescriptor.DesignerText"/>).</item>
/// <item>An <b>RDForms</b> form is a <c>.rdform</c> module (<see cref="ProjectModuleKinds.RDFormsModuleExtension"/>) with no designer block: its
/// controls are in the RDForms document of the same base name beside it (<see cref="RDFormsDocumentOf"/>), which the <see cref="IFormDocumentReader"/>
/// registered for its extension reads (<see cref="Register"/>).</item>
/// </list>
/// The reader takes the companion file's content from its caller, which owns the file system:
/// <see cref="CompanionOf(string)"/> says which file and where, and <see cref="ReadCompanion"/> reads it.
/// </remarks>
public static partial class FormDesigner
{
    /// <summary>
    /// The extension of an RDForms form's document, which holds its controls: <c>.xaml</c>.
    /// </summary>
    public const string RDFormsDocumentExtension = ".xaml";

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, IFormDocumentReader> DocumentReaders = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The path of the RDForms document of the RDForms form whose code is at <paramref name="modulePath"/>: the file of the same base name in the same folder,
    /// with the <see cref="RDFormsDocumentExtension"/> extension (<c>Forms/Form1.rdform</c> pairs with <c>Forms/Form1.xaml</c>).
    /// </summary>
    /// <param name="modulePath">The path or relative URI of the form's <c>.rdform</c> module.</param>
    /// <exception cref="ArgumentNullException"><paramref name="modulePath"/> is <see langword="null"/>.</exception>
    public static string RDFormsDocumentOf(string modulePath)
    {
        ArgumentNullException.ThrowIfNull(modulePath);
        return Path.ChangeExtension(modulePath, RDFormsDocumentExtension);
    }

    /// <summary>
    /// Registers the reader of the form documents of its <see cref="IFormDocumentReader.DocumentExtension"/>, in place of any registered for it before: every
    /// read of this process that takes a module's path
    /// (<see cref="Read(string?, string?, ReadOnlyMemory{byte}, out FormDesignerFormatException?)"/>) reads such a form's document with it.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/>.</exception>
    public static void Register(IFormDocumentReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        DocumentReaders[reader.DocumentExtension] = reader;
    }

    /// <summary>
    /// The class identifier of the Microsoft Forms 2.0 designer, which names a VBA UserForm's designer block:
    /// <see cref="FormControlClass.MSFormsDesignerClassId"/>.
    /// </summary>
    public static readonly Guid MSFormsClassId = FormControlClass.MSFormsDesignerClassId;

    /// <summary>
    /// The name of the Microsoft Forms type library, which qualifies the classes of a UserForm and of its controls (<c>MSForms.TextBox</c>):
    /// <see cref="FormControlClass.MSFormsLibrary"/>.
    /// </summary>
    public const string MSFormsLibrary = FormControlClass.MSFormsLibrary;

    /// <summary>
    /// The extension of the companion file of each kind of designer file, keyed by the designer file's extension: a form's <c>.frx</c>
    /// (<c>.frm</c>), a user control's <c>.ctx</c> (<c>.ctl</c>), a designer's <c>.dsx</c> (<c>.dsr</c>), a property page's <c>.pgx</c>
    /// (<c>.pag</c>), a user document's <c>.dox</c> (<c>.dob</c>), and an RDForms form's document, <see cref="RDFormsDocumentExtension"/>
    /// (<see cref="ProjectModuleKinds.RDFormsModuleExtension"/>).
    /// </summary>
    /// <remarks>
    /// Each extension has its dot; a key is looked up ignoring case.
    /// </remarks>
    public static IReadOnlyDictionary<string, string> CompanionExtensions { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".frm"] = ".frx",
        [".ctl"] = ".ctx",
        [".dsr"] = ".dsx",
        [".pag"] = ".pgx",
        [".dob"] = ".dox",
        [ProjectModuleKinds.RDFormsModuleExtension] = RDFormsDocumentExtension,
    };

    private const int BlobHeaderSize = 24;

    /// <summary>
    /// Where the designer data of the form module <paramref name="source"/> is, or <c>null</c> when the module is not a
    /// form module or keeps no designer data in a companion file.
    /// </summary>
    /// <remarks>
    /// A VBA UserForm's is at its <c>OleObjectBlob</c> property's offset. A Visual Basic 6.0 form's is the whole file that the first
    /// property citing one names, in the form's block or in any of its controls', grouped properties included
    /// (<c>Picture = "Form1.frx":0000</c>), at offset <c>0</c>.
    /// </remarks>
    /// <param name="source">The form module's source.</param>
    /// <exception cref="FormDesignerFormatException">The designer block is not terminated.</exception>
    public static FormDesignerReference? CompanionOf(string? source)
        => ReadSource(source) is { } designer ? CompanionOf(designer) : null;

    private static FormDesignerReference? CompanionOf(DesignerSource designer)
    {
        if (KindOf(designer.Root.Class) == FormDesignerKind.VisualBasic6)
        {
            return designer.Cited;
        }

        return designer.Root.Properties.GetValueOrDefault("OleObjectBlob") is { } blob && FileReference().Match(blob) is { Success: true } match
            ? new FormDesignerReference(
                match.Groups["file"].Value.Replace("\"\"", "\""),
                long.Parse(match.Groups["offset"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture))
            : null;
    }

    /// <summary>
    /// The path of the companion file of the form module at <paramref name="modulePath"/>: the file its source names (<see cref="CompanionOf(string)"/>),
    /// beside the module's own file, or an RDForms form's document (<see cref="RDFormsDocumentOf"/>), as <see cref="ReadCompanion"/> reads it.
    /// </summary>
    /// <param name="modulePath">The path of the form module's file.</param>
    /// <param name="source">The form module's source, which an RDForms form's path does not depend on.</param>
    /// <returns>
    /// The path, whether or not the file is there; <see langword="null"/> when the module names no companion file, or its designer block is not
    /// terminated.
    /// </returns>
    public static string? CompanionPathOf(string modulePath, string? source)
    {
        if (ProjectModuleKinds.IsRDFormsModule(modulePath))
        {
            return RDFormsDocumentOf(modulePath);
        }

        try
        {
            return CompanionOf(source) is { } reference ? Path.Combine(Path.GetDirectoryName(modulePath) ?? string.Empty, reference.FileName) : null;
        }
        catch (FormDesignerFormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads the companion file of the form module at <paramref name="modulePath"/>: the file its source names (<see cref="CompanionOf(string)"/>), which is
    /// beside the module's own file.
    /// </summary>
    /// <remarks>
    /// A program that loads a workspace's form modules reads each one's companion with its text, then gives both to
    /// <see cref="Read(string, ReadOnlyMemory{byte}, out FormDesignerFormatException)"/>. A module whose companion is not read is still loaded. A VBA
    /// UserForm's controls are in its companion, so without it the read gives the problem and the form declares no controls. A Visual Basic 6.0 form's
    /// controls are in its text, so it declares them, with an empty <see cref="FormDescriptor.State"/>.
    /// <para>
    /// An RDForms form's companion is its RDForms document (<see cref="RDFormsDocumentOf"/>), which its text does not name: a missing document is the problem
    /// <see cref="FormDesignerFormatProblem.MissingDocument"/>, and the module is still loaded, with no controls.
    /// </para>
    /// </remarks>
    /// <param name="fileSystem">The file system the module is on.</param>
    /// <param name="modulePath">The path of the form module's file.</param>
    /// <param name="source">The form module's source.</param>
    /// <param name="problem">
    /// Why the companion file was not read: a <see cref="FormDesignerFormatException"/> when the designer block is not terminated, an
    /// <see cref="IOException"/> when the file cannot be read. <c>null</c> when it was read, and when there is none to read.
    /// </param>
    /// <returns>
    /// The content of the companion file. Empty, with no problem, when the module names no companion file or the file is not there; empty, with the problem,
    /// when the designer block or the file cannot be read, and when an RDForms form's document is not there.
    /// </returns>
    public static ReadOnlyMemory<byte> ReadCompanion(System.IO.Abstractions.IFileSystem fileSystem, string modulePath, string? source, out Exception? problem)
    {
        problem = null;
        try
        {
            if (ProjectModuleKinds.IsRDFormsModule(modulePath))
            {
                var document = RDFormsDocumentOf(modulePath);
                if (fileSystem.File.Exists(document))
                {
                    return fileSystem.File.ReadAllBytes(document);
                }

                problem = new FormDesignerFormatException(FormDesignerFormatProblem.MissingDocument);
            }
            else if (CompanionOf(source) is { } reference
                && fileSystem.Path.Combine(fileSystem.Path.GetDirectoryName(modulePath) ?? string.Empty, reference.FileName) is var path
                && fileSystem.File.Exists(path))
            {
                return fileSystem.File.ReadAllBytes(path);
            }
        }
        catch (Exception exception) when (exception is FormDesignerFormatException or IOException)
        {
            problem = exception;
        }

        return ReadOnlyMemory<byte>.Empty;
    }

    /// <summary>
    /// What the form module <paramref name="source"/> declares, as <see cref="Read(string, ReadOnlyMemory{byte})"/> reads it, or <c>null</c> when it is not a
    /// form module or its designer data cannot be read, which <paramref name="problem"/> then says.
    /// </summary>
    /// <remarks>
    /// A form whose designer data cannot be read declares nothing, so its module is loaded as one whose form has no controls.
    /// </remarks>
    /// <param name="source">The form module's source.</param>
    /// <param name="companion">The content of the module's companion file (<see cref="ReadCompanion"/>).</param>
    /// <param name="problem">
    /// Why the designer data cannot be read (<see cref="FormDesignerFormatException.Problem"/>); <c>null</c> when it was read, and for a module that is not a
    /// form module.
    /// </param>
    public static FormDescriptor? Read(string? source, ReadOnlyMemory<byte> companion, out FormDesignerFormatException? problem)
        => Read(modulePath: null, source, companion, out problem);

    /// <summary>
    /// What the form module at <paramref name="modulePath"/> declares, as <see cref="Read(string?, string?, ReadOnlyMemory{byte})"/> reads it, or <c>null</c>
    /// when it is not a form module or its designer data cannot be read, which <paramref name="problem"/> then says.
    /// </summary>
    /// <remarks>
    /// A form whose designer data cannot be read declares nothing, so its module is loaded as one whose form has no controls.
    /// </remarks>
    /// <param name="modulePath">The path or relative URI of the module's file, or <see langword="null"/> for a module that has no file.</param>
    /// <param name="source">The form module's source.</param>
    /// <param name="companion">The content of the module's companion file (<see cref="ReadCompanion"/>).</param>
    /// <param name="problem">
    /// Why the designer data cannot be read (<see cref="FormDesignerFormatException.Problem"/>); <c>null</c> when it was read, and for a module that is not a
    /// form module.
    /// </param>
    public static FormDescriptor? Read(string? modulePath, string? source, ReadOnlyMemory<byte> companion, out FormDesignerFormatException? problem)
    {
        problem = null;
        try
        {
            return Read(modulePath, source, companion);
        }
        catch (FormDesignerFormatException exception)
        {
            problem = exception;
            return null;
        }
    }

    /// <summary>
    /// What the form module at <paramref name="modulePath"/> declares, or <c>null</c> when it is not a form module.
    /// </summary>
    /// <remarks>
    /// The code of an RDForms form (<see cref="ProjectModuleKinds.IsRDFormsModule"/>) declares what its RDForms document does, as the
    /// <see cref="IFormDocumentReader"/> registered for <see cref="RDFormsDocumentExtension"/> reads it (<see cref="Register"/>). Any other module is read as
    /// <see cref="Read(string?, ReadOnlyMemory{byte})"/> reads it.
    /// </remarks>
    /// <param name="modulePath">The path or relative URI of the module's file, or <see langword="null"/> for a module that has no file.</param>
    /// <param name="source">The form module's source.</param>
    /// <param name="companion">The content of the module's companion file (<see cref="ReadCompanion"/>): an RDForms form's document.</param>
    /// <exception cref="FormDesignerFormatException">
    /// The designer data cannot be read, as <see cref="Read(string?, ReadOnlyMemory{byte})"/> says; or, for an RDForms form, <paramref name="companion"/> is
    /// empty (<see cref="FormDesignerFormatProblem.MissingDocument"/>), no reader is registered for its document
    /// (<see cref="FormDesignerFormatProblem.NoDocumentReader"/>), or its document holds no form (<see cref="FormDesignerFormatProblem.InvalidDocument"/>).
    /// </exception>
    public static FormDescriptor? Read(string? modulePath, string? source, ReadOnlyMemory<byte> companion)
    {
        if (!ProjectModuleKinds.IsRDFormsModule(modulePath))
        {
            return Read(source, companion);
        }

        if (companion.IsEmpty)
        {
            throw new FormDesignerFormatException(FormDesignerFormatProblem.MissingDocument);
        }

        return DocumentReaders.TryGetValue(RDFormsDocumentExtension, out var reader)
            ? reader.Read(companion)
            : throw new FormDesignerFormatException(FormDesignerFormatProblem.NoDocumentReader);
    }

    /// <summary>
    /// What the form module <paramref name="source"/> declares, or <c>null</c> when it is not a form module (it has no
    /// designer block).
    /// </summary>
    /// <remarks>
    /// A VBA UserForm's controls are read from its designer data, which is the descriptor's <see cref="FormDescriptor.State"/>. A Visual Basic 6.0 form's are
    /// read from its text; its <see cref="FormDescriptor.DesignerText"/> is that text, and its <see cref="FormDescriptor.State"/> the whole companion file,
    /// or empty when <paramref name="companion"/> is.
    /// </remarks>
    /// <param name="source">The form module's source.</param>
    /// <param name="companion">The content of the module's companion file (<see cref="CompanionOf(string)"/>), when the module
    /// keeps designer data there; ignored when it does not.</param>
    /// <exception cref="FormDesignerFormatException">The designer block is not terminated, a VBA UserForm keeps designer data in
    /// a companion file and <paramref name="companion"/> is empty, or that designer data cannot be read.</exception>
    public static FormDescriptor? Read(string? source, ReadOnlyMemory<byte> companion = default)
    {
        if (ReadSource(source) is not { } designer)
        {
            return null;
        }

        var block = designer.Root;
        var @class = block.Class;
        var kind = KindOf(@class);
        var controls = block.Children.Select(ToDescriptor).ToList();
        var state = ReadOnlyMemory<byte>.Empty;
        string? text = null;
        if (kind == FormDesignerKind.MSForms && CompanionOf(designer) is { } reference)
        {
            state = BlobAt(companion, reference.Offset);
            try
            {
                controls = [.. MSFormsReader.Read(state.Span).Form.Children.Select(ToDescriptor)];
            }
            catch (MSFormsFormatException exception)
            {
                throw new FormDesignerFormatException(exception);
            }
        }
        else if (kind == FormDesignerKind.VisualBasic6)
        {
            text = designer.Text;
            state = designer.Cited is null ? ReadOnlyMemory<byte>.Empty : companion;
        }

        return new FormDescriptor(block.Name, kind, @class, block.Properties, controls) { State = state, DesignerText = text };
    }

    /// <summary>
    /// The designer's state of the form module <paramref name="source"/>: for a VBA UserForm, the compound file its companion file keeps at the offset its
    /// <c>OleObjectBlob</c> says, which is what Microsoft Forms loads a form from (<c>IPersistStorage</c>); for a Visual Basic 6.0 form, the whole companion
    /// file. Empty when the module keeps no designer data in a companion file.
    /// </summary>
    /// <remarks>
    /// The state is opaque here: <see cref="Read"/> reads what a UserForm declares from it, and the form host hands it, as it is, to the library that hosts the
    /// form.
    /// </remarks>
    /// <param name="source">The form module's source.</param>
    /// <param name="companion">The content of the module's companion file (<see cref="CompanionOf(string)"/>).</param>
    /// <exception cref="FormDesignerFormatException">The designer block is not terminated, a VBA UserForm keeps designer data in a companion file and
    /// <paramref name="companion"/> is empty, or the designer data is not where it says.</exception>
    public static ReadOnlyMemory<byte> StateOf(string? source, ReadOnlyMemory<byte> companion)
    {
        if (ReadSource(source) is not { } designer || CompanionOf(designer) is not { } reference)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        return KindOf(designer.Root.Class) == FormDesignerKind.VisualBasic6 ? companion : BlobAt(companion, reference.Offset);
    }

    /// <summary>
    /// Where the name of a Visual Basic 6.0 form's control is written in the form module's designer block: on the <c>Begin &lt;class&gt; &lt;name&gt;</c>
    /// line of each control so named, which is every element of a control array, at any depth.
    /// </summary>
    /// <remarks>
    /// The name is what a rename of the control changes in the designer block. Names compare case-insensitively. The form's own <c>Begin</c> line names
    /// the form, not a control. A VBA UserForm keeps its controls' names in its companion file, not in the module's text, so none is found in one, nor
    /// in another designer's form.
    /// </remarks>
    /// <param name="source">The form module's source.</param>
    /// <param name="name">The name of the control.</param>
    /// <returns>
    /// The range of the name on each <c>Begin</c> line, in the order the lines are written: empty when no control of the form has the name, and for a
    /// module that is not a Visual Basic 6.0 form.
    /// </returns>
    /// <exception cref="FormDesignerFormatException">The designer block is not terminated.</exception>
    public static IReadOnlyList<SourceRange> ControlNameRangesOf(string? source, string name)
    {
        if (ReadSource(source) is not { } designer || KindOf(designer.Root.Class) != FormDesignerKind.VisualBasic6)
        {
            return [];
        }

        return [.. designer.Root.Children.SelectMany(block => block.DescendantsAndSelf())
            .Where(block => string.Equals(block.Name, name, StringComparison.OrdinalIgnoreCase))
            .Select(NameRangeOf)];
    }

    /// <summary>
    /// Where a form module's designer block writes the name of the form: on its first <c>Begin &lt;class&gt; &lt;name&gt;</c> line, whichever designer wrote
    /// it (<c>Begin VB.Form Form1</c>, <c>Begin {C62A69F0-16DC-11CE-9E98-00AA00574A4F} Dialog</c>).
    /// </summary>
    /// <remarks>
    /// The name is what a rename of the form's module changes in the designer block, with the module's <c>VB_Name</c> attribute.
    /// </remarks>
    /// <param name="source">The form module's source.</param>
    /// <returns>The range of the name, or <c>null</c> when the module is not a form module (it has no designer block).</returns>
    /// <exception cref="FormDesignerFormatException">The designer block is not terminated.</exception>
    public static SourceRange? FormNameRangeOf(string? source)
        => ReadSource(source) is { } designer ? NameRangeOf(designer.Root) : null;

    /// <summary>
    /// The string properties of a Visual Basic 6.0 form, and of its controls other than those named <paramref name="name"/>, whose whole value is
    /// <paramref name="name"/>: what may name the control so named, as an UpDown's <c>BuddyControl = "Text1"</c> names its buddy and a data-bound control's
    /// <c>DataSource = "Adodc1"</c> its data control.
    /// </summary>
    /// <remarks>
    /// Whether a property names the control, or only holds the same text (a <c>Label</c>'s <c>Caption</c>), is the property's own meaning, which the designer
    /// block does not say: every such property is found. Names compare case-insensitively. The properties of a grouped property (<c>BeginProperty</c> …
    /// <c>EndProperty</c>) are not the control's, and are not searched. A VBA UserForm's properties are in its companion file: none is found in one.
    /// </remarks>
    /// <param name="source">The form module's source.</param>
    /// <param name="name">The name of the control.</param>
    /// <returns>Each property so found, in the order it is written: empty for a module that is not a Visual Basic 6.0 form.</returns>
    /// <exception cref="FormDesignerFormatException">The designer block is not terminated.</exception>
    public static IReadOnlyList<FormControlNameReference> PropertiesNaming(string? source, string name)
    {
        if (ReadSource(source) is not { } designer || KindOf(designer.Root.Class) != FormDesignerKind.VisualBasic6)
        {
            return [];
        }

        return [.. designer.Root.DescendantsAndSelf()
            .Where(block => ReferenceEquals(block, designer.Root) || !string.Equals(block.Name, name, StringComparison.OrdinalIgnoreCase))
            .SelectMany(block => block.Strings
                .Where(property => string.Equals(property.Value, name, StringComparison.OrdinalIgnoreCase))
                .Select(property => new FormControlNameReference(block.Name, property.Property, new SourceRange(
                    new SourcePosition(property.Line, property.Start),
                    new SourcePosition(property.Line, property.Start + name.Length)))))];
    }

    private static SourceRange NameRangeOf(DesignerBlock block)
        => new(new SourcePosition(block.Line, block.NameColumn), new SourcePosition(block.Line, block.NameColumn + block.Name.Length));

    // the designer data at `offset` of a companion file: its blob header, then that many bytes
    private static ReadOnlyMemory<byte> BlobAt(ReadOnlyMemory<byte> companion, long offset)
    {
        if (companion.IsEmpty)
        {
            throw new FormDesignerFormatException(FormDesignerFormatProblem.MissingCompanion);
        }

        if (offset < 0 || offset > companion.Length - BlobHeaderSize)
        {
            throw new FormDesignerFormatException(FormDesignerFormatProblem.BlobOutOfRange);
        }

        var length = BinaryPrimitives.ReadUInt32LittleEndian(companion.Span[(int)(offset + 4)..]);
        var start = (int)offset + BlobHeaderSize;
        return length > companion.Length - start
            ? throw new FormDesignerFormatException(FormDesignerFormatProblem.BlobOutOfRange)
            : companion.Slice(start, (int)length);
    }

    // the designer of a form whose header names the class (ModuleHeader.DesignerOf): a class identifier alone is another ActiveX designer's
    private static FormDesignerKind KindOf(FormControlClass @class)
        => @class.ClassId == MSFormsClassId ? FormDesignerKind.MSForms
            : @class.ClassId is not null ? FormDesignerKind.Other
            : FormDesignerKind.VisualBasic6;

    private static FormControlDescriptor ToDescriptor(DesignerBlock block)
        => new(
            block.Name,
            block.Class,
            IntegerOf(block.Properties.GetValueOrDefault("TabIndex")),
            IntegerOf(block.Properties.GetValueOrDefault("Index")),
            [.. block.Children.Select(ToDescriptor)]);

    // a UserForm's control by the Microsoft Forms reader: an ActiveX control that is not Microsoft Forms' own is named by its
    // CLSID, a nested form (cache index 7 outside a MultiPage) is a UserForm
    private static FormControlDescriptor ToDescriptor(MSFormsControl control)
        => new(
            control.Name,
            control.Kind switch
            {
                MSFormsControlKind.Other => new FormControlClass(null, null, control.ClassId),
                MSFormsControlKind.Form => new FormControlClass(MSFormsLibrary, "UserForm"),
                _ => new FormControlClass(MSFormsLibrary, control.Kind.ToString()),
            },
            control.Site is { TabIndex: >= 0 } site ? site.TabIndex : null,
            Index: null,
            [.. control.Children.Select(ToDescriptor)]);

    private static int? IntegerOf(string? value)
        => int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result) ? result : null;

    // a `Begin` block: its name is at `NameColumn` (zero-based) of the source's zero-based `Line`; its string properties are where they are written
    private sealed record class DesignerBlock(FormControlClass Class, string Name, Dictionary<string, string> Properties, List<DesignerBlock> Children,
        int Line, int NameColumn)
    {
        public List<DesignerString> Strings { get; } = [];

        public IEnumerable<DesignerBlock> DescendantsAndSelf()
            => Children.SelectMany(child => child.DescendantsAndSelf()).Prepend(this);
    }

    // a property whose value is a string literal: its value, unquoted, and where its text starts, past the opening quote, on its zero-based line
    private sealed record class DesignerString(string Property, string Value, int Line, int Start);

    // the designer block, the source from its first line through the block's End, and the first companion file a property of
    // the block cites at any depth, grouped properties included
    private sealed record class DesignerSource(DesignerBlock Root, string Text, FormDesignerReference? Cited);

    // the designer block the header names (ModuleHeader.DesignerOf), which every client reads the same way: null when it names none, as a class
    // module's header (`BEGIN` alone) does not. It throws UnterminatedBlock exactly when the header's EndLine is null, so every client finds the same
    // blocks unclosed; the loop's own throw is never reached, as both apply the same End rule.
    private static DesignerSource? ReadSource(string? source)
    {
        if (ModuleHeader.DesignerOf(source) is not { } header)
        {
            return null;
        }

        if (header.EndLine is null)
        {
            throw new FormDesignerFormatException(FormDesignerFormatProblem.UnterminatedBlock);
        }

        var lines = source!.Split('\n');
        var index = header.Line;
        var root = new DesignerBlock(header.Class, header.Name, new(StringComparer.OrdinalIgnoreCase), [], header.Line, header.NameColumn);
        var open = new Stack<DesignerBlock>([root]);
        var propertyDepth = 0;
        FormDesignerReference? cited = null;
        for (index++; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (cited is null && PropertyLine().Match(line) is { Success: true } citing
                && FileReference().Match(ValueOf(citing.Groups["value"].Value)) is { Success: true } file)
            {
                cited = new FormDesignerReference(file.Groups["file"].Value.Replace("\"\"", "\""), 0);
            }

            // a grouped property's own properties (a Font's Name, Size) are not the control's
            if (line.StartsWith("BeginProperty", StringComparison.OrdinalIgnoreCase))
            {
                propertyDepth++;
                continue;
            }

            if (line.StartsWith("EndProperty", StringComparison.OrdinalIgnoreCase))
            {
                propertyDepth = Math.Max(0, propertyDepth - 1);
                continue;
            }

            if (propertyDepth > 0)
            {
                continue;
            }

            if (BeginLine().Match(lines[index]) is { Success: true } child)
            {
                var block = new DesignerBlock(FormControlClass.Parse(child.Groups["class"].Value), child.Groups["name"].Value,
                    new(StringComparer.OrdinalIgnoreCase), [], index, child.Groups["name"].Index);
                open.Peek().Children.Add(block);
                open.Push(block);
            }
            else if (EndLine().IsMatch(line))
            {
                open.Pop();
                if (open.Count == 0)
                {
                    // each line but the last kept its '\n' in the split: the text ends with the End line's own line break
                    var end = lines.Take(index + 1).Sum(text => text.Length) + Math.Min(index + 1, lines.Length - 1);
                    return new DesignerSource(root, source[..end], cited);
                }
            }
            else if (PropertyLine().Match(line) is { Success: true } property)
            {
                var name = property.Groups["name"].Value;
                var value = ValueOf(property.Groups["value"].Value);
                open.Peek().Properties[name] = value;
                if (property.Groups["value"].Value.StartsWith('"'))
                {
                    // a property's name has no quote: the line's first one opens the value
                    open.Peek().Strings.Add(new DesignerString(name, value, index, lines[index].IndexOf('"') + 1));
                }
            }
        }

        throw new FormDesignerFormatException(FormDesignerFormatProblem.UnterminatedBlock);
    }

    // a property's value as written, without its trailing comment (`1  'CenterOwner`); a string literal unquoted
    private static string ValueOf(string text)
    {
        var inString = false;
        var end = text.Length;
        for (var position = 0; position < text.Length; position++)
        {
            if (text[position] == '"')
            {
                inString = !inString;
            }
            else if (text[position] == '\'' && !inString)
            {
                end = position;
                break;
            }
        }

        var value = text[..end].Trim();
        return StringLiteral().Match(value) is { Success: true } literal
            ? literal.Groups["value"].Value.Replace("\"\"", "\"")
            : value;
    }

    [GeneratedRegex(@"^[ \t]*Begin[ \t]+(?<class>\{[0-9A-Fa-f-]+\}|[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)?)[ \t]+(?<name>[A-Za-z_][A-Za-z0-9_]*)[ \t]*\r?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BeginLine();

    [GeneratedRegex(@"^End$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EndLine();

    [GeneratedRegex(@"^(?<name>[A-Za-z_][A-Za-z0-9_]*)[ \t]*=[ \t]*(?<value>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex PropertyLine();

    [GeneratedRegex(@"^""(?<value>(?:[^""]|"""")*)""$", RegexOptions.CultureInvariant)]
    private static partial Regex StringLiteral();

    [GeneratedRegex(@"^""(?<file>(?:[^""]|"""")*)"":(?<offset>[0-9A-Fa-f]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex FileReference();
}

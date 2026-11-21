namespace RDCore.SDK.Runtime.Stdole;

/// <summary>
/// What a <c>stdole.StdFont</c> object holds: a font's name, size, weight, style and character set, with the size snapping and the
/// <c>FontChanged</c> notification MS-VBA has.
/// </summary>
/// <remarks>
/// <para>
/// One object is one font, by reference: a runtime binds it to the VBA object that <c>New StdFont</c> makes, and a form's control that is
/// given the font can hold the same object, so that a change through any reference is seen through all of them. Whether a control shares
/// a font it is given or copies it (<see cref="Clone"/>) is the control's own rule.
/// </para>
/// <para>
/// <see cref="Name"/> and <see cref="Charset"/> read back what was set. MS-VBA reads back the face and the character set Windows' font
/// mapper chose for them instead (an unknown name reads <c>Arial</c>, and <c>Wingdings</c> reads Charset 2); here, whoever draws the
/// font picks the face it falls back on.
/// </para>
/// <para>
/// A size is drawn at a whole number of pixels, so a font reads back the size it is drawn at (<see cref="DrawnSize"/>). As in MS-VBA,
/// the size snaps when it is read, not when it is set, and a read keeps the size it reads. Setting <see cref="Name"/> snaps the size held
/// again, to the new face, at the next read: the size set when it was not read since, otherwise the size last read.
/// </para>
/// <para>
/// A font whose name was never set reads <c>MS Sans Serif</c> but is drawn in a scalable face, as MS-VBA draws it in the face Windows
/// maps no name to: its sizes snap to whole pixels, of at least 2, and a size of 2 pixels reads back as 3 (2.25 points). Setting any
/// name, <c>MS Sans Serif</c> included, gives it that face's sizes. <see cref="IsNamed"/> tells the two apart, for whoever draws the font.
/// </para>
/// <para>
/// The object is safe to use from several threads. <see cref="Changed"/> is raised on the thread that made the change.
/// </para>
/// </remarks>
public sealed class StdFontObject
{
    /// <summary>
    /// The name of a new font: <c>MS Sans Serif</c>.
    /// </summary>
    public const string DefaultName = "MS Sans Serif";

    /// <summary>
    /// The size of a new font, in points: 8.25.
    /// </summary>
    public const decimal DefaultSize = 8.25m;

    /// <summary>
    /// The weight of a normal font, and of a new one: 400.
    /// </summary>
    public const short NormalWeight = 400;

    /// <summary>
    /// The weight of a bold font: 700.
    /// </summary>
    public const short BoldWeight = 700;

    // the largest weight that reads back as a normal one; any more, up to 1000, reads back bold.
    private const short LastNormalWeight = 550;

    private const short MaxWeight = 1000;
    private const short MaxCharset = 255;

    // the smallest size MS-VBA refuses as too large (an overflow), and the longest name it takes.
    private const decimal OverflowSize = 24576m;
    private const int MaxNameLength = 31;

    // the largest height in pixels a font is drawn at. MS-VBA takes a size from 24575.625 points up to 24576, which is 32768 pixels, and reads
    // back a value of no meaning for it (24575.75 reads 190663.452); here it reads 24575.25.
    private const decimal MaxPixels = 32767m;

    private readonly Lock _lock = new();

    private string _name = DefaultName;
    private bool _named;
    private decimal _size = DefaultSize;
    private bool _sizeIsDrawn = true;
    private short _weight = NormalWeight;
    private short _charset;
    private bool _italic;
    private bool _underline;
    private bool _strikethrough;

    /// <summary>
    /// Creates what <c>New StdFont</c> makes: a font that reads <c>MS Sans Serif</c> at 8.25 points, of weight 400 and character set 0,
    /// neither italic, underlined nor struck through, and whose name was never set.
    /// </summary>
    public StdFontObject()
    {
    }

    private StdFontObject(StdFontObject font)
    {
        _name = font._name;
        _named = font._named;
        _size = font._size;
        _sizeIsDrawn = font._sizeIsDrawn;
        _weight = font._weight;
        _charset = font._charset;
        _italic = font._italic;
        _underline = font._underline;
        _strikethrough = font._strikethrough;
    }

    /// <summary>
    /// Raised after a member's value changes, with the member's name: the type library's <c>FontChanged(PropertyName)</c>.
    /// </summary>
    /// <remarks>
    /// It is raised when a value set differs from the one held, and only then: setting the same value raises nothing, and reading
    /// <see cref="Size"/> raises nothing although it can snap the size held. It is raised synchronously, on the thread that set the value,
    /// after the value is stored.
    /// </remarks>
    public event EventHandler<StdFontChangedEventArgs>? Changed;

    /// <summary>
    /// The font's face name, which <c>Name</c>, the default member, reads: what was last set.
    /// </summary>
    public string Name
    {
        get
        {
            lock (_lock)
            {
                return _name;
            }
        }
    }

    /// <summary>
    /// Whether the font's name was ever set (<see cref="TrySetName"/>). A font never named reads <c>MS Sans Serif</c> but is drawn in a
    /// scalable face, at the sizes <see cref="DrawnSize"/> gives for no name.
    /// </summary>
    public bool IsNamed
    {
        get
        {
            lock (_lock)
            {
                return _named;
            }
        }
    }

    /// <summary>
    /// The font's size in points, which <c>Size</c> reads: the size it is drawn at (<see cref="DrawnSize"/>).
    /// </summary>
    /// <remarks>
    /// Reading it keeps the size it reads, which is the one a later name snaps from (<see cref="TrySetName"/>). It raises no
    /// <see cref="Changed"/>.
    /// </remarks>
    public decimal Size
    {
        get
        {
            lock (_lock)
            {
                if (!_sizeIsDrawn)
                {
                    _size = DrawnSize(_named ? _name : null, _size);
                    _sizeIsDrawn = true;
                }

                return _size;
            }
        }
    }

    /// <summary>
    /// The size the font is drawn at, in points: what <see cref="Size"/> reads, without keeping it.
    /// </summary>
    /// <remarks>
    /// This is the size for whoever draws the font or copies its values, where reading <see cref="Size"/> would change which size a later
    /// name snaps from: this read leaves the size <see cref="TrySetName"/> snaps from as it was, and raises no <see cref="Changed"/>.
    /// </remarks>
    public decimal SizeAsDrawn
    {
        get
        {
            lock (_lock)
            {
                return _sizeIsDrawn ? _size : DrawnSize(_named ? _name : null, _size);
            }
        }
    }

    /// <summary>
    /// The font's weight, which <c>Weight</c> reads: <see cref="NormalWeight"/> or <see cref="BoldWeight"/>.
    /// </summary>
    public short Weight
    {
        get
        {
            lock (_lock)
            {
                return _weight;
            }
        }
    }

    /// <summary>
    /// The font's character set, which <c>Charset</c> reads: what was last set, from 0 to 255.
    /// </summary>
    public short Charset
    {
        get
        {
            lock (_lock)
            {
                return _charset;
            }
        }
    }

    /// <summary>
    /// Whether the font is bold: its <see cref="Weight"/> is <see cref="BoldWeight"/>. Setting it sets the weight to
    /// <see cref="BoldWeight"/> or <see cref="NormalWeight"/>, whatever it was, and raises <see cref="Changed"/> with <c>Bold</c>.
    /// </summary>
    public bool Bold
    {
        get => Weight == BoldWeight;
        set => Change(nameof(Bold), ref _weight, value ? BoldWeight : NormalWeight);
    }

    /// <summary>
    /// Whether the font is italic.
    /// </summary>
    public bool Italic
    {
        get
        {
            lock (_lock)
            {
                return _italic;
            }
        }

        set => Change(nameof(Italic), ref _italic, value);
    }

    /// <summary>
    /// Whether the font is underlined.
    /// </summary>
    public bool Underline
    {
        get
        {
            lock (_lock)
            {
                return _underline;
            }
        }

        set => Change(nameof(Underline), ref _underline, value);
    }

    /// <summary>
    /// Whether the font is struck through.
    /// </summary>
    public bool Strikethrough
    {
        get
        {
            lock (_lock)
            {
                return _strikethrough;
            }
        }

        set => Change(nameof(Strikethrough), ref _strikethrough, value);
    }

    /// <summary>
    /// Sets the font's face name, as <c>Name</c> does.
    /// </summary>
    /// <remarks>
    /// The name is kept as it is given, in its case, whether or not a face of that name exists. The size held snaps to the new face when
    /// it is next read, even when the name is the one the font had.
    /// </remarks>
    /// <param name="name">The face name, of at most 31 characters; it can be empty.</param>
    /// <returns>
    /// <see cref="StdFontFailure.None"/> when the name is set; <see cref="StdFontFailure.InvalidPropertyValue"/>, with nothing changed, for a
    /// name that is too long.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public StdFontFailure TrySetName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length > MaxNameLength)
        {
            return StdFontFailure.InvalidPropertyValue;
        }

        bool changed;
        lock (_lock)
        {
            changed = !string.Equals(_name, name, StringComparison.Ordinal);
            _name = name;
            _named = true;
            _sizeIsDrawn = false;
        }

        RaiseIf(changed, nameof(Name));
        return StdFontFailure.None;
    }

    /// <summary>
    /// Sets the font's size in points, as <c>Size</c> does. The size is kept as it is given until it is read (<see cref="Size"/>).
    /// </summary>
    /// <param name="size">The size in points: more than 0 and less than 24576.</param>
    /// <returns>
    /// <see cref="StdFontFailure.None"/> when the size is set; otherwise, with nothing changed, <see cref="StdFontFailure.InvalidPropertyValue"/>
    /// for a size of 0 or less, and <see cref="StdFontFailure.Overflow"/> for one of 24576 or more.
    /// </returns>
    public StdFontFailure TrySetSize(decimal size)
    {
        if (size <= 0)
        {
            return StdFontFailure.InvalidPropertyValue;
        }

        if (size >= OverflowSize)
        {
            return StdFontFailure.Overflow;
        }

        bool changed;
        lock (_lock)
        {
            changed = _size != size;
            if (changed)
            {
                _size = size;
                _sizeIsDrawn = false;
            }
        }

        RaiseIf(changed, nameof(Size));
        return StdFontFailure.None;
    }

    /// <summary>
    /// Sets the font's weight, as <c>Weight</c> does: a weight up to 550 is stored as <see cref="NormalWeight"/>, and one above it as
    /// <see cref="BoldWeight"/>.
    /// </summary>
    /// <param name="weight">The weight, from 0 to 1000.</param>
    /// <returns>
    /// <see cref="StdFontFailure.None"/> when the weight is set; <see cref="StdFontFailure.InvalidPropertyValue"/>, with nothing changed, for a
    /// weight below 0 or above 1000.
    /// </returns>
    public StdFontFailure TrySetWeight(short weight)
    {
        if (weight is < 0 or > MaxWeight)
        {
            return StdFontFailure.InvalidPropertyValue;
        }

        var stored = weight > LastNormalWeight ? BoldWeight : NormalWeight;
        Change(nameof(Weight), ref _weight, stored);
        return StdFontFailure.None;
    }

    /// <summary>
    /// Sets the font's character set, as <c>Charset</c> does. It is kept as it is given, and changes neither <see cref="Name"/> nor the
    /// size.
    /// </summary>
    /// <param name="charset">The character set, from 0 to 255, as Windows numbers them: 0 is ANSI, 2 is Symbol.</param>
    /// <returns>
    /// <see cref="StdFontFailure.None"/> when the character set is set; <see cref="StdFontFailure.InvalidPropertyValue"/>, with nothing
    /// changed, for one below 0 or above 255.
    /// </returns>
    public StdFontFailure TrySetCharset(short charset)
    {
        if (charset is < 0 or > MaxCharset)
        {
            return StdFontFailure.InvalidPropertyValue;
        }

        Change(nameof(Charset), ref _charset, charset);
        return StdFontFailure.None;
    }

    /// <summary>
    /// Makes another font with this one's values, which a change to either does not change in the other: what <c>Ambient.Font</c> and
    /// <c>PropertyBag.ReadProperty</c> give, and what a control that copies a font it is given keeps.
    /// </summary>
    /// <returns>A new object, with no handler of <see cref="Changed"/>. A size that was not read yet still snaps when it is read.</returns>
    public StdFontObject Clone()
    {
        lock (_lock)
        {
            return new StdFontObject(this);
        }
    }

    /// <summary>
    /// The size a font of a face is drawn at, and so the size a font reads back, in points.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A font is drawn at a whole number of pixels, 96 to an inch, so at a multiple of 0.75 points: the size asked, in pixels and rounded
    /// half up, at least 2 pixels and at most 32767. <c>MS Sans Serif</c>, <c>MS Serif</c>, <c>Courier</c>, <c>System</c> and <c>Small Fonts</c> are
    /// bitmap fonts, each drawn at sizes of its own up to 160 pixels (153 for <c>Small Fonts</c>): <c>MS Sans Serif</c> asked for 8 points,
    /// about 10.7 pixels, is drawn at 11 pixels, 8.25 points. Past those sizes, they are drawn at whole pixels too. The sizes are those
    /// VB6 and Excel read back; the face name is compared ignoring case.
    /// </para>
    /// <para>
    /// A font never named (<see cref="IsNamed"/>) is drawn in the scalable face Windows maps no name to: at whole pixels too, but a size
    /// of 2 pixels is drawn at 3 (2.25 points), as measured in VB6 and Excel.
    /// </para>
    /// </remarks>
    /// <param name="name">The face name, or <see langword="null"/> for a font never named.</param>
    /// <param name="size">The size asked for, in points; more than 0.</param>
    /// <returns>The size the font is drawn at, in points.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is 0 or less.</exception>
    public static decimal DrawnSize(string? name, decimal size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        return (name is null ? UnnamedDrawnPixels(size) : DrawnPixels(name, size)) * 0.75m;
    }

    private static decimal DrawnPixels(string name, decimal size)
    {
        var pixels = Math.Round(size * 4 / 3, MidpointRounding.AwayFromZero);
        if (BitmapSizes.TryGetValue(name, out var sizes) && pixels <= sizes.Last)
        {
            var drawn = sizes.Runs[0].Drawn;
            foreach (var (from, at) in sizes.Runs)
            {
                if (from > pixels)
                {
                    break;
                }

                drawn = at;
            }

            return drawn;
        }

        return Math.Clamp(pixels, 2, MaxPixels);
    }

    // a font whose name was never set is drawn in the face Windows maps no name to: a scalable one, whose sizes of 1 pixel or less are
    // drawn at 2 pixels, and of 2 pixels at 3 (measured in VB6 and Excel).
    private static decimal UnnamedDrawnPixels(decimal size)
        => Math.Round(size * 4 / 3, MidpointRounding.AwayFromZero) switch
        {
            <= 1 => 2,
            2 => 3,
            > MaxPixels => MaxPixels,
            var pixels => pixels,
        };

    // stores a value under the lock and raises Changed outside it, when the value differs from the one held (a name, by ordinal).
    private void Change<T>(string member, ref T field, T value)
        where T : IEquatable<T>
    {
        bool changed;
        lock (_lock)
        {
            changed = !field.Equals(value);
            field = value;
        }

        RaiseIf(changed, member);
    }

    private void RaiseIf(bool changed, string member)
    {
        if (changed)
        {
            Changed?.Invoke(this, new StdFontChangedEventArgs(member));
        }
    }

    // each bitmap font's sizes in pixels as VB6 measures them, native and p-code agreeing: from each size asked in a run, up to the next
    // run's, the font is drawn at the run's size. The sizes do not always grow with the size asked: GDI scales a size of the font's
    // file to draw another, and MS Sans Serif asked for 143 pixels is drawn at 150, and asked for 144 at 144.
    private static readonly Dictionary<string, (int Last, (int From, int Drawn)[] Runs)> BitmapSizes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MS Sans Serif"] = (160,
        [
            (1, 11), (13, 13), (16, 16), (18, 18), (20, 20), (22, 22), (23, 23), (24, 24), (26, 26), (30, 30), (32, 32), (36, 36),
            (39, 39), (40, 40), (44, 44), (46, 46), (48, 48), (52, 52), (54, 54), (55, 55), (60, 60), (64, 64), (65, 65), (69, 69),
            (72, 72), (78, 80), (88, 90), (92, 92), (96, 96), (100, 100), (108, 108), (111, 115), (120, 120), (127, 128), (138, 138),
            (143, 150), (144, 144), (146, 150), (157, 160),
        ]),
        ["MS Serif"] = (160,
        [
            (1, 8), (9, 9), (11, 11), (13, 13), (16, 16), (18, 18), (20, 20), (22, 22), (23, 23), (24, 24), (26, 26), (27, 27),
            (30, 30), (32, 32), (36, 36), (39, 39), (40, 40), (44, 44), (45, 45), (46, 46), (48, 48), (52, 52), (54, 54), (55, 55),
            (60, 60), (64, 64), (65, 65), (69, 69), (72, 72), (78, 80), (88, 90), (92, 92), (96, 96), (100, 100), (108, 108),
            (111, 115), (120, 120), (127, 128), (138, 138), (143, 150), (144, 144), (146, 150), (157, 160),
        ]),
        ["Courier"] = (160,
        [
            (1, 13), (16, 16), (20, 20), (25, 25), (26, 26), (32, 32), (38, 39), (40, 40), (47, 48), (50, 50), (52, 52), (58, 60),
            (64, 64), (65, 65), (72, 75), (80, 80), (91, 91), (94, 100), (96, 96), (97, 100), (112, 112), (117, 120), (119, 125),
            (120, 120), (122, 125), (140, 140), (146, 150), (160, 160),
        ]),
        ["System"] = (160, [(1, 13), (23, 26), (36, 39), (49, 52), (62, 65), (75, 78), (88, 91), (101, 104)]),
        ["Small Fonts"] = (153,
        [
            (1, 3), (4, 4), (5, 5), (7, 7), (8, 8), (9, 9), (10, 10), (11, 11), (14, 14), (16, 16), (18, 18), (20, 20), (21, 21),
            (22, 22), (24, 24), (27, 27), (28, 28), (30, 30), (32, 32), (33, 33), (36, 36), (40, 40), (44, 44), (45, 45), (50, 50),
            (55, 55), (64, 60), (66, 66), (70, 70), (76, 77), (86, 88),
        ]),
    };
}

/// <summary>
/// The data of <see cref="StdFontObject.Changed"/>: the name of the member whose value changed.
/// </summary>
/// <param name="propertyName">
/// The member's name, as the type library's <c>FontChanged(PropertyName)</c> gives it: <c>Name</c>, <c>Size</c>, <c>Bold</c>, <c>Italic</c>,
/// <c>Underline</c>, <c>Strikethrough</c>, <c>Weight</c> or <c>Charset</c>.
/// </param>
public sealed class StdFontChangedEventArgs(string propertyName) : EventArgs
{
    /// <summary>
    /// The name of the member whose value changed: <c>Name</c>, <c>Size</c>, <c>Bold</c>, <c>Italic</c>, <c>Underline</c>,
    /// <c>Strikethrough</c>, <c>Weight</c> or <c>Charset</c>.
    /// </summary>
    public string PropertyName { get; } = propertyName;
}

/// <summary>
/// Why a <see cref="StdFontObject"/> refused a value: each value but <see cref="None"/> is the error number MS-VBA raises for it.
/// </summary>
public enum StdFontFailure
{
    /// <summary>
    /// The value is set.
    /// </summary>
    None = 0,

    /// <summary>
    /// The size is too large: run-time error 6, "Overflow".
    /// </summary>
    Overflow = 6,

    /// <summary>
    /// The value is out of the member's range, or the name is too long: run-time error 380, "Invalid property value".
    /// </summary>
    InvalidPropertyValue = 380,
}

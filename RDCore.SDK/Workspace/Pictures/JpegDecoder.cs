using System.Buffers.Binary;

namespace RDCore.SDK.Workspace.Pictures;

// A JPEG's pixels (ITU-T T.81): Huffman coding, baseline, extended of 8-bit samples or progressive, of 1 component (gray) or 3 (YCbCr, or RGB where an
// Adobe segment says so), with restart intervals. Each component's blocks are decoded to samples as they come (sequential) or kept as coefficients until
// the last scan (progressive); either way only for the block rows a scan reaches, so a small file that states a large picture costs no more than its
// data. Chroma at half the width, or half the width and height, is upsampled by the triangle filter; any other ratio repeats each sample.
internal static class JpegDecoder
{
    // more scans than any encoder writes: a cap on the work a file can ask for.
    private const int MaxScans = 256;

    // the coefficient each step of the zigzag order names, in the block's row-by-row order.
    private static readonly byte[] Natural =
    [
        0, 1, 8, 16, 9, 2, 3, 10, 17, 24, 32, 25, 18, 11, 4, 5, 12, 19, 26, 33, 40, 48, 41, 34, 27, 20, 13, 6, 7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36, 29, 22, 15, 23, 30, 37, 44, 51, 58, 59, 52, 45, 38, 31, 39, 46, 53, 60, 61, 54, 47, 55, 62, 63,
    ];

    // cos(kπ/16) for k = 0 to 8, in units of 1/8192.
    private static readonly int[] Cosines = [8192, 8035, 7568, 6811, 5793, 4551, 3135, 1598, 0];

    // the inverse DCT's basis: Basis[u * 8 + x] is C(u)·cos((2x + 1)uπ/16) in units of 1/8192, where C(0) is 1/√2 and C(u) is 1 otherwise.
    private static readonly int[] Basis = MakeBasis();

    private static int[] MakeBasis()
    {
        var basis = new int[64];
        for (var u = 0; u < 8; u++)
        {
            for (var x = 0; x < 8; x++)
            {
                basis[u * 8 + x] = u == 0 ? Cosines[4] : Cosine((2 * x + 1) * u);
            }
        }

        return basis;
    }

    // cos(mπ/16) for any m ≥ 0.
    private static int Cosine(int m)
    {
        m %= 32;
        return m switch
        {
            <= 8 => Cosines[m],
            <= 16 => -Cosines[16 - m],
            <= 24 => -Cosines[m - 16],
            _ => Cosines[32 - m],
        };
    }

    private sealed class Huffman
    {
        // per code length 1 to 16: the largest code of that length (-1 for none), and what to add to a code to find its value's place.
        private readonly int[] _maxCode = new int[17];
        private readonly int[] _offset = new int[17];
        private readonly byte[] _values;

        private Huffman(byte[] values) => _values = values;

        // the canonical code of the counts of each length and the values in code order; null where the counts need more codes than a length has.
        public static Huffman? Build(ReadOnlySpan<byte> counts, ReadOnlySpan<byte> values)
        {
            var table = new Huffman(values.ToArray());
            int code = 0, index = 0;
            for (var length = 1; length <= 16; length++)
            {
                int n = counts[length - 1];
                table._offset[length] = index - code;
                table._maxCode[length] = n > 0 ? code + n - 1 : -1;
                code += n;
                index += n;
                if (code > 1 << length)
                {
                    return null;
                }

                code <<= 1;
            }

            return table;
        }

        // the next value, or -1 where the data ends or holds no code of 16 bits or fewer.
        public int Decode(ref BitReader bits)
        {
            var code = 0;
            for (var length = 1; length <= 16; length++)
            {
                var bit = bits.Bit();
                if (bit < 0)
                {
                    return -1;
                }

                code = code << 1 | bit;
                if (code <= _maxCode[length])
                {
                    return _values[code + _offset[length]];
                }
            }

            return -1;
        }
    }

    // the bits of a scan's entropy-coded data, from its first byte to the marker that ends it: a 0xFF byte is followed by a 0 byte that is not data, and any
    // other marker ends the bits, until a restart moves past one.
    private ref struct BitReader(ReadOnlySpan<byte> data, int start, int end)
    {
        private readonly ReadOnlySpan<byte> _data = data;
        private readonly int _end = end;
        private int _at = start;
        private int _byte;
        private int _count;

        public bool Ended { get; private set; }

        public int Bit()
        {
            if (_count == 0 && !Fill())
            {
                Ended = true;
                return -1;
            }

            _count--;
            return _byte >> _count & 1;
        }

        public int Receive(int n)
        {
            var value = 0;
            for (var i = 0; i < n; i++)
            {
                value = value << 1 | (Bit() & 1);
            }

            return value;
        }

        // past the bits left in the byte, and the restart marker that should follow them; false where there is none.
        public bool Restart()
        {
            _count = 0;
            while (_at + 1 < _end && _data[_at] == 0xFF && _data[_at + 1] == 0xFF)
            {
                _at++;
            }

            if (_at + 1 < _end && _data[_at] == 0xFF && _data[_at + 1] is >= 0xD0 and <= 0xD7)
            {
                _at += 2;
                return true;
            }

            return false;
        }

        private bool Fill()
        {
            if (_at >= _end)
            {
                return false;
            }

            var b = _data[_at];
            if (b == 0xFF)
            {
                if (_at + 1 >= _end || _data[_at + 1] != 0)
                {
                    return false;
                }

                _at += 2;
            }
            else
            {
                _at++;
            }

            _byte = b;
            _count = 8;
            return true;
        }
    }

    private sealed class Component(int id, int h, int v, int table)
    {
        public int Id { get; } = id;
        public int H { get; } = h;
        public int V { get; } = v;
        public int Table { get; } = table;

        // the component's size in samples, its blocks, and its blocks padded to whole MCUs.
        public int Width { get; set; }
        public int Height { get; set; }
        public int RealBlocksWide { get; set; }
        public int RealBlocksHigh { get; set; }
        public int BlocksWide { get; set; }
        public int BlocksHigh { get; set; }

        // the quantization table latched at the component's first scan.
        public ushort[]? Quant { get; set; }

        // per block row: the coefficients of a progressive picture, the samples (8 rows of BlocksWide · 8), and whether each block was decoded.
        public short[]?[] Coefficients { get; set; } = [];
        public byte[]?[] Samples { get; set; } = [];
        public bool[]?[] Given { get; set; } = [];

        // the scan's state: the DC prediction and the Huffman tables.
        public int Prediction { get; set; }
        public Huffman? Dc { get; set; }
        public Huffman? Ac { get; set; }

        public byte[] SampleRow(int row) => Samples[row] ??= new byte[BlocksWide * 64];
        public bool[] GivenRow(int row) => Given[row] ??= new bool[BlocksWide];
        public short[] CoefficientRow(int row) => Coefficients[row] ??= new short[BlocksWide * 64];

        public bool IsGiven(int x, int y)
        {
            var row = y >> 3;
            return row < Given.Length && Given[row] is { } given && given[Math.Min(x >> 3, BlocksWide - 1)];
        }

        // a sample, clamped to the component's size; 128, a block of no coefficients, in a row never decoded.
        public int Sample(int x, int y)
        {
            x = Math.Clamp(x, 0, Width - 1);
            y = Math.Clamp(y, 0, Height - 1);
            return Samples[y >> 3] is { } row ? row[(y & 7) * BlocksWide * 8 + x] : 128;
        }
    }

    private sealed class Frame(Component[] components, bool progressive, int width, int height)
    {
        public Component[] Components { get; } = components;
        public bool Progressive { get; } = progressive;
        public int Width { get; } = width;
        public int Height { get; } = height;
        public int MaxH { get; set; }
        public int MaxV { get; set; }
        public int McusWide { get; set; }
        public int McusHigh { get; set; }
    }

    // the scan's state shared by its blocks.
    private sealed class Scan
    {
        public int Start { get; init; }
        public int End { get; init; }
        public int High { get; init; }
        public int Low { get; init; }
        public int EndOfBands { get; set; }
    }

    public static DecodedPicture? Decode(ReadOnlySpan<byte> b, int width, int height, out PictureDecodeFailure failure)
    {
        failure = PictureDecodeFailure.None;
        var quant = new ushort[]?[4];
        var dc = new Huffman?[4];
        var ac = new Huffman?[4];
        Frame? frame = null;
        int restart = 0, transform = -1, scans = 0;
        bool whole = true, ended = false;
        var at = 2;
        while (!ended)
        {
            if (at + 2 > b.Length || b[at] != 0xFF)
            {
                whole = false;
                break;
            }

            var code = b[at + 1];
            if (code == 0xFF)
            {
                at++;
                continue;
            }

            if (code == 0xD9)
            {
                ended = true;
                break;
            }

            if (code is >= 0xD0 and <= 0xD8 or 0x01)
            {
                at += 2;
                continue;
            }

            if (at + 4 > b.Length)
            {
                whole = false;
                break;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(b[(at + 2)..]);
            if (length < 2 || at + 2 + length > b.Length)
            {
                whole = false;
                break;
            }

            var segment = b.Slice(at + 4, length - 2);
            at += 2 + length;
            switch (code)
            {
                case 0xDB when !Quantization(segment, quant):
                case 0xC4 when !Tables(segment, dc, ac):
                case 0xDD when segment.Length < 2:
                    whole = false;
                    ended = true;
                    continue;
                case 0xDD:
                    restart = BinaryPrimitives.ReadUInt16BigEndian(segment);
                    break;
                case 0xEE when segment.Length >= 12 && segment[..5].SequenceEqual("Adobe"u8):
                    transform = segment[11];
                    break;
                case 0xC0 or 0xC1 or 0xC2 when frame is null:
                    frame = MakeFrame(segment, code == 0xC2, width, height, out failure);
                    if (frame is null)
                    {
                        return null;
                    }

                    break;
                case >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC):
                    if (frame is null)
                    {
                        failure = PictureDecodeFailure.NotDecoded;
                        return null;
                    }

                    // a second frame: the picture ends with the first.
                    whole = false;
                    ended = true;
                    continue;
                case 0xDA:
                    if (frame is null)
                    {
                        failure = PictureDecodeFailure.Refused;
                        return null;
                    }

                    var end = EntropyEnd(b, at);
                    if (++scans > MaxScans || !DecodeScan(segment, b, at, end, frame, quant, dc, ac, restart))
                    {
                        whole = false;
                    }

                    at = end;
                    break;
            }
        }

        if (frame is null)
        {
            failure = PictureDecodeFailure.Refused;
            return null;
        }

        if (frame.Progressive)
        {
            foreach (var component in frame.Components)
            {
                Transform(component);
            }
        }

        var bgra = Pixels(frame, transform, out var given);
        return new DecodedPicture(width, height, bgra, isComplete: whole && ended && given);
    }

    // the tables of a DQT segment, each in the coefficients' order: 64 values of 8 bits or of 16.
    private static bool Quantization(ReadOnlySpan<byte> s, ushort[]?[] quant)
    {
        var at = 0;
        while (at < s.Length)
        {
            int precision = s[at] >> 4, id = s[at] & 15;
            var size = precision == 0 ? 64 : 128;
            if (precision > 1 || id > 3 || at + 1 + size > s.Length)
            {
                return false;
            }

            var table = new ushort[64];
            for (var k = 0; k < 64; k++)
            {
                table[Natural[k]] = precision == 0 ? s[at + 1 + k] : BinaryPrimitives.ReadUInt16BigEndian(s[(at + 1 + k * 2)..]);
            }

            quant[id] = table;
            at += 1 + size;
        }

        return true;
    }

    // the tables of a DHT segment: a class (DC or AC), an id, 16 counts and the values.
    private static bool Tables(ReadOnlySpan<byte> s, Huffman?[] dc, Huffman?[] ac)
    {
        var at = 0;
        while (at < s.Length)
        {
            int kind = s[at] >> 4, id = s[at] & 15;
            if (kind > 1 || id > 3 || at + 17 > s.Length)
            {
                return false;
            }

            var counts = s.Slice(at + 1, 16);
            var total = 0;
            foreach (var count in counts)
            {
                total += count;
            }

            if (total > 256 || at + 17 + total > s.Length || Huffman.Build(counts, s.Slice(at + 17, total)) is not { } table)
            {
                return false;
            }

            (kind == 0 ? dc : ac)[id] = table;
            at += 17 + total;
        }

        return true;
    }

    // the frame header: 8-bit samples of the size the picture's header states, 1 or 3 components of sampling factors 1 to 4, each a whole fraction of the
    // largest.
    private static Frame? MakeFrame(ReadOnlySpan<byte> s, bool progressive, int width, int height, out PictureDecodeFailure failure)
    {
        failure = PictureDecodeFailure.Refused;
        if (s.Length < 6 || s.Length < 6 + 3 * s[5] || s[5] == 0)
        {
            return null;
        }

        int precision = s[0], count = s[5];
        if (BinaryPrimitives.ReadUInt16BigEndian(s[1..]) != height || BinaryPrimitives.ReadUInt16BigEndian(s[3..]) != width)
        {
            return null;
        }

        var components = new Component[count];
        for (var i = 0; i < count; i++)
        {
            var at = 6 + i * 3;
            int id = s[at], h = s[at + 1] >> 4, v = s[at + 1] & 15, table = s[at + 2];
            if (h is < 1 or > 4 || v is < 1 or > 4 || table > 3 || components.Take(i).Any(c => c.Id == id))
            {
                return null;
            }

            components[i] = new Component(id, h, v, table);
        }

        failure = PictureDecodeFailure.NotDecoded;
        var frame = new Frame(components, progressive, width, height)
        {
            MaxH = components.Max(c => c.H),
            MaxV = components.Max(c => c.V),
        };
        if (precision != 8 || count is not (1 or 3) || height == 0 || components.Any(c => frame.MaxH % c.H != 0 || frame.MaxV % c.V != 0))
        {
            return null;
        }

        frame.McusWide = (width + 8 * frame.MaxH - 1) / (8 * frame.MaxH);
        frame.McusHigh = (height + 8 * frame.MaxV - 1) / (8 * frame.MaxV);
        foreach (var c in components)
        {
            c.Width = (width * c.H + frame.MaxH - 1) / frame.MaxH;
            c.Height = (height * c.V + frame.MaxV - 1) / frame.MaxV;
            c.RealBlocksWide = (c.Width + 7) / 8;
            c.RealBlocksHigh = (c.Height + 7) / 8;
            c.BlocksWide = frame.McusWide * c.H;
            c.BlocksHigh = frame.McusHigh * c.V;
            c.Samples = new byte[]?[c.BlocksHigh];
            c.Given = new bool[]?[c.BlocksHigh];
            c.Coefficients = progressive ? new short[]?[c.BlocksHigh] : [];
        }

        failure = PictureDecodeFailure.None;
        return frame;
    }

    // where a scan's entropy-coded data ends: at the first marker that is neither a stuffed 0 nor a restart.
    private static int EntropyEnd(ReadOnlySpan<byte> b, int at)
    {
        for (var i = at; i + 1 < b.Length; i++)
        {
            if (b[i] == 0xFF && b[i + 1] != 0 && b[i + 1] is not (>= 0xD0 and <= 0xD7))
            {
                return i;
            }
        }

        return b.Length;
    }

    // one scan, to its end; false where its header is not one the frame takes, or its data ends or breaks before its last block.
    private static bool DecodeScan(ReadOnlySpan<byte> s, ReadOnlySpan<byte> b, int start, int end, Frame frame, ushort[]?[] quant, Huffman?[] dc, Huffman?[] ac,
        int restart)
    {
        if (s.Length < 1 || s[0] is < 1 or > 4 || s.Length < 4 + 2 * s[0])
        {
            return false;
        }

        var count = s[0];
        var components = new Component[count];
        for (var i = 0; i < count; i++)
        {
            int id = s[1 + i * 2], tables = s[2 + i * 2];
            if (frame.Components.FirstOrDefault(c => c.Id == id) is not { } component || (tables >> 4) > 3 || (tables & 15) > 3 || components.Contains(component))
            {
                return false;
            }

            component.Dc = dc[tables >> 4];
            component.Ac = ac[tables & 15];
            component.Prediction = 0;
            component.Quant ??= quant[component.Table];
            if (component.Quant is null)
            {
                return false;
            }

            components[i] = component;
        }

        var after = 1 + count * 2;
        var scan = frame.Progressive
            ? new Scan { Start = s[after], End = s[after + 1], High = s[after + 2] >> 4, Low = s[after + 2] & 15 }
            : new Scan { Start = 0, End = 63 };
        if (frame.Progressive && (scan.Start > scan.End || scan.End > 63 || (scan.Start == 0) != (scan.End == 0) || scan.Start > 0 && count > 1 || scan.Low > 13
            || scan.High > 13))
        {
            return false;
        }

        foreach (var c in components)
        {
            var needsDc = !frame.Progressive || scan.Start == 0 && scan.High == 0;
            var needsAc = !frame.Progressive || scan.Start > 0;
            if (needsDc && c.Dc is null || needsAc && c.Ac is null)
            {
                return false;
            }
        }

        var bits = new BitReader(b, start, end);
        var single = count == 1;
        var mcus = single ? components[0].RealBlocksWide * components[0].RealBlocksHigh : frame.McusWide * frame.McusHigh;
        for (var m = 0; m < mcus; m++)
        {
            if (restart > 0 && m > 0 && m % restart == 0)
            {
                if (!bits.Restart())
                {
                    return false;
                }

                foreach (var c in components)
                {
                    c.Prediction = 0;
                }

                scan.EndOfBands = 0;
            }

            if (single)
            {
                var c = components[0];
                if (!Block(ref bits, frame, scan, c, m % c.RealBlocksWide, m / c.RealBlocksWide))
                {
                    return false;
                }

                continue;
            }

            foreach (var c in components)
            {
                for (var v = 0; v < c.V; v++)
                {
                    for (var h = 0; h < c.H; h++)
                    {
                        if (!Block(ref bits, frame, scan, c, m % frame.McusWide * c.H + h, m / frame.McusWide * c.V + v))
                        {
                            return false;
                        }
                    }
                }
            }
        }

        return true;
    }

    private static bool Block(ref BitReader bits, Frame frame, Scan scan, Component c, int x, int y)
    {
        if (!frame.Progressive)
        {
            Span<short> block = stackalloc short[64];
            block.Clear();
            if (!Sequential(ref bits, c, block))
            {
                return false;
            }

            Idct(block, c.Quant!, c.SampleRow(y).AsSpan(x * 8), c.BlocksWide * 8);
            c.GivenRow(y)[x] = true;
            return true;
        }

        var coefficients = c.CoefficientRow(y).AsSpan(x * 64, 64);
        if (scan.Start == 0)
        {
            if (scan.High != 0)
            {
                var bit = bits.Bit();
                if (bit < 0)
                {
                    return false;
                }

                coefficients[0] = (short)(coefficients[0] | bit << scan.Low);
                return true;
            }

            if (!Difference(ref bits, c.Dc!, out var difference))
            {
                return false;
            }

            c.Prediction = Math.Clamp(c.Prediction + difference, short.MinValue, short.MaxValue);
            coefficients[0] = Clamp(c.Prediction * (1L << scan.Low));
            c.GivenRow(y)[x] = true;
            return true;
        }

        return scan.High == 0 ? FirstBand(ref bits, c.Ac!, scan, coefficients) : Refinement(ref bits, c.Ac!, scan, coefficients);
    }

    private static short Clamp(long value) => (short)Math.Clamp(value, short.MinValue, short.MaxValue);

    // a value of the given size in bits, as the code that follows a symbol holds it: the low half of the range is the negative values.
    private static int Extend(int value, int size) => size == 0 ? 0 : value < 1 << (size - 1) ? value - (1 << size) + 1 : value;

    private static bool Difference(ref BitReader bits, Huffman table, out int difference)
    {
        difference = 0;
        var size = table.Decode(ref bits);
        if (size is < 0 or > 15)
        {
            return false;
        }

        difference = Extend(bits.Receive(size), size);
        return !bits.Ended;
    }

    // a block of a sequential scan: its DC difference, then its AC coefficients in zigzag order, with runs of zeros, to the end of the block.
    private static bool Sequential(ref BitReader bits, Component c, scoped Span<short> block)
    {
        if (!Difference(ref bits, c.Dc!, out var difference))
        {
            return false;
        }

        c.Prediction = Math.Clamp(c.Prediction + difference, short.MinValue, short.MaxValue);
        block[0] = (short)c.Prediction;
        for (var k = 1; k < 64;)
        {
            var symbol = c.Ac!.Decode(ref bits);
            if (symbol < 0)
            {
                return false;
            }

            int run = symbol >> 4, size = symbol & 15;
            if (size == 0)
            {
                if (run != 15)
                {
                    break;
                }

                k += 16;
                continue;
            }

            k += run;
            if (k > 63)
            {
                return false;
            }

            block[Natural[k]] = (short)Extend(bits.Receive(size), size);
            if (bits.Ended)
            {
                return false;
            }

            k++;
        }

        return true;
    }

    // a progressive scan's first pass over a band of AC coefficients: each value scaled by its point transform, and runs of blocks that hold no more.
    private static bool FirstBand(ref BitReader bits, Huffman table, Scan scan, Span<short> block)
    {
        if (scan.EndOfBands > 0)
        {
            scan.EndOfBands--;
            return true;
        }

        for (var k = scan.Start; k <= scan.End; k++)
        {
            var symbol = table.Decode(ref bits);
            if (symbol < 0)
            {
                return false;
            }

            int run = symbol >> 4, size = symbol & 15;
            if (size != 0)
            {
                k += run;
                if (k > scan.End)
                {
                    return false;
                }

                block[Natural[k]] = Clamp(Extend(bits.Receive(size), size) * (1L << scan.Low));
                if (bits.Ended)
                {
                    return false;
                }
            }
            else if (run == 15)
            {
                k += 15;
            }
            else
            {
                scan.EndOfBands = (1 << run) + (run > 0 ? bits.Receive(run) : 0) - 1;
                return !bits.Ended;
            }
        }

        return true;
    }

    // a progressive scan's refinement of a band: one more bit of each coefficient already nonzero, and the coefficients that become nonzero, each of the
    // size of the bit refined.
    private static bool Refinement(ref BitReader bits, Huffman table, Scan scan, Span<short> block)
    {
        int plus = 1 << scan.Low, minus = -1 << scan.Low;
        var k = scan.Start;
        if (scan.EndOfBands == 0)
        {
            for (; k <= scan.End; k++)
            {
                var symbol = table.Decode(ref bits);
                if (symbol < 0)
                {
                    return false;
                }

                int run = symbol >> 4, size = symbol & 15, value = 0;
                if (size != 0)
                {
                    var sign = bits.Bit();
                    if (size != 1 || sign < 0)
                    {
                        return false;
                    }

                    value = sign != 0 ? plus : minus;
                }
                else if (run != 15)
                {
                    scan.EndOfBands = (1 << run) + (run > 0 ? bits.Receive(run) : 0);
                    if (bits.Ended)
                    {
                        return false;
                    }

                    break;
                }

                // past the coefficients already nonzero, refining each, and past as many zero ones as the run says.
                for (; k <= scan.End; k++)
                {
                    ref var coefficient = ref block[Natural[k]];
                    if (coefficient != 0)
                    {
                        if (!Refine(ref bits, ref coefficient, plus, minus))
                        {
                            return false;
                        }
                    }
                    else if (--run < 0)
                    {
                        break;
                    }
                }

                if (value != 0)
                {
                    if (k > scan.End)
                    {
                        return false;
                    }

                    block[Natural[k]] = (short)value;
                }
            }
        }

        if (scan.EndOfBands > 0)
        {
            for (; k <= scan.End; k++)
            {
                ref var coefficient = ref block[Natural[k]];
                if (coefficient != 0 && !Refine(ref bits, ref coefficient, plus, minus))
                {
                    return false;
                }
            }

            scan.EndOfBands--;
        }

        return true;
    }

    // a bit that adds the refined bit to a nonzero coefficient's magnitude, where it is not there yet.
    private static bool Refine(ref BitReader bits, ref short coefficient, int plus, int minus)
    {
        var bit = bits.Bit();
        if (bit < 0)
        {
            return false;
        }

        if (bit != 0 && (coefficient & plus) == 0)
        {
            coefficient = Clamp(coefficient + (coefficient >= 0 ? plus : minus));
        }

        return true;
    }

    // a progressive picture's coefficients, once its scans are read, to samples: each block a scan reached.
    private static void Transform(Component c)
    {
        for (var row = 0; row < c.Coefficients.Length; row++)
        {
            if (c.Coefficients[row] is not { } coefficients || c.Given[row] is not { } given)
            {
                continue;
            }

            var samples = c.SampleRow(row);
            for (var x = 0; x < c.BlocksWide; x++)
            {
                if (given[x])
                {
                    Idct(coefficients.AsSpan(x * 64, 64), c.Quant!, samples.AsSpan(x * 8), c.BlocksWide * 8);
                }
            }

            c.Coefficients[row] = null;
        }
    }

    // the inverse DCT of a block of quantized coefficients, rounded and level-shifted to samples of 0 to 255, rows of 8 written stride apart.
    private static void Idct(ReadOnlySpan<short> block, ushort[] quant, Span<byte> output, int stride)
    {
        Span<long> rows = stackalloc long[64];
        for (var v = 0; v < 8; v++)
        {
            for (var x = 0; x < 8; x++)
            {
                long sum = 0;
                for (var u = 0; u < 8; u++)
                {
                    // a coefficient past what 8-bit samples give is cut to 16 bits, so that the sums stay in range.
                    sum += Basis[u * 8 + x] * Math.Clamp(block[v * 8 + u] * quant[v * 8 + u], short.MinValue, short.MaxValue);
                }

                rows[v * 8 + x] = sum;
            }
        }

        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                long sum = 0;
                for (var v = 0; v < 8; v++)
                {
                    sum += Basis[v * 8 + y] * rows[v * 8 + x];
                }

                output[y * stride + x] = (byte)Math.Clamp(((sum + (1L << 27)) >> 28) + 128, 0, 255);
            }
        }
    }

    // the picture's BGRA: each component's samples upsampled to the picture's size, then gray, YCbCr or RGB to color. A pixel is given where every
    // component's block that covers it was decoded; any other pixel is 0.
    private static byte[] Pixels(Frame frame, int transform, out bool given)
    {
        var components = frame.Components;
        var bgra = new byte[frame.Width * frame.Height * 4];
        var rows = components.Select(_ => new int[frame.Width + 1]).ToArray();
        var known = components.Select(_ => new bool[frame.Width]).ToArray();
        var rgb = components.Length == 3 && (transform == 0 || transform < 0 && components.Select(c => c.Id).SequenceEqual(new[] { 'R', 'G', 'B' }.Select(c => (int)c)));
        given = true;
        for (var y = 0; y < frame.Height; y++)
        {
            for (var i = 0; i < components.Length; i++)
            {
                Upsampled(frame, components[i], y, rows[i], known[i]);
            }

            for (var x = 0; x < frame.Width; x++)
            {
                var decoded = true;
                for (var i = 0; i < known.Length; i++)
                {
                    decoded &= known[i][x];
                }

                if (!decoded)
                {
                    given = false;
                    continue;
                }

                int r, g, b;
                if (components.Length == 1)
                {
                    r = g = b = rows[0][x];
                }
                else if (rgb)
                {
                    (r, g, b) = (rows[0][x], rows[1][x], rows[2][x]);
                }
                else
                {
                    int luma = rows[0][x], cb = rows[1][x] - 128, cr = rows[2][x] - 128;
                    r = luma + ((91881 * cr + 32768) >> 16);
                    g = luma + ((-22554 * cb - 46802 * cr + 32768) >> 16);
                    b = luma + ((116130 * cb + 32768) >> 16);
                }

                var at = (y * frame.Width + x) * 4;
                bgra[at] = (byte)Math.Clamp(b, 0, 255);
                bgra[at + 1] = (byte)Math.Clamp(g, 0, 255);
                bgra[at + 2] = (byte)Math.Clamp(r, 0, 255);
                bgra[at + 3] = 0xFF;
            }
        }

        return bgra;
    }

    // a component's samples for one row of the picture: at half the width (and height), each sample between its neighbors by the triangle filter;
    // otherwise each sample repeated.
    private static void Upsampled(Frame frame, Component c, int y, int[] row, bool[] known)
    {
        int ratioX = frame.MaxH / c.H, ratioY = frame.MaxV / c.V, width = frame.Width, n = c.Width;
        var cy = y / ratioY;
        for (var x = 0; x < width; x++)
        {
            known[x] = c.IsGiven(x / ratioX, cy);
        }

        if (ratioX != 2 || ratioY > 2 || n < 2)
        {
            for (var x = 0; x < width; x++)
            {
                row[x] = c.Sample(x / ratioX, cy);
            }

            return;
        }

        // at half the height, the column sums: 3 parts of this row and 1 of the nearer one, above for an upper row and below for a lower one, in 16ths
        // with the even outputs rounded up from a half and the odd ones from a little more. At the full height, the samples, in quarters, the even outputs
        // rounded up from three quarters and the odd ones from a half. The first and the last output are their sample's.
        var half = ratioY == 2;
        var near = y % 2 == 0 ? cy - 1 : cy + 1;
        for (var i = 0; i < n && 2 * i < width; i++)
        {
            int here = Sum(i), left = i > 0 ? Sum(i - 1) : here, right = i < n - 1 ? Sum(i + 1) : here;
            row[2 * i] = half
                ? i == 0 ? (4 * here + 8) >> 4 : (3 * here + left + 8) >> 4
                : i == 0 ? here : (3 * here + left + 1) >> 2;
            if (2 * i + 1 < width)
            {
                row[2 * i + 1] = half
                    ? i == n - 1 ? (4 * here + 7) >> 4 : (3 * here + right + 7) >> 4
                    : i == n - 1 ? here : (3 * here + right + 2) >> 2;
            }
        }

        int Sum(int i) => half ? 3 * c.Sample(i, cy) + c.Sample(i, near) : c.Sample(i, cy);
    }
}

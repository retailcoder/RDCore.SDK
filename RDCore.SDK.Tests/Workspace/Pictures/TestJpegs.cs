namespace RDCore.Tests.Workspace.Pictures;

/// <summary>
/// A JPEG encoder for the decoder's tests (ITU-T T.81, Huffman coding): each component's samples to quantized DCT coefficients, then either one
/// sequential scan of every component (or one each), or a progressive script of DC first and refinement scans and AC band and refinement scans,
/// with restart intervals. Every scan has its own Huffman tables, of one code length for each symbol it uses.
/// </summary>
public static class TestJpegs
{
    // the coefficient each step of the zigzag order names.
    private static readonly byte[] Natural =
    [
        0, 1, 8, 16, 9, 2, 3, 10, 17, 24, 32, 25, 18, 11, 4, 5, 12, 19, 26, 33, 40, 48, 41, 34, 27, 20, 13, 6, 7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36, 29, 22, 15, 23, 30, 37, 44, 51, 58, 59, 52, 45, 38, 31, 39, 46, 53, 60, 61, 54, 47, 55, 62, 63,
    ];

    /// <summary>
    /// A component: its sampling factors, and its samples at its own size, row by row.
    /// </summary>
    public sealed record Plane(int H, int V, byte[] Samples);

    /// <summary>
    /// How the picture is coded.
    /// </summary>
    public sealed record Options
    {
        public bool Progressive { get; init; }

        public int Restart { get; init; }

        public int Quant { get; init; } = 1;

        public bool Interleaved { get; init; } = true;

        public int Adobe { get; init; } = -1;

        public int[]? Ids { get; init; }
    }

    private sealed class Component
    {
        public int Id, H, V, BlocksWide, BlocksHigh, RealBlocksWide, RealBlocksHigh, Prediction;
        public int[][] Blocks = [];
        public int[] Block(int x, int y) => Blocks[y * BlocksWide + x];
    }

    // a component's size in samples, as the decoder takes it.
    public static (int Width, int Height) SizeOf(int width, int height, int h, int v, int maxH, int maxV)
        => ((width * h + maxH - 1) / maxH, (height * v + maxV - 1) / maxV);

    // the samples of a pattern of strong edges and gradients, the same for the same arguments.
    public static byte[] Pattern(int width, int height, int seed)
    {
        var samples = new byte[width * height];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                samples[y * width + x] = (byte)((x * 37 + y * 91 + seed * 53 + x * y * 7 % 31 + ((x / 3 + y / 5 + seed) % 2) * 90) & 0xFF);
        return samples;
    }

    public static byte[] Flat(int width, int height, byte value) => Enumerable.Repeat(value, width * height).ToArray();

    public static byte[] Jpeg(int width, int height, Plane[] planes, Options? options = null)
    {
        options ??= new Options();
        int maxH = planes.Max(p => p.H), maxV = planes.Max(p => p.V);
        int mcusWide = (width + 8 * maxH - 1) / (8 * maxH), mcusHigh = (height + 8 * maxV - 1) / (8 * maxV);
        var components = planes.Select((plane, i) =>
        {
            var (w, h) = SizeOf(width, height, plane.H, plane.V, maxH, maxV);
            var c = new Component
            {
                Id = options.Ids?[i] ?? i + 1, H = plane.H, V = plane.V, BlocksWide = mcusWide * plane.H, BlocksHigh = mcusHigh * plane.V,
                RealBlocksWide = (w + 7) / 8, RealBlocksHigh = (h + 7) / 8,
            };
            c.Blocks = new int[c.BlocksWide * c.BlocksHigh][];
            for (var by = 0; by < c.BlocksHigh; by++)
                for (var bx = 0; bx < c.BlocksWide; bx++)
                    c.Blocks[by * c.BlocksWide + bx] = Forward(plane.Samples, w, h, bx, by, options.Quant);
            return c;
        }).ToArray();

        var o = new List<byte> { 0xFF, 0xD8 };
        if (options.Adobe >= 0)
            o.AddRange([0xFF, 0xEE, 0, 14, (byte)'A', (byte)'d', (byte)'o', (byte)'b', (byte)'e', 0, 100, 0, 0, 0, 0, (byte)options.Adobe]);
        else
            o.AddRange([0xFF, 0xE0, 0, 16, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 1, 0, 0, 1, 0, 1, 0, 0]);
        o.AddRange([0xFF, 0xDB, 0, 67, 0]);
        o.AddRange(Enumerable.Repeat((byte)options.Quant, 64));
        o.AddRange([0xFF, options.Progressive ? (byte)0xC2 : (byte)0xC0, 0, (byte)(8 + 3 * planes.Length), 8, (byte)(height >> 8), (byte)height,
            (byte)(width >> 8), (byte)width, (byte)planes.Length]);
        foreach (var c in components)
            o.AddRange([(byte)c.Id, (byte)(c.H << 4 | c.V), 0]);
        if (options.Restart > 0)
            o.AddRange([0xFF, 0xDD, 0, 4, (byte)(options.Restart >> 8), (byte)options.Restart]);

        void Scan(Component[] scanned, int start, int end, int high, int low)
        {
            var counting = new Emitter(null, null);
            Encode(counting, scanned, start, end, high, low, mcusWide, mcusHigh, options.Restart);
            var dc = Codes(counting.DcUsed);
            var ac = Codes(counting.AcUsed);
            foreach (var (kind, table) in new[] { (0, dc), (1, ac) })
            {
                if (table.Count == 0)
                    continue;
                var symbols = table.Keys.OrderBy(s => s).ToArray();
                var length = table[symbols[0]].Length;
                o.AddRange([0xFF, 0xC4, (byte)((19 + symbols.Length) >> 8), (byte)(19 + symbols.Length), (byte)(kind << 4)]);
                for (var l = 1; l <= 16; l++)
                    o.Add(l == length ? (byte)symbols.Length : (byte)0);
                o.AddRange(symbols.Select(s => (byte)s));
            }

            o.AddRange([0xFF, 0xDA, 0, (byte)(6 + 2 * scanned.Length), (byte)scanned.Length]);
            foreach (var c in scanned)
                o.AddRange([(byte)c.Id, 0]);
            o.AddRange([(byte)start, (byte)end, (byte)(high << 4 | low)]);
            var writing = new Emitter(dc, ac);
            Encode(writing, scanned, start, end, high, low, mcusWide, mcusHigh, options.Restart);
            writing.Pad();
            o.AddRange(writing.Bytes);
        }

        if (!options.Progressive)
        {
            if (options.Interleaved)
                Scan(components, 0, 63, 0, 0);
            else
                foreach (var c in components)
                    Scan([c], 0, 63, 0, 0);
        }
        else if (components.Length == 1)
        {
            var y = components;
            Scan(y, 0, 0, 0, 1);
            Scan(y, 1, 5, 0, 2);
            Scan(y, 6, 63, 0, 2);
            Scan(y, 1, 63, 2, 1);
            Scan(y, 0, 0, 1, 0);
            Scan(y, 1, 63, 1, 0);
        }
        else
        {
            Scan(components, 0, 0, 0, 1);
            Scan([components[0]], 1, 5, 0, 2);
            Scan([components[2]], 1, 63, 0, 1);
            Scan([components[1]], 1, 63, 0, 1);
            Scan([components[0]], 6, 63, 0, 2);
            Scan([components[0]], 1, 63, 2, 1);
            Scan(components, 0, 0, 1, 0);
            Scan([components[2]], 1, 63, 1, 0);
            Scan([components[1]], 1, 63, 1, 0);
            Scan([components[0]], 1, 63, 1, 0);
        }

        o.AddRange([0xFF, 0xD9]);
        return [.. o];
    }

    // a block's DCT, each coefficient divided by the quantizer and rounded, the samples past the component's edge repeating its last ones.
    private static int[] Forward(byte[] samples, int width, int height, int bx, int by, int quant)
    {
        var block = new int[64];
        for (var v = 0; v < 8; v++)
            for (var u = 0; u < 8; u++)
            {
                var sum = 0.0;
                for (var y = 0; y < 8; y++)
                    for (var x = 0; x < 8; x++)
                    {
                        var f = samples[Math.Min(by * 8 + y, height - 1) * width + Math.Min(bx * 8 + x, width - 1)] - 128.0;
                        sum += f * Math.Cos((2 * x + 1) * u * Math.PI / 16) * Math.Cos((2 * y + 1) * v * Math.PI / 16);
                    }

                var scale = (u == 0 ? Math.Sqrt(0.5) : 1) * (v == 0 ? Math.Sqrt(0.5) : 1) / 4;
                block[v * 8 + u] = (int)Math.Round(sum * scale / quant, MidpointRounding.AwayFromZero);
            }

        return block;
    }

    // codes of one length for the symbols used: the shortest that leaves the code of all 1 bits unused.
    private static Dictionary<int, (int Code, int Length)> Codes(SortedSet<int> used)
    {
        var length = 1;
        while (1 << length < used.Count + 1)
            length++;
        return used.Select((symbol, i) => (symbol, i)).ToDictionary(p => p.symbol, p => (p.i, length));
    }

    private static int Size(int value)
    {
        value = Math.Abs(value);
        var size = 0;
        while (value > 0)
        {
            size++;
            value >>= 1;
        }

        return size;
    }

    // a value of the given size as the code that follows a symbol holds it: a negative one as its one's complement.
    private static int Bits(int value, int size) => value >= 0 ? value : value + (1 << size) - 1;

    private sealed class Emitter(Dictionary<int, (int Code, int Length)>? dc, Dictionary<int, (int Code, int Length)>? ac)
    {
        private int _buffer, _count;

        public SortedSet<int> DcUsed { get; } = [];
        public SortedSet<int> AcUsed { get; } = [];
        public List<byte> Bytes { get; } = [];
        public int EndOfBands;
        public List<int> Corrections = [];

        public void Dc(int symbol)
        {
            DcUsed.Add(symbol);
            if (dc is not null)
                Put(dc[symbol].Code, dc[symbol].Length);
        }

        public void Ac(int symbol)
        {
            AcUsed.Add(symbol);
            if (ac is not null)
                Put(ac[symbol].Code, ac[symbol].Length);
        }

        public void Put(int value, int count)
        {
            if (dc is null && ac is null)
                return;
            for (var i = count - 1; i >= 0; i--)
            {
                _buffer = _buffer << 1 | (value >> i & 1);
                if (++_count == 8)
                {
                    Bytes.Add((byte)_buffer);
                    if (_buffer == 0xFF)
                        Bytes.Add(0);
                    _buffer = _count = 0;
                }
            }
        }

        public void Pad()
        {
            if (_count > 0)
                Put(0xFF, 8 - _count);
        }

        public void Marker(int restart)
        {
            Pad();
            if (dc is not null || ac is not null)
                Bytes.AddRange([0xFF, (byte)(0xD0 + restart % 8)]);
        }

        // the pending run of blocks with no more coefficients in the band, and the correction bits of those blocks.
        public void FlushEndOfBands()
        {
            if (EndOfBands > 0)
            {
                var size = Size(EndOfBands) - 1;
                Ac(size << 4);
                Put(EndOfBands & ((1 << size) - 1), size);
                EndOfBands = 0;
            }

            foreach (var bit in Corrections)
                Put(bit, 1);
            Corrections.Clear();
        }
    }

    private static void Encode(Emitter e, Component[] scanned, int start, int end, int high, int low, int mcusWide, int mcusHigh, int restart)
    {
        foreach (var c in scanned)
            c.Prediction = 0;
        e.EndOfBands = 0;
        var single = scanned.Length == 1;
        var mcus = single ? scanned[0].RealBlocksWide * scanned[0].RealBlocksHigh : mcusWide * mcusHigh;
        var restarts = 0;
        for (var m = 0; m < mcus; m++)
        {
            if (restart > 0 && m > 0 && m % restart == 0)
            {
                e.FlushEndOfBands();
                e.Marker(restarts++);
                foreach (var c in scanned)
                    c.Prediction = 0;
            }

            if (single)
            {
                var c = scanned[0];
                Block(e, c, c.Block(m % c.RealBlocksWide, m / c.RealBlocksWide), start, end, high, low);
                continue;
            }

            foreach (var c in scanned)
                for (var v = 0; v < c.V; v++)
                    for (var h = 0; h < c.H; h++)
                        Block(e, c, c.Block(m % mcusWide * c.H + h, m / mcusWide * c.V + v), start, end, high, low);
        }

        e.FlushEndOfBands();
    }

    private static void Block(Emitter e, Component c, int[] block, int start, int end, int high, int low)
    {
        if (start == 0 && end == 63)
        {
            Difference(e, c, block[0]);
            var run = 0;
            for (var k = 1; k < 64; k++)
            {
                var value = block[Natural[k]];
                if (value == 0)
                {
                    run++;
                    continue;
                }

                for (; run > 15; run -= 16)
                    e.Ac(0xF0);
                var size = Size(value);
                e.Ac(run << 4 | size);
                e.Put(Bits(value, size), size);
                run = 0;
            }

            if (run > 0)
                e.Ac(0);
        }
        else if (start == 0)
        {
            if (high == 0)
                Difference(e, c, block[0] >> low);
            else
                e.Put(block[0] >> low & 1, 1);
        }
        else if (high == 0)
        {
            FirstBand(e, block, start, end, low);
        }
        else
        {
            Refinement(e, block, start, end, low);
        }
    }

    private static void Difference(Emitter e, Component c, int value)
    {
        var difference = value - c.Prediction;
        c.Prediction = value;
        var size = Size(difference);
        e.Dc(size);
        e.Put(Bits(difference, size), size);
    }

    private static void FirstBand(Emitter e, int[] block, int start, int end, int low)
    {
        var run = 0;
        for (var k = start; k <= end; k++)
        {
            var value = block[Natural[k]];
            var magnitude = Math.Abs(value) >> low;
            if (magnitude == 0)
            {
                run++;
                continue;
            }

            e.FlushEndOfBands();
            for (; run > 15; run -= 16)
                e.Ac(0xF0);
            var size = Size(magnitude);
            e.Ac(run << 4 | size);
            e.Put(value < 0 ? ~magnitude & ((1 << size) - 1) : magnitude, size);
            run = 0;
        }

        if (run > 0 && ++e.EndOfBands == 0x7FFF)
            e.FlushEndOfBands();
    }

    private static void Refinement(Emitter e, int[] block, int start, int end, int low)
    {
        var magnitudes = new int[64];
        var last = start - 1;
        for (var k = start; k <= end; k++)
        {
            magnitudes[k] = Math.Abs(block[Natural[k]]) >> low;
            if (magnitudes[k] == 1)
                last = k;
        }

        var run = 0;
        var corrections = new List<int>();
        for (var k = start; k <= end; k++)
        {
            var magnitude = magnitudes[k];
            if (magnitude == 0)
            {
                run++;
                continue;
            }

            for (; run > 15 && k <= last; run -= 16)
            {
                e.FlushEndOfBands();
                e.Ac(0xF0);
                corrections.ForEach(bit => e.Put(bit, 1));
                corrections.Clear();
            }

            if (magnitude > 1)
            {
                corrections.Add(magnitude & 1);
                continue;
            }

            e.FlushEndOfBands();
            e.Ac(run << 4 | 1);
            e.Put(block[Natural[k]] < 0 ? 0 : 1, 1);
            corrections.ForEach(bit => e.Put(bit, 1));
            corrections.Clear();
            run = 0;
        }

        if (run > 0 || corrections.Count > 0)
        {
            e.EndOfBands++;
            e.Corrections.AddRange(corrections);
            if (e.EndOfBands == 0x7FFF || e.Corrections.Count > 937)
                e.FlushEndOfBands();
        }
    }
}

using System.Buffers.Binary;

namespace RDCore.SDK.Forms;

/// <summary>
/// Gives a stream of a compound file new content ([MS-CFB]), changing nothing of the file that the new content does not
/// need changed.
/// </summary>
/// <remarks>
/// The stream keeps the sectors its new length still needs, frees the others and takes more as it must: a free sector,
/// or one appended to the file. A stream shorter than the mini stream cutoff lives in the mini stream, a longer one in
/// regular sectors ([MS-CFB] 2.6.3), so a new length across the cutoff moves the stream from one to the other. The FAT,
/// the mini FAT and the mini stream grow as their sectors must; the FAT grows within the 109 sectors the header's DIFAT
/// lists ([MS-CFB] 2.2), a file of up to 6.8 MB with 512-byte sectors, and past them the edit is refused. A freed
/// sector is zeroed, and so is what a new sector holds past the stream's end.
/// </remarks>
internal sealed class CompoundFileEditor
{
    private const uint FreeSector = 0xFFFFFFFF;
    private const uint EndOfChain = 0xFFFFFFFE;
    private const uint FatSector = 0xFFFFFFFD;
    private const int HeaderDifatEntries = 109;
    private const int DirectoryEntrySize = 128;

    private byte[] _data;
    private readonly bool _isVersion3;
    private readonly int _sectorSize;
    private readonly int _miniSectorSize;
    private readonly long _miniStreamCutoff;
    private readonly List<uint> _fat;
    private readonly List<uint> _fatSectors;
    private readonly List<uint> _miniFat;
    private readonly List<uint> _miniFatSectors;
    private readonly List<uint> _miniStreamSectors;
    private readonly List<uint> _directorySectors;
    private long _miniStreamSize;

    /// <summary>
    /// Starts editing a copy of <paramref name="file"/>.
    /// </summary>
    /// <param name="file">The compound file, opened.</param>
    /// <exception cref="MSFormsFormatException">The file's FAT does not cover its sectors, or one of its chains is damaged.</exception>
    public CompoundFileEditor(CompoundFile file)
    {
        _data = file.Data.ToArray();
        _isVersion3 = file.MajorVersion == 3;
        _sectorSize = file.SectorSize;
        _miniSectorSize = file.MiniSectorSize;
        _miniStreamCutoff = file.MiniStreamCutoff;
        _fat = [.. file.Fat];
        _fatSectors = [.. file.FatSectors];
        _miniFat = [.. file.MiniFat];
        if (SectorCount > _fat.Count)
        {
            throw new MSFormsFormatException(MSFormsFormatProblem.CorruptCompoundFile);
        }

        _directorySectors = ChainOf(_fat, BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(48)));
        _miniFatSectors = ChainOf(_fat, BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(60)));
        (var rootStart, _miniStreamSize) = EntryOf(file.Root.Id);
        _miniStreamSectors = _miniStreamSize == 0 ? [] : ChainOf(_fat, rootStart);
    }

    private int EntriesPerSector => _sectorSize / 4;

    // the sectors after the header; a last sector the file holds only part of counts
    private int SectorCount => (_data.Length + _sectorSize - 1) / _sectorSize - 1;

    /// <summary>
    /// Gives <paramref name="stream"/> the content <paramref name="content"/>.
    /// </summary>
    /// <param name="stream">The stream, as the file this editor started from lists it.</param>
    /// <param name="content">The stream's new content.</param>
    /// <exception cref="NotSupportedException">The file would need more FAT sectors than its header lists.</exception>
    public void Replace(CompoundFileEntry stream, ReadOnlySpan<byte> content)
    {
        var (start, size) = EntryOf(stream.Id);
        var wasMini = size < _miniStreamCutoff;
        var isMini = content.Length < _miniStreamCutoff;
        var oldTable = wasMini ? _miniFat : _fat;
        var oldSectors = size == 0 ? [] : ChainOf(oldTable, start);
        if ((long)oldSectors.Count * (wasMini ? _miniSectorSize : _sectorSize) < size)
        {
            throw new MSFormsFormatException(MSFormsFormatProblem.CorruptCompoundFile);
        }

        // on the same side of the cutoff, the stream keeps the sectors it still needs, in their order
        var unit = isMini ? _miniSectorSize : _sectorSize;
        var needed = (content.Length + unit - 1) / unit;
        var kept = wasMini == isMini ? Math.Min(needed, oldSectors.Count) : 0;
        foreach (var sector in oldSectors.Skip(kept))
        {
            (wasMini ? MiniSector(sector) : Sector(sector)).Clear();
            oldTable[(int)sector] = FreeSector;
        }

        var sectors = oldSectors.Take(kept).ToList();
        while (sectors.Count < needed)
        {
            sectors.Add(isMini ? AllocateMiniSector() : AllocateSector());
        }

        var table = isMini ? _miniFat : _fat;
        for (var index = 0; index < sectors.Count; index++)
        {
            table[(int)sectors[index]] = index + 1 < sectors.Count ? sectors[index + 1] : EndOfChain;
        }

        for (var index = 0; index < sectors.Count; index++)
        {
            var span = isMini ? MiniSector(sectors[index]) : Sector(sectors[index]);
            var from = index * unit;
            var count = Math.Min(unit, content.Length - from);
            content.Slice(from, count).CopyTo(span);

            // past the new end: a kept sector's bytes up to the old end are the old content's, a new sector's are whatever
            // it held; the bytes of a kept sector past the old end are not the stream's, and stay as they are
            var clearTo = index < kept ? (int)Math.Clamp(size - from, count, unit) : unit;
            span[count..clearTo].Clear();
        }

        SetEntry(stream.Id, sectors.Count > 0 ? sectors[0] : EndOfChain, content.Length);
    }

    /// <summary>
    /// The edited file's bytes.
    /// </summary>
    public byte[] ToArray()
    {
        var header = _data.AsSpan(0, 512);
        BinaryPrimitives.WriteUInt32LittleEndian(header[44..], (uint)_fatSectors.Count);
        for (var index = 0; index < Math.Min(HeaderDifatEntries, _fatSectors.Count); index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(header[(76 + index * 4)..], _fatSectors[index]);
        }

        BinaryPrimitives.WriteUInt32LittleEndian(header[60..], _miniFatSectors.Count > 0 ? _miniFatSectors[0] : EndOfChain);
        BinaryPrimitives.WriteUInt32LittleEndian(header[64..], (uint)_miniFatSectors.Count);
        WriteTable(_fat, _fatSectors);
        WriteTable(_miniFat, _miniFatSectors);
        if (_miniStreamSectors.Count > 0)
        {
            // the root's stream is the mini stream ([MS-CFB] 2.6.2); a file without one keeps its root as it is
            SetEntry(0, _miniStreamSectors[0], _miniStreamSize);
        }

        return [.. _data];
    }

    private void WriteTable(List<uint> table, List<uint> sectors)
    {
        for (var index = 0; index < sectors.Count; index++)
        {
            var sector = Sector(sectors[index]);
            for (var entry = 0; entry < EntriesPerSector; entry++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(sector[(entry * 4)..], table[index * EntriesPerSector + entry]);
            }
        }
    }

    // a free sector the file has, or one appended to it; its FAT entry ends a chain until the caller links it
    private uint AllocateSector()
    {
        for (var sector = 0; sector < SectorCount; sector++)
        {
            if (_fat[sector] == FreeSector)
            {
                _fat[sector] = EndOfChain;
                Sector((uint)sector).Clear();
                return (uint)sector;
            }
        }

        var appended = SectorCount;
        if (appended == _fat.Count)
        {
            // no FAT entry for a new sector: the new sector is a FAT sector, whose first entry maps itself
            if (_fatSectors.Count == HeaderDifatEntries)
            {
                throw new NotSupportedException(MSFormsMessages.FileTooLarge);
            }

            Grow();
            _fat.AddRange(Enumerable.Repeat(FreeSector, EntriesPerSector));
            _fat[appended] = FatSector;
            _fatSectors.Add((uint)appended);
            appended = SectorCount;
        }

        Grow();
        _fat[appended] = EndOfChain;
        return (uint)appended;
    }

    // a free mini sector of the mini stream, or one past its end; the mini FAT and the mini stream grow as they must
    private uint AllocateMiniSector()
    {
        var inStream = (int)((_miniStreamSize + _miniSectorSize - 1) / _miniSectorSize);
        for (var sector = 0; sector < Math.Min(inStream, _miniFat.Count); sector++)
        {
            if (_miniFat[sector] == FreeSector)
            {
                _miniFat[sector] = EndOfChain;
                MiniSector((uint)sector).Clear();
                return (uint)sector;
            }
        }

        if (inStream >= _miniFat.Count)
        {
            Append(_miniFatSectors, AllocateSector());
            _miniFat.AddRange(Enumerable.Repeat(FreeSector, EntriesPerSector));
        }

        _miniStreamSize = (inStream + 1L) * _miniSectorSize;
        while ((long)_miniStreamSectors.Count * _sectorSize < _miniStreamSize)
        {
            Append(_miniStreamSectors, AllocateSector());
        }

        _miniFat[inStream] = EndOfChain;
        return (uint)inStream;
    }

    // a sector at the end of a chain of the FAT
    private void Append(List<uint> chain, uint sector)
    {
        if (chain.Count > 0)
        {
            _fat[(int)chain[^1]] = sector;
        }

        _fat[(int)sector] = EndOfChain;
        chain.Add(sector);
    }

    private void Grow() => Array.Resize(ref _data, (SectorCount + 2) * _sectorSize);

    private Span<byte> Sector(uint sector)
    {
        var offset = (sector + 1L) * _sectorSize;
        if (offset + _sectorSize > _data.Length)
        {
            throw new MSFormsFormatException(MSFormsFormatProblem.CorruptCompoundFile);
        }

        return _data.AsSpan((int)offset, _sectorSize);
    }

    private Span<byte> MiniSector(uint sector)
    {
        var offset = (long)sector * _miniSectorSize;
        if (offset / _sectorSize >= _miniStreamSectors.Count)
        {
            throw new MSFormsFormatException(MSFormsFormatProblem.CorruptCompoundFile);
        }

        return Sector(_miniStreamSectors[(int)(offset / _sectorSize)]).Slice((int)(offset % _sectorSize), _miniSectorSize);
    }

    // the sectors of a chain of `table`, bounded: a chain longer than the table has entries loops
    private static List<uint> ChainOf(List<uint> table, uint start)
    {
        var result = new List<uint>();
        for (var sector = start; sector != EndOfChain; sector = table[(int)sector])
        {
            if (sector >= table.Count || result.Count >= table.Count)
            {
                throw new MSFormsFormatException(MSFormsFormatProblem.CorruptCompoundFile);
            }

            result.Add(sector);
        }

        return result;
    }

    private Span<byte> Entry(int id)
    {
        var perSector = _sectorSize / DirectoryEntrySize;
        if (id / perSector >= _directorySectors.Count)
        {
            throw new MSFormsFormatException(MSFormsFormatProblem.CorruptCompoundFile);
        }

        return Sector(_directorySectors[id / perSector]).Slice(id % perSector * DirectoryEntrySize, DirectoryEntrySize);
    }

    // a version 3 file's size has undefined high 32 bits ([MS-CFB] 2.6.3): they are neither read nor written
    private (uint Start, long Size) EntryOf(int id)
    {
        var entry = Entry(id);
        var size = (long)BinaryPrimitives.ReadUInt64LittleEndian(entry[120..]);
        return (BinaryPrimitives.ReadUInt32LittleEndian(entry[116..]), _isVersion3 ? size & 0xFFFFFFFF : size);
    }

    private void SetEntry(int id, uint start, long size)
    {
        var entry = Entry(id);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[116..], start);
        if (_isVersion3)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(entry[120..], (uint)size);
        }
        else
        {
            BinaryPrimitives.WriteUInt64LittleEndian(entry[120..], (ulong)size);
        }
    }
}

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Dignite.Abp.BlobStoring.Pipeline;

/// <summary>
/// Minimal fonts: the real headers and table directories of an sfnt (TrueType, OpenType), WOFF and WOFF2 font, each
/// followed by four bytes of data per table, built here so that no font file is committed; and variants that break
/// one thing each. The layout constants are the font formats' own (the OpenType and WOFF specifications), written
/// out here instead of shared with the detector so that a mistake in one cannot hide in the other.
/// </summary>
public static class FontSamples
{
    // sfnt: a 12-byte header (version, table count, ...), then a 16-byte record per table (tag, checksum, offset, length).
    private const int SfntTableCountOffset = 4;
    private const int SfntHeaderLength = 12;
    private const int SfntRecordLength = 16;
    private const int SfntRecordOffsetField = 8;
    private const int SfntRecordLengthField = 12;

    // WOFF (a 44-byte header) and WOFF2 (48 bytes) begin alike: signature, flavor, length, table count, reserved.
    private const int WoffFlavorOffset = 4;
    private const int WoffLengthOffset = 8;
    private const int WoffTableCountOffset = 12;
    private const int WoffReservedOffset = 14;
    private const int WoffHeaderLength = 44;
    private const int Woff2HeaderLength = 48;

    private const uint TrueTypeVersion = 0x00010000;
    private const uint AppleTrueTypeVersion = 0x74727565; // "true"
    private const uint OpenTypeCffVersion = 0x4F54544F; // "OTTO"
    private const uint TrueTypeCollectionVersion = 0x74746366; // "ttcf"

    private static readonly string[] TrueTypeTables =
        ["OS/2", "cmap", "glyf", "head", "hhea", "hmtx", "loca", "maxp", "name", "post"];

    private static readonly string[] CffTables = ["CFF ", "OS/2", "cmap", "head", "hhea", "hmtx", "maxp", "name", "post"];

    public static byte[] TrueType { get; } = CreateSfnt(TrueTypeVersion, TrueTypeTables);

    /// <summary>
    /// The sfnt version "true" of fonts made for Apple platforms: TrueType all the same.
    /// </summary>
    public static byte[] AppleTrueType { get; } = CreateSfnt(AppleTrueTypeVersion, TrueTypeTables);

    /// <summary>
    /// OpenType with CFF outlines ("OTTO").
    /// </summary>
    public static byte[] OpenType { get; } = CreateSfnt(OpenTypeCffVersion, CffTables);

    public static byte[] Woff { get; } = CreateWoff(isWoff2: false, TrueTypeVersion, TrueTypeTables);

    /// <summary>
    /// A WOFF2 header and table directory. The table data after them is not a Brotli stream; the signature probe
    /// reads the header only.
    /// </summary>
    public static byte[] Woff2 { get; } = CreateWoff(isWoff2: true, OpenTypeCffVersion, CffTables);

    /// <summary>
    /// A TrueType font with 100 tables: its table directory is longer than the part of the file the detector reads.
    /// </summary>
    public static byte[] TrueTypeWithManyTables { get; } =
        CreateSfnt(TrueTypeVersion, Enumerable.Range(0, 100).Select(i => $"T{i:D3}").ToArray());

    public static byte[] WoffWithAppleTrueTypeFlavor { get; } = WithWoffFlavor(Woff, AppleTrueTypeVersion);

    /// <summary>
    /// WOFF2 can wrap a font collection; its flavor is then "ttcf".
    /// </summary>
    public static byte[] Woff2WithCollectionFlavor { get; } = WithWoffFlavor(Woff2, TrueTypeCollectionVersion);

    // Variants that are not fonts: a signature the rest of the header does not bear out.

    public static byte[] TrueTypeWithoutTables { get; } = WithSfntTableCount(TrueType, 0);

    public static byte[] TrueTypeWithTooManyTables { get; } = WithSfntTableCount(TrueType, 4096);

    /// <summary>
    /// Cut short before the end of its first table record.
    /// </summary>
    public static byte[] TruncatedTrueType { get; } = TrueType[..(SfntHeaderLength + SfntRecordLength - 1)];

    /// <summary>
    /// The tag of the first table holds control bytes.
    /// </summary>
    public static byte[] TrueTypeWithBinaryTableTag { get; } = WithFirstTableTag(TrueType, [0x00, 0x01, 0x02, 0x03]);

    /// <summary>
    /// The first table starts at offset 0, inside the header and the table directory.
    /// </summary>
    public static byte[] TrueTypeWithTableInsideDirectory { get; } = WithFirstTableOffset(TrueType, 0);

    /// <summary>
    /// The first table ends beyond 4 GB, which an sfnt offset and length cannot address.
    /// </summary>
    public static byte[] TrueTypeWithTableBeyond32Bits { get; } =
        WithFirstTableLength(WithFirstTableOffset(TrueType, 0xFFFFFFFC), 8);

    public static byte[] WoffWithoutTables { get; } = WithWoffTableCount(Woff, 0);

    public static byte[] WoffWithTooManyTables { get; } = WithWoffTableCount(Woff, 4096);

    public static byte[] Woff2WithoutTables { get; } = WithWoffTableCount(Woff2, 0);

    public static byte[] WoffWithUnknownFlavor { get; } = WithWoffFlavor(Woff, 0x78787878); // "xxxx"

    public static byte[] WoffWithReservedFieldSet { get; } = WithWoffReserved(Woff, 1);

    /// <summary>
    /// The length of the file, as the header gives it, is less than the header itself.
    /// </summary>
    public static byte[] WoffShorterThanItsHeader { get; } = WithWoffLength(Woff, WoffHeaderLength - 1);

    public static byte[] WoffTruncated { get; } = Woff[..(WoffHeaderLength - 1)];

    public static byte[] Woff2Truncated { get; } = Woff2[..(Woff2HeaderLength - 1)];

    public static byte[] Get(string name) => name switch
    {
        nameof(TrueType) => TrueType,
        nameof(AppleTrueType) => AppleTrueType,
        nameof(OpenType) => OpenType,
        nameof(Woff) => Woff,
        nameof(Woff2) => Woff2,
        nameof(TrueTypeWithManyTables) => TrueTypeWithManyTables,
        nameof(WoffWithAppleTrueTypeFlavor) => WoffWithAppleTrueTypeFlavor,
        nameof(Woff2WithCollectionFlavor) => Woff2WithCollectionFlavor,
        nameof(TrueTypeWithoutTables) => TrueTypeWithoutTables,
        nameof(TrueTypeWithTooManyTables) => TrueTypeWithTooManyTables,
        nameof(TruncatedTrueType) => TruncatedTrueType,
        nameof(TrueTypeWithBinaryTableTag) => TrueTypeWithBinaryTableTag,
        nameof(TrueTypeWithTableInsideDirectory) => TrueTypeWithTableInsideDirectory,
        nameof(TrueTypeWithTableBeyond32Bits) => TrueTypeWithTableBeyond32Bits,
        nameof(WoffWithoutTables) => WoffWithoutTables,
        nameof(WoffWithTooManyTables) => WoffWithTooManyTables,
        nameof(Woff2WithoutTables) => Woff2WithoutTables,
        nameof(WoffWithUnknownFlavor) => WoffWithUnknownFlavor,
        nameof(WoffWithReservedFieldSet) => WoffWithReservedFieldSet,
        nameof(WoffShorterThanItsHeader) => WoffShorterThanItsHeader,
        nameof(WoffTruncated) => WoffTruncated,
        nameof(Woff2Truncated) => Woff2Truncated,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
    };

    private static byte[] CreateSfnt(uint version, IReadOnlyList<string> tags)
    {
        var tableCount = tags.Count;
        var directoryEnd = SfntHeaderLength + SfntRecordLength * tableCount;
        var bytes = new byte[directoryEnd + 4 * tableCount];

        var entrySelector = BitOperations.Log2((uint)tableCount);
        var searchRange = 16 << entrySelector;
        BinaryPrimitives.WriteUInt32BigEndian(bytes, version);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(SfntTableCountOffset), (ushort)tableCount);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(6), (ushort)searchRange);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(8), (ushort)entrySelector);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(10), (ushort)(16 * tableCount - searchRange));

        for (var i = 0; i < tableCount; i++)
        {
            // Table record: tag, checksum (left zero), offset, length. Each table holds four bytes of zeros.
            var record = bytes.AsSpan(SfntHeaderLength + SfntRecordLength * i, SfntRecordLength);
            Encoding.ASCII.GetBytes(tags[i], record);
            BinaryPrimitives.WriteUInt32BigEndian(record[SfntRecordOffsetField..], (uint)(directoryEnd + 4 * i));
            BinaryPrimitives.WriteUInt32BigEndian(record[SfntRecordLengthField..], 4);
        }

        return bytes;
    }

    private static byte[] CreateWoff(bool isWoff2, uint flavor, IReadOnlyList<string> tags)
    {
        var tableCount = tags.Count;
        var headerLength = isWoff2 ? Woff2HeaderLength : WoffHeaderLength;

        // WOFF: tag, offset, compressed length, original length and original checksum, 20 bytes. WOFF2: a flags
        // byte (0x3F, the tag follows), the tag and the original length as a one-byte UIntBase128.
        var entryLength = isWoff2 ? 6 : 20;
        var directoryEnd = headerLength + entryLength * tableCount;
        var bytes = new byte[directoryEnd + 4 * tableCount];

        Encoding.ASCII.GetBytes(isWoff2 ? "wOF2" : "wOFF", bytes.AsSpan(0, 4));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(WoffFlavorOffset), flavor);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(WoffLengthOffset), (uint)bytes.Length);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(WoffTableCountOffset), (ushort)tableCount);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16), (uint)(SfntHeaderLength + 20 * tableCount));

        // Version 1.0; WOFF2 puts the total compressed size before it. The reserved field and the metadata and
        // private data blocks stay zero.
        var version = isWoff2 ? 24 : 20;
        if (isWoff2)
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20), (uint)(4 * tableCount));
        }

        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(version), 1);

        for (var i = 0; i < tableCount; i++)
        {
            var entry = bytes.AsSpan(headerLength + entryLength * i, entryLength);
            if (isWoff2)
            {
                entry[0] = 0x3F;
                Encoding.ASCII.GetBytes(tags[i], entry.Slice(1, 4));
                entry[5] = 4;
            }
            else
            {
                Encoding.ASCII.GetBytes(tags[i], entry[..4]);
                BinaryPrimitives.WriteUInt32BigEndian(entry[4..], (uint)(directoryEnd + 4 * i));
                BinaryPrimitives.WriteUInt32BigEndian(entry[8..], 4);
                BinaryPrimitives.WriteUInt32BigEndian(entry[12..], 4);
            }
        }

        return bytes;
    }

    private static byte[] WithSfntTableCount(byte[] font, ushort count) =>
        Patch(font, bytes => BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(SfntTableCountOffset), count));

    private static byte[] WithFirstTableTag(byte[] font, byte[] tag) =>
        Patch(font, bytes => tag.CopyTo(bytes, SfntHeaderLength));

    private static byte[] WithFirstTableOffset(byte[] font, uint offset) =>
        Patch(font, bytes => BinaryPrimitives.WriteUInt32BigEndian(
            bytes.AsSpan(SfntHeaderLength + SfntRecordOffsetField), offset));

    private static byte[] WithFirstTableLength(byte[] font, uint length) =>
        Patch(font, bytes => BinaryPrimitives.WriteUInt32BigEndian(
            bytes.AsSpan(SfntHeaderLength + SfntRecordLengthField), length));

    private static byte[] WithWoffFlavor(byte[] font, uint flavor) =>
        Patch(font, bytes => BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(WoffFlavorOffset), flavor));

    private static byte[] WithWoffLength(byte[] font, uint length) =>
        Patch(font, bytes => BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(WoffLengthOffset), length));

    private static byte[] WithWoffTableCount(byte[] font, ushort count) =>
        Patch(font, bytes => BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(WoffTableCountOffset), count));

    private static byte[] WithWoffReserved(byte[] font, ushort value) =>
        Patch(font, bytes => BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(WoffReservedOffset), value));

    private static byte[] Patch(byte[] source, Action<byte[]> change)
    {
        var copy = (byte[])source.Clone();
        change(copy);
        return copy;
    }
}

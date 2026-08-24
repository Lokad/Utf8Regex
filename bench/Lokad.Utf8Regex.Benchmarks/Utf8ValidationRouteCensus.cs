using System.Buffers;
using System.Text;

namespace Lokad.Utf8Regex.Benchmarks;

internal enum Utf8ValidationScalarShape : byte
{
    Ascii = 0,
    TwoByte = 1,
    SafeThreeByte = 2,
    ConstrainedThreeByte = 3,
    FourByte = 4,
}

internal enum Utf8ValidationBlockShape : byte
{
    AsciiOnly = 0,
    TwoByteCommon = 1,
    SafeThreeByteCommon = 2,
    ConstrainedThreeByte = 3,
    FourByte = 4,
}

internal sealed class Utf8ValidationRouteCensus
{
    private const int VectorBlockSize = 32;
    private const int DenseSafeThreeByteLeadThreshold = 2;
    private const int SafeThreeByteIdleBlockLimit = 4;

    private Utf8ValidationRouteCensus()
    {
    }

    public int InputBytes { get; private set; }

    public int ScalarCount { get; private set; }

    public int FirstNonAsciiOffset { get; private set; } = -1;

    public int FirstTwoByteOffset { get; private set; } = -1;

    public int FirstSafeThreeByteOffset { get; private set; } = -1;

    public int FirstConstrainedThreeByteOffset { get; private set; } = -1;

    public int FirstFourByteOffset { get; private set; } = -1;

    public int CrossBlockScalars { get; private set; }

    public int BlockCount { get; private set; }

    public int DenseSafeThreeByteBlocks { get; private set; }

    public int ScalarEscapeBlocks { get; private set; }

    public int BlockShapeTransitions { get; private set; }

    public int EstimatedLeanBlocks { get; private set; }

    public int EstimatedWideBlocks { get; private set; }

    public int EstimatedWideEntries { get; private set; }

    public int EstimatedWideExits { get; private set; }

    public bool InitialWideMode { get; private set; }

    public long[] ScalarCounts { get; } = new long[5];

    public long[] ScalarBytes { get; } = new long[5];

    public int[] ScalarRuns { get; } = new int[5];

    public int[] LongestScalarRunScalars { get; } = new int[5];

    public int[] LongestScalarRunBytes { get; } = new int[5];

    public int[] BlockShapeCounts { get; } = new int[5];

    public int[] LongestBlockShapeRuns { get; } = new int[5];

    public static Utf8ValidationRouteCensus Analyze(ReadOnlySpan<byte> input)
    {
        var census = new Utf8ValidationRouteCensus
        {
            InputBytes = input.Length,
        };
        census.AnalyzeScalars(input);
        census.AnalyzeBlocks(input);
        census.VerifyTotals();
        return census;
    }

    private void AnalyzeScalars(ReadOnlySpan<byte> input)
    {
        var offset = 0;
        var previousShape = (Utf8ValidationScalarShape?)null;
        var currentRunScalars = 0;
        var currentRunBytes = 0;
        while (offset < input.Length)
        {
            var status = Rune.DecodeFromUtf8(input[offset..], out _, out var consumed);
            if (status != OperationStatus.Done)
            {
                throw new InvalidOperationException($"Validation census received malformed UTF-8 at byte offset {offset}.");
            }

            var shape = ClassifyScalar(input[offset]);
            var shapeIndex = (int)shape;
            ScalarCount++;
            ScalarCounts[shapeIndex]++;
            ScalarBytes[shapeIndex] += consumed;
            RecordFirstOffset(shape, offset);
            if (offset / VectorBlockSize != (offset + consumed - 1) / VectorBlockSize)
            {
                CrossBlockScalars++;
            }

            if (shape == previousShape)
            {
                currentRunScalars++;
                currentRunBytes += consumed;
            }
            else
            {
                CompleteScalarRun(previousShape, currentRunScalars, currentRunBytes);
                previousShape = shape;
                currentRunScalars = 1;
                currentRunBytes = consumed;
            }

            offset += consumed;
        }

        CompleteScalarRun(previousShape, currentRunScalars, currentRunBytes);
    }

    private void AnalyzeBlocks(ReadOnlySpan<byte> input)
    {
        BlockCount = (input.Length + VectorBlockSize - 1) / VectorBlockSize;
        var initialSample = input[..Math.Min(input.Length, 256)];
        var initialTwoByteLeads = 0;
        var initialSafeThreeByteLeads = 0;
        foreach (var value in initialSample)
        {
            if (value is >= 0xC2 and < 0xE0)
            {
                initialTwoByteLeads++;
            }
            else if (IsSafeThreeByteLead(value))
            {
                initialSafeThreeByteLeads++;
            }
        }

        InitialWideMode = initialSafeThreeByteLeads > initialTwoByteLeads;
        var estimatedWideMode = InitialWideMode;
        var safeThreeByteIdleBlocks = 0;
        var previousShape = (Utf8ValidationBlockShape?)null;
        var currentShapeRun = 0;
        for (var blockOffset = 0; blockOffset < input.Length; blockOffset += VectorBlockSize)
        {
            var block = input.Slice(blockOffset, Math.Min(VectorBlockSize, input.Length - blockOffset));
            var shape = ClassifyBlock(block, out var safeThreeByteLeads);
            var shapeIndex = (int)shape;
            BlockShapeCounts[shapeIndex]++;
            if (shape == previousShape)
            {
                currentShapeRun++;
            }
            else
            {
                CompleteBlockRun(previousShape, currentShapeRun);
                if (previousShape.HasValue)
                {
                    BlockShapeTransitions++;
                }

                previousShape = shape;
                currentShapeRun = 1;
            }

            if (safeThreeByteLeads >= DenseSafeThreeByteLeadThreshold)
            {
                DenseSafeThreeByteBlocks++;
            }

            if (shape is Utf8ValidationBlockShape.ConstrainedThreeByte or Utf8ValidationBlockShape.FourByte)
            {
                ScalarEscapeBlocks++;
            }

            if (!estimatedWideMode && safeThreeByteLeads >= DenseSafeThreeByteLeadThreshold)
            {
                estimatedWideMode = true;
                safeThreeByteIdleBlocks = 0;
                EstimatedWideEntries++;
            }

            if (estimatedWideMode)
            {
                EstimatedWideBlocks++;
                safeThreeByteIdleBlocks = safeThreeByteLeads == 0
                    ? safeThreeByteIdleBlocks + 1
                    : 0;
                if (safeThreeByteIdleBlocks >= SafeThreeByteIdleBlockLimit)
                {
                    estimatedWideMode = false;
                    safeThreeByteIdleBlocks = 0;
                    EstimatedWideExits++;
                }
            }
            else
            {
                EstimatedLeanBlocks++;
            }
        }

        CompleteBlockRun(previousShape, currentShapeRun);
    }

    private static Utf8ValidationScalarShape ClassifyScalar(byte lead)
    {
        if (lead < 0x80)
        {
            return Utf8ValidationScalarShape.Ascii;
        }

        if (lead < 0xE0)
        {
            return Utf8ValidationScalarShape.TwoByte;
        }

        if (lead is 0xE0 or 0xED)
        {
            return Utf8ValidationScalarShape.ConstrainedThreeByte;
        }

        return lead < 0xF0
            ? Utf8ValidationScalarShape.SafeThreeByte
            : Utf8ValidationScalarShape.FourByte;
    }

    private static Utf8ValidationBlockShape ClassifyBlock(ReadOnlySpan<byte> block, out int safeThreeByteLeads)
    {
        var hasTwoByteLead = false;
        var hasConstrainedThreeByteLead = false;
        var hasFourByteLead = false;
        safeThreeByteLeads = 0;
        foreach (var value in block)
        {
            if (value is >= 0xC2 and < 0xE0)
            {
                hasTwoByteLead = true;
            }
            else if (IsSafeThreeByteLead(value))
            {
                safeThreeByteLeads++;
            }
            else if (value is 0xE0 or 0xED)
            {
                hasConstrainedThreeByteLead = true;
            }
            else if (value is >= 0xF0 and < 0xF5)
            {
                hasFourByteLead = true;
            }
        }

        if (hasFourByteLead)
        {
            return Utf8ValidationBlockShape.FourByte;
        }

        if (hasConstrainedThreeByteLead)
        {
            return Utf8ValidationBlockShape.ConstrainedThreeByte;
        }

        if (safeThreeByteLeads > 0)
        {
            return Utf8ValidationBlockShape.SafeThreeByteCommon;
        }

        return hasTwoByteLead
            ? Utf8ValidationBlockShape.TwoByteCommon
            : Utf8ValidationBlockShape.AsciiOnly;
    }

    private static bool IsSafeThreeByteLead(byte value)
        => value is >= 0xE1 and <= 0xEC or >= 0xEE and < 0xF0;

    private void RecordFirstOffset(Utf8ValidationScalarShape shape, int offset)
    {
        if (shape != Utf8ValidationScalarShape.Ascii && FirstNonAsciiOffset < 0)
        {
            FirstNonAsciiOffset = offset;
        }

        switch (shape)
        {
            case Utf8ValidationScalarShape.TwoByte when FirstTwoByteOffset < 0:
                FirstTwoByteOffset = offset;
                break;
            case Utf8ValidationScalarShape.SafeThreeByte when FirstSafeThreeByteOffset < 0:
                FirstSafeThreeByteOffset = offset;
                break;
            case Utf8ValidationScalarShape.ConstrainedThreeByte when FirstConstrainedThreeByteOffset < 0:
                FirstConstrainedThreeByteOffset = offset;
                break;
            case Utf8ValidationScalarShape.FourByte when FirstFourByteOffset < 0:
                FirstFourByteOffset = offset;
                break;
        }
    }

    private void CompleteScalarRun(Utf8ValidationScalarShape? shape, int scalars, int bytes)
    {
        if (!shape.HasValue)
        {
            return;
        }

        var index = (int)shape.Value;
        ScalarRuns[index]++;
        LongestScalarRunScalars[index] = Math.Max(LongestScalarRunScalars[index], scalars);
        LongestScalarRunBytes[index] = Math.Max(LongestScalarRunBytes[index], bytes);
    }

    private void CompleteBlockRun(Utf8ValidationBlockShape? shape, int blocks)
    {
        if (shape.HasValue)
        {
            var index = (int)shape.Value;
            LongestBlockShapeRuns[index] = Math.Max(LongestBlockShapeRuns[index], blocks);
        }
    }

    private void VerifyTotals()
    {
        if (ScalarCounts.Sum() != ScalarCount || ScalarBytes.Sum() != InputBytes)
        {
            throw new InvalidOperationException("The scalar census does not cover the complete input.");
        }

        if (BlockShapeCounts.Sum() != BlockCount || EstimatedLeanBlocks + EstimatedWideBlocks != BlockCount)
        {
            throw new InvalidOperationException("The block census does not cover the complete input.");
        }
    }
}

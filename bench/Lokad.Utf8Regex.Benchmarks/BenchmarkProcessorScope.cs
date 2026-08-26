using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Lokad.Utf8Regex.Benchmarks;

internal sealed class BenchmarkProcessorScope : IDisposable
{
    private const int CpuSetInformationType = 0;
    private const int MinimumCpuSetInformationSize = 20;
    private readonly Process? _process;
    private readonly nint _originalAffinity;

    private BenchmarkProcessorScope(
        Process? process,
        nint originalAffinity,
        string policy,
        string affinityMask,
        int? efficiencyClass,
        string description)
    {
        _process = process;
        _originalAffinity = originalAffinity;
        Policy = policy;
        AffinityMask = affinityMask;
        EfficiencyClass = efficiencyClass;
        Description = description;
    }

    internal string Policy { get; }

    internal string AffinityMask { get; }

    internal int? EfficiencyClass { get; }

    internal string Description { get; }

    internal static BenchmarkProcessorScope EnterHighestEfficiencyClass() => Enter(singleProcessor: false);

    internal static BenchmarkProcessorScope EnterSingleHighestEfficiencyProcessor() => Enter(singleProcessor: true);

    public void Dispose()
    {
        if (_process is null)
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            _process.ProcessorAffinity = _originalAffinity;
        }

        _process.Dispose();
    }

    private static BenchmarkProcessorScope Enter(bool singleProcessor)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new(
                null,
                0,
                "scheduler-default",
                "unavailable",
                null,
                "scheduler default (processor efficiency classes unavailable)");
        }

        var process = Process.GetCurrentProcess();
        var originalAffinity = process.ProcessorAffinity;
        var originalMask = unchecked((ulong)originalAffinity.ToInt64());
        var selected = ReadHighestEfficiencyProcessorMask(originalMask);
        if (selected.Mask == 0 || (!singleProcessor && BitOperations.PopCount(selected.Mask) < 2))
        {
            process.Dispose();
            return new(
                null,
                0,
                "scheduler-default",
                FormatMask(originalMask),
                selected.EfficiencyClass,
                $"scheduler default ({FormatMask(originalMask)})");
        }

        var appliedMask = singleProcessor
            ? SelectLeastContendedProcessorMask(process, selected.Mask)
            : selected.Mask;
        process.ProcessorAffinity = new nint(unchecked((long)appliedMask));
        var policy = singleProcessor
            ? "single-least-contended-highest-efficiency-processor"
            : "highest-efficiency-class";
        var description = singleProcessor
            ? $"least-contended processor from efficiency class {selected.EfficiencyClass} " +
              $"({FormatMask(appliedMask)})"
            : $"highest efficiency class {selected.EfficiencyClass} ({FormatMask(appliedMask)})";
        return new(
            process,
            originalAffinity,
            policy,
            FormatMask(appliedMask),
            selected.EfficiencyClass,
            description);

        static (ulong Mask, int? EfficiencyClass) ReadHighestEfficiencyProcessorMask(ulong allowedMask)
        {
            _ = GetSystemCpuSetInformation(nint.Zero, 0, out var requiredLength, nint.Zero, 0);
            if (requiredLength == 0 || requiredLength > int.MaxValue)
            {
                return (0, null);
            }

            var buffer = Marshal.AllocHGlobal((int)requiredLength);
            try
            {
                if (!GetSystemCpuSetInformation(buffer, requiredLength, out var returnedLength, nint.Zero, 0))
                {
                    return (0, null);
                }

                int? highestEfficiencyClass = null;
                var selectedMask = 0UL;
                var offset = 0;
                while (offset < returnedLength)
                {
                    var entry = nint.Add(buffer, offset);
                    var entrySize = Marshal.ReadInt32(entry, 0);
                    if (entrySize < MinimumCpuSetInformationSize || entrySize > returnedLength - offset)
                    {
                        return (0, null);
                    }

                    var type = Marshal.ReadInt32(entry, 4);
                    if (type == CpuSetInformationType && Marshal.ReadInt16(entry, 12) == 0)
                    {
                        var logicalProcessor = Marshal.ReadByte(entry, 14);
                        var efficiencyClass = Marshal.ReadByte(entry, 18);
                        if (logicalProcessor < 64)
                        {
                            var processorMask = 1UL << logicalProcessor;
                            if ((processorMask & allowedMask) != 0 &&
                                (highestEfficiencyClass is null || efficiencyClass >= highestEfficiencyClass))
                            {
                                if (efficiencyClass > highestEfficiencyClass)
                                {
                                    selectedMask = 0;
                                }

                                highestEfficiencyClass = efficiencyClass;
                                selectedMask |= processorMask;
                            }
                        }
                    }

                    offset += entrySize;
                }

                return (selectedMask, highestEfficiencyClass);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        static ulong SelectLeastContendedProcessorMask(Process process, ulong candidateMask)
        {
            // The first logical processor is commonly burdened by system work. Probe only
            // equal-efficiency candidates; the child comparator inherits the selected mask.
            const int probeSamples = 3;
            const int probeSpinIterations = 250_000;
            var bestMask = 0UL;
            var bestMedianTicks = long.MaxValue;
            Span<long> elapsedTicks = stackalloc long[probeSamples];
            while (candidateMask != 0)
            {
                var logicalProcessor = BitOperations.TrailingZeroCount(candidateMask);
                var processorMask = 1UL << logicalProcessor;
                candidateMask &= ~processorMask;
                process.ProcessorAffinity = new nint(unchecked((long)processorMask));
                Thread.SpinWait(10_000);

                for (var sample = 0; sample < probeSamples; sample++)
                {
                    var started = Stopwatch.GetTimestamp();
                    Thread.SpinWait(probeSpinIterations);
                    elapsedTicks[sample] = Stopwatch.GetTimestamp() - started;
                }

                elapsedTicks.Sort();
                if (elapsedTicks[probeSamples / 2] < bestMedianTicks)
                {
                    bestMedianTicks = elapsedTicks[probeSamples / 2];
                    bestMask = processorMask;
                }
            }

            return bestMask;
        }
    }

    private static string FormatMask(ulong mask) => $"0x{mask:X}";

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemCpuSetInformation(
        nint information,
        uint bufferLength,
        out uint returnedLength,
        nint process,
        uint flags);
}

using System.Diagnostics;
using System.Numerics;
using Lokad.Utf8Regex.Benchmarks;
using Xunit;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class BenchmarkProcessorScopeTests
{
    [Fact]
    public void SingleProcessorScopePinsAnAllowedProcessorAndRestoresAffinity()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            return;
        }

        using var process = Process.GetCurrentProcess();
        var original = process.ProcessorAffinity;
        using (var scope = BenchmarkProcessorScope.EnterSingleHighestEfficiencyProcessor())
        {
            process.Refresh();
            var selected = unchecked((ulong)process.ProcessorAffinity.ToInt64());
            Assert.Equal(1, BitOperations.PopCount(selected));
            Assert.Equal(selected, selected & unchecked((ulong)original.ToInt64()));
            Assert.Equal($"0x{selected:X}", scope.AffinityMask);
            if (OperatingSystem.IsLinux())
            {
                Assert.Equal(BenchmarkProcessorScope.LinuxSingleProcessorPolicy, scope.Policy);
                Assert.Null(scope.EfficiencyClass);
            }
        }

        process.Refresh();
        Assert.Equal(original, process.ProcessorAffinity);
    }
}

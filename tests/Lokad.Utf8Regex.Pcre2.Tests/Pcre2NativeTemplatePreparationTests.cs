using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2NativeTemplatePreparationTests
{
    [Theory]
    [InlineData("a", "aa", "é$0😀", Pcre2SubstitutionOptions.None)]
    [InlineData("(?<x>a+)(😀)", "aa😀/a😀", "é${x}😀", Pcre2SubstitutionOptions.None)]
    [InlineData("(?<x>a)?b", "ab/b", @"${x:+\Ué😀:\x{10428}}", Pcre2SubstitutionOptions.Extended)]
    [InlineData("(?<x>a+)(😀)", "aa😀/a😀", @"\Q$1é😀\E\U$<x>", Pcre2SubstitutionOptions.Extended)]
    public void WarmNativeTemplatesNeedNoDecodedKeyOrConstruction(string pattern, string input, string template, Pcre2SubstitutionOptions options)
    {
        var regex = new Utf8Pcre2Regex(Encoding.UTF8.GetBytes(pattern));
        var subject = Encoding.UTF8.GetBytes(input); var source = Encoding.UTF8.GetBytes(template);
        var expected = regex.Replace(subject, template, 0, options);
        Assert.Equal(expected, regex.Replace(subject, source, 0, options));
        var entries = regex.DebugReplacementCacheEntryCount;
        Assert.Equal(2, entries); // Distinct text/native keys in one bounded cache.
        using (var owner = new Utf8ConstructionScope(0, 0, 0, 0))
        {
            Assert.Equal(expected, regex.Replace(subject, source, 0, options));
            Assert.Equal(0UL, owner.Budget.ReservedBytes); Assert.Equal(0UL, owner.Budget.WorkSteps);
        }
        long Allocated(bool native)
        {
            byte[] output = [];
            for (var index = 0; index < 10; index++)
                output = native ? regex.Replace(subject, source, 0, options) : regex.Replace(subject, template, 0, options);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 20; index++)
                output = native ? regex.Replace(subject, source, 0, options) : regex.Replace(subject, template, 0, options);
            var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            GC.KeepAlive(output); return bytes;
        }
        Assert.Equal(Allocated(false), Allocated(true));
        Assert.Equal(entries, regex.DebugReplacementCacheEntryCount);
    }

    [Theory]
    [InlineData(Pcre2SubstitutionOptions.None)]
    [InlineData(Pcre2SubstitutionOptions.Extended)]
    [InlineData(Pcre2SubstitutionOptions.SubstituteLiteral)]
    public void DestinationAliasingDoesNotChangeAnUnreadNativeTemplate(Pcre2SubstitutionOptions options)
    {
        var template = "é$<x>😀"u8.ToArray();
        var expected = (options == Pcre2SubstitutionOptions.SubstituteLiteral
            ? "é$<x>😀é$<x>😀"u8 : "éa😀éa😀"u8).ToArray();
        foreach (var warm in new[] { false, true })
        {
            var regex = new Utf8Pcre2Regex("(?<x>a)");
            if (warm) Assert.Equal(expected, regex.Replace("aa"u8, template, 0, options));
            var storage = new byte[64]; storage.AsSpan().Fill(0xcc);
            "aa"u8.CopyTo(storage); template.CopyTo(storage, 2);
            var status = regex.TryReplace(storage.AsSpan(0, 2), storage.AsSpan(2, template.Length), storage,
                out var written, 0, options, Pcre2MatchOptions.None);
            Assert.Equal(OperationStatus.Done, status); Assert.Equal(expected.Length, written);
            Assert.Equal(expected, storage.AsSpan(0, written).ToArray());
            Assert.All(storage.AsSpan(Math.Max(expected.Length, 2 + template.Length)).ToArray(), value => Assert.Equal(0xcc, value));
        }
    }

    [Theory]
    [InlineData("a", "a")]
    [InlineData("(?<x>a)b", "ab")]
    [InlineData(@"(?<x>a)\k<x>", "aa")]
    [InlineData(@"(?<x>\x41+)", "AAA")]
    public void NativeErrorsKeepExistingDeferredAndStrictValidationRules(string pattern, string hit)
    {
        var regex = new Utf8Pcre2Regex(pattern); var subject = Encoding.UTF8.GetBytes(hit);
        const string fallback = @"${0:-\x{z}}";
        Assert.Equal(subject, regex.Replace(subject, Encoding.UTF8.GetBytes(fallback), 0, Pcre2SubstitutionOptions.Extended));
        const string conditional = @"${0:+ok:\x{z}}";
        Assert.Equal("zzz"u8.ToArray(), regex.Replace("zzz"u8, Encoding.UTF8.GetBytes(conditional), 0, Pcre2SubstitutionOptions.Extended));
        Assert.Equal(Pcre2ErrorKind.BadReplacementEscape, Assert.Throws<Pcre2SubstitutionException>(() =>
            regex.Replace(subject, Encoding.UTF8.GetBytes(conditional), 0, Pcre2SubstitutionOptions.Extended)).ErrorKind);
        byte[] malformed = [.. "é😀${0:+ok:bad}"u8, 0x80];
        foreach (var options in new[] { Pcre2SubstitutionOptions.Extended, Pcre2SubstitutionOptions.SubstituteLiteral })
        {
            Assert.Throws<ArgumentException>(() => regex.Replace("zzz"u8, malformed, 0, options));
            var destination = new byte[] { 0xcc };
            Assert.Throws<ArgumentException>(() => regex.TryReplace(subject, malformed, destination, out _, options));
            Assert.Equal(0xcc, destination[0]);
        }
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Replace([0x80], malformed,
            -1, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None, token.Token)).CancellationToken);
    }

    [Fact]
    public void NativeKeysOwnTheirBytesAndOnlyGrammarOptionsSeparateEntries()
    {
        var regex = new Utf8Pcre2Regex("(?<x>a)"); var template = "é${x}😀"u8.ToArray();
        Assert.Equal("éa😀"u8.ToArray(), regex.Replace("a"u8, template));
        template[0] = 0xff;
        Assert.Throws<ArgumentException>(() => regex.Replace("a"u8, template));
        foreach (var options in new[] { Pcre2SubstitutionOptions.None, Pcre2SubstitutionOptions.UnsetEmpty,
            Pcre2SubstitutionOptions.UnknownUnset, Pcre2SubstitutionOptions.SubstituteMatched,
            Pcre2SubstitutionOptions.SubstituteOverflowLength })
            Assert.Equal("éa😀"u8.ToArray(), regex.Replace("a"u8, "é${x}😀"u8, 0, options));
        Assert.Equal(1, regex.DebugReplacementCacheEntryCount);
        Assert.Equal("éa😀"u8.ToArray(), regex.Replace("a"u8, "é${x}😀"u8, 0, Pcre2SubstitutionOptions.Extended));
        Assert.Equal(2, regex.DebugReplacementCacheEntryCount);
    }

    [Fact]
    public void NativeKeyAndParserLoansShareTheOriginalInvocationAndRefundOnFailure()
    {
        var regex = new Utf8Pcre2Regex("(?<x>a)");
        var invocation = new Utf8SliceInvocation(new(1_000_000, 1024, 1 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryReserveWorking(13));
        var source = "é${x}😀"u8.ToArray();
        // Public operations establish their own invocation. Borrow the production
        // preparation method directly to verify the existing ledger's overlap.
        var method = typeof(Utf8Pcre2Regex).GetMethod("GetTemplatePreparation",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var prepare = method.CreateDelegate<Func<Lokad.Utf8Regex.Internal.FrontEnd.Utf8ConstructionPattern, Pcre2SubstitutionOptions, Pcre2ReplacementPreparation>>(regex);
        using (var control = Pcre2ReplacementPreparationControl.Borrow(invocation, default))
        {
            var plan = prepare(new(source), default); var held = invocation.Ledger.ReservedBytes;
            Assert.True(held > 13);
            Assert.Equal(plan, prepare(new(source), default)); Assert.Equal(held, invocation.Ledger.ReservedBytes);
        }
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes); invocation.Ledger.Release(13);
        var denied = new Utf8SliceInvocation(new(1_000_000, 1024, 32), Timeout.InfiniteTimeSpan, default);
        using (var control = Pcre2ReplacementPreparationControl.Borrow(denied, default))
            Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(() =>
                prepare(new("another${x}"u8), default)).ErrorKind);
        Assert.Equal(0UL, denied.Ledger.ReservedBytes);
        Assert.Equal(1, regex.DebugReplacementCacheEntryCount);
    }
}

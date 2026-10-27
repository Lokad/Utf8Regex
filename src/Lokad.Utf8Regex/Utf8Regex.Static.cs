using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Replacement;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Unicode;

namespace Lokad.Utf8Regex;

// static entry points, split from the instance facade along the stateless-entry ownership boundary.
/// <summary>Static entry points for <see cref="Utf8Regex"/>.</summary>
public sealed partial class Utf8Regex
{
    /// <summary>Tests UTF-8 input with a cached culture-invariant regular expression.</summary>
    public static bool IsMatch(
        ReadOnlySpan<byte> input,
        string pattern) => IsMatch(input, pattern, RegexOptions.CultureInvariant);

    /// <summary>Tests UTF-8 input with a cached expression using the specified options.</summary>
    public static bool IsMatch(
        ReadOnlySpan<byte> input,
        string pattern,
        RegexOptions options)
    {
        return Utf8RegexCache.GetOrAdd(pattern, options).IsMatch(input);
    }

    /// <summary>Tests UTF-8 input with a cached expression using the specified options and timeout.</summary>
    public static bool IsMatch(
        ReadOnlySpan<byte> input,
        string pattern,
        RegexOptions options,
        TimeSpan matchTimeout)
    {
        return Utf8RegexCache.GetOrAdd(pattern, options, matchTimeout).IsMatch(input);
    }

    /// <summary>Counts matches in UTF-8 input with a cached culture-invariant expression.</summary>
    public static int Count(
        ReadOnlySpan<byte> input,
        string pattern) => Count(input, pattern, RegexOptions.CultureInvariant);

    /// <summary>Counts matches with a cached expression using the specified options.</summary>
    public static int Count(
        ReadOnlySpan<byte> input,
        string pattern,
        RegexOptions options)
    {
        return Utf8RegexCache.GetOrAdd(pattern, options).Count(input);
    }

    /// <summary>Counts matches with a cached expression using the specified options and timeout.</summary>
    public static int Count(
        ReadOnlySpan<byte> input,
        string pattern,
        RegexOptions options,
        TimeSpan matchTimeout)
    {
        return Utf8RegexCache.GetOrAdd(pattern, options, matchTimeout).Count(input);
    }

    /// <summary>Finds the first match with a cached culture-invariant expression.</summary>
    public static Utf8ValueMatch Match(
        ReadOnlySpan<byte> input,
        string pattern) => Match(input, pattern, RegexOptions.CultureInvariant);

    /// <summary>Finds the first match with a cached expression using the specified options.</summary>
    public static Utf8ValueMatch Match(
        ReadOnlySpan<byte> input,
        string pattern,
        RegexOptions options)
    {
        return Utf8RegexCache.GetOrAdd(pattern, options).Match(input);
    }

    /// <summary>Finds the first match with a cached expression using the specified options and timeout.</summary>
    public static Utf8ValueMatch Match(
        ReadOnlySpan<byte> input,
        string pattern,
        RegexOptions options,
        TimeSpan matchTimeout)
    {
        return Utf8RegexCache.GetOrAdd(pattern, options, matchTimeout).Match(input);
    }

    /// <summary>Finds the first match and capture context with a cached expression.</summary>
    public static Utf8MatchContext MatchDetailed(
        ReadOnlySpan<byte> input,
        string pattern) => MatchDetailed(input, pattern, RegexOptions.CultureInvariant);

    /// <summary>Finds the first detailed match with a cached expression using the specified options.</summary>
    public static Utf8MatchContext MatchDetailed(
        ReadOnlySpan<byte> input,
        string pattern,
        RegexOptions options)
    {
        return Utf8RegexCache.GetOrAdd(pattern, options).MatchDetailed(input);
    }

    /// <summary>Finds the first detailed match with a cached expression using the specified options and timeout.</summary>
    public static Utf8MatchContext MatchDetailed(
        ReadOnlySpan<byte> input,
        string pattern,
        RegexOptions options,
        TimeSpan matchTimeout)
    {
        return Utf8RegexCache.GetOrAdd(pattern, options, matchTimeout).MatchDetailed(input);
    }

    /// <summary>Enumerates matches with a cached culture-invariant expression.</summary>
    public static Utf8ValueMatchEnumerator EnumerateMatches(
        ReadOnlySpan<byte> input,
        string pattern) => EnumerateMatches(input, pattern, RegexOptions.CultureInvariant);

    /// <summary>Enumerates matches with a cached expression using the specified options.</summary>
    public static Utf8ValueMatchEnumerator EnumerateMatches(
        ReadOnlySpan<byte> input,
        string pattern,
        RegexOptions options)
    {
        return Utf8RegexCache.GetOrAdd(pattern, options).EnumerateMatches(input);
    }

    /// <summary>Enumerates matches with a cached expression using the specified options and timeout.</summary>
    public static Utf8ValueMatchEnumerator EnumerateMatches(
        ReadOnlySpan<byte> input,
        string pattern,
        RegexOptions options,
        TimeSpan matchTimeout)
    {
        return Utf8RegexCache.GetOrAdd(pattern, options, matchTimeout).EnumerateMatches(input);
    }

    /// <summary>Enumerates all split segments with a cached culture-invariant expression.</summary>
    public static Utf8ValueSplitEnumerator EnumerateSplits(
        ReadOnlySpan<byte> input,
        string pattern) => EnumerateSplits(input, pattern, int.MaxValue, RegexOptions.CultureInvariant);

    /// <summary>Enumerates a bounded number of split segments with a cached expression.</summary>
    public static Utf8ValueSplitEnumerator EnumerateSplits(
        ReadOnlySpan<byte> input,
        string pattern,
        int count) => EnumerateSplits(input, pattern, count, RegexOptions.CultureInvariant);

    /// <summary>Enumerates a bounded number of split segments with a cached expression using the specified options.</summary>
    public static Utf8ValueSplitEnumerator EnumerateSplits(
        ReadOnlySpan<byte> input,
        string pattern,
        int count,
        RegexOptions options)
    {
        return Utf8RegexCache.GetOrAdd(pattern, options).EnumerateSplits(input, count);
    }

    /// <summary>Enumerates a bounded number of split segments with a cached expression using the specified options and timeout.</summary>
    public static Utf8ValueSplitEnumerator EnumerateSplits(
        ReadOnlySpan<byte> input,
        string pattern,
        int count,
        RegexOptions options,
        TimeSpan matchTimeout)
    {
        return Utf8RegexCache.GetOrAdd(pattern, options, matchTimeout).EnumerateSplits(input, count);
    }

    /// <summary>Replaces matches with a cached culture-invariant expression.</summary>
    public static byte[] Replace(
        ReadOnlySpan<byte> input,
        string pattern,
        string replacement) => Replace(input, pattern, replacement, RegexOptions.CultureInvariant);

    /// <summary>Replaces matches with a cached expression using the specified options.</summary>
    public static byte[] Replace(
        ReadOnlySpan<byte> input,
        string pattern,
        string replacement,
        RegexOptions options)
    {
        return Utf8RegexCache.GetOrAdd(pattern, options).Replace(input, replacement);
    }

    /// <summary>Replaces matches with a cached expression using the specified options and timeout.</summary>
    public static byte[] Replace(
        ReadOnlySpan<byte> input,
        string pattern,
        string replacement,
        RegexOptions options,
        TimeSpan matchTimeout)
    {
        return Utf8RegexCache.GetOrAdd(pattern, options, matchTimeout).Replace(input, replacement);
    }
}

using System.Reflection;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;
using System.Globalization;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ParseTreeRetentionTests
{
    [Theory]
    [InlineData("abc", false)] [InlineData("abc", true)]
    [InlineData("(?<n>.)", false)] [InlineData("(?<n>.)", true)]
    [InlineData("(?<900>😀)\\k<900>", false)] [InlineData("(?<900>😀)\\k<900>", true)]
    [InlineData("[\\p{L}-[AE]]+", false)] [InlineData("[\\p{L}-[AE]]+", true)]
    [InlineData("(?=(?<n>.))\\k<n>", false)] [InlineData("(?=(?<n>.))\\k<n>", true)]
    [InlineData("(?i:ab[0-9]{2})", false)] [InlineData("(?i:ab[0-9]{2})", true)]
    public void PublishedRegexDoesNotRetainConstructionTreeObjects(string pattern, bool compiled)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None));
        var objects = InspectLibraryGraph(regex);
        Assert.DoesNotContain(objects, static value => value is Utf8OwnedRegexTree or Utf8TreeNode);
        Assert.NotNull(regex.Inspection.PreparedRegex.ScalarProgram);
    }

    [Theory]
    [InlineData("(?im)^(?<n>.)$", RegexOptions.CultureInvariant)]
    [InlineData("(?<900>😀)\\k<900>", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    [InlineData("(?<n>.)+", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)]
    [InlineData("(?<n>.)", RegexOptions.RightToLeft | RegexOptions.CultureInvariant)]
    public void PreparationCopiesSemanticIdentityAndBorrowsTheOriginalCatalog(string pattern, RegexOptions options)
    {
        var analysis = Utf8FrontEnd.Analyze(pattern, options);
        var original = analysis.SemanticRegex;
        Assert.NotNull(original.RuntimeTree);
        var prepared = Utf8RegexPreparer.Prepare(analysis);
        var metadata = prepared.SemanticRegex;
        Assert.Same(original.Pattern, metadata.Pattern);
        Assert.Equal(original.Options, metadata.Options);
        Assert.Same(original.ExecutionPattern, metadata.ExecutionPattern);
        Assert.Equal(original.ExecutionOptions, metadata.ExecutionOptions);
        Assert.Same(original.RuntimeTree.CaptureCatalog, metadata.CaptureCatalog);
        Assert.NotNull(prepared.ScalarProgram);
        Assert.Same(metadata.CaptureCatalog, prepared.ScalarProgram.CaptureCatalog);
        Assert.DoesNotContain(InspectLibraryGraph(prepared), static value => value is Utf8OwnedRegexTree or Utf8TreeNode);
        // The analysis boundary retains its useful construction input.
        Assert.NotNull(original.RuntimeTree.Root);
    }

    [Fact]
    public void RetainedSemanticStateHasFiveReadonlyFieldsAndNoTreeSlot()
    {
        var type = typeof(Utf8PreparedSemanticRegex);
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var fields = type.GetFields(flags);
        Assert.Equal(5, fields.Length);
        Assert.All(fields, static field => Assert.True(field.IsInitOnly));
        Assert.All(type.GetProperties(flags), static property => Assert.Null(property.SetMethod));
        Assert.DoesNotContain(fields, static field => field.FieldType == typeof(Utf8OwnedRegexTree) || field.FieldType == typeof(CultureInfo));
        var preparedFields = typeof(Utf8PreparedRegex).GetFields(flags);
        Assert.Equal(20, preparedFields.Length);
        Assert.DoesNotContain(preparedFields, static field => field.FieldType == typeof(Utf8SemanticRegex));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MatchingAndReplacementKeepConstructionBoundScalarCaseAndCaptureIdentity(bool compiled)
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var regex = new Utf8Regex("(?<n>i.)", RegexOptions.IgnoreCase | (compiled ? RegexOptions.Compiled : RegexOptions.None));
            var catalog = regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog;
            Assert.NotNull(catalog);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var hit = regex.MatchDetailed("İ😀"u8);
            Assert.Equal((0, 6), (hit.IndexInBytes, hit.LengthInBytes));
            Assert.Equal("İ😀", hit.GetGroup("n").GetValueString());
            Assert.Equal("<İ😀>", regex.ReplaceToString("İ😀"u8, "<${n}>"));
            Assert.Same(catalog, regex.Inspection.PreparedRegex.ScalarProgram?.CaptureCatalog);
            Assert.DoesNotContain(InspectLibraryGraph(regex), static value => value is Utf8OwnedRegexTree or Utf8TreeNode);
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }

    internal static List<object> InspectLibraryGraph(object root)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var queue = new Queue<object>();
        var result = new List<object>(); queue.Enqueue(root);
        while (queue.TryDequeue(out var value))
        {
            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is string) continue;
            if (!type.IsValueType && !seen.Add(value)) continue;
            if (value is Array array)
            {
                foreach (var item in array) if (item is not null) queue.Enqueue(item);
                continue;
            }
            if (value is Delegate callback)
            {
                if (callback.Target is { } target) queue.Enqueue(target);
                continue;
            }
            // Opaque framework graphs are outside this library-field census.
            // Value tuples carry inline library references and must be traversed.
            if (!type.IsValueType && type.Assembly != typeof(Utf8Regex).Assembly) continue;
            if (!type.IsValueType) result.Add(value);
            for (var current = type; current != typeof(object) && current is not null; current = current.BaseType)
                foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (field.GetValue(value) is { } child) queue.Enqueue(child);
        }
        return result;
    }
}

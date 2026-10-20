using System.Reflection;
using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReTranslationLifetimeTests
{
    [Fact]
    public void ScalarOnlyInstancesDoNotRetainUnusedRenderedPatterns()
    {
        const string pattern = "(?P<α>a)?(?(α)b|)";
        var parse = new PythonReParser(pattern).Parse(PythonReCompileOptions.None);
        var rendered = PythonReTranslator.Translate(parse, true).NativePattern;
        Assert.NotEqual(pattern, rendered);
        foreach (var regex in new[] { new Utf8PythonRegex(pattern), new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern)) })
        {
            Assert.False(regex.DebugUsesUtf8RegexBackend);
            Assert.DoesNotContain(ReachableStrings(regex), text => text == rendered);
            Assert.Equal(["0", "α"], regex.GetGroupNames());
            var hit = regex.SearchDetailedData("ab"u8);
            Assert.True(hit.Success);
            Assert.Equal("a", hit.Groups[1].ValueText);
            Assert.Equal("α", hit.LastGroup);
            Assert.True(regex.FullMatchDetailedData("ab"u8).Success);
            Assert.Equal("a", regex.ReplaceToString("ab"u8, @"\g<α>", 1));
        }
    }

    [Theory]
    [InlineData("(?(missing)a|)")]
    [InlineData("(?:(?<=a+)b)?(a)?(?(1)b|)")]
    public void SkippingNativeRenderingStillValidatesReferencesAndLookbehind(string pattern)
    {
        Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex(pattern));
        Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern)));
    }

    [Fact]
    public void ScalarOnlyTranslationAdmitsLessWorkAndStorageWithoutChangingNames()
    {
        const string pattern = "(?P<α>a)?(?(α)b|)";
        var rendered = Translate(true);
        var scalar = Translate(false);
        Assert.Equal(string.Empty, scalar.Pattern);
        Assert.Equal(rendered.Name, scalar.Name);
        Assert.True(scalar.Work < rendered.Work);
        Assert.True(scalar.PeakBytes < rendered.PeakBytes);

        static (string Pattern, string Name, ulong Work, ulong PeakBytes) Translate(bool render)
        {
            using var scope = Utf8ConstructionScope.Enter();
            var parse = new PythonReParser(pattern).Parse(PythonReCompileOptions.None);
            var translation = PythonReTranslator.Translate(parse, render);
            return (translation.NativePattern, translation.EmittedGroupNames["α"], scope.Budget.WorkSteps, scope.Budget.PeakBytes);
        }
    }

    private static IEnumerable<string> ReachableStrings(object root)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<object>();
        pending.Push(root);
        while (pending.TryPop(out var value))
        {
            if (!seen.Add(value)) continue;
            if (value is string text) { yield return text; continue; }
            if (value is Array array)
            {
                if (array.GetType().GetElementType()?.IsPrimitive == true) continue;
                foreach (var item in array) if (item is not null) pending.Push(item);
                continue;
            }
            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum ||
                type.Assembly != typeof(Utf8PythonRegex).Assembly && type.Assembly != typeof(Utf8Regex).Assembly) continue;
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (field.GetValue(value) is { } item) pending.Push(item);
        }
    }
}

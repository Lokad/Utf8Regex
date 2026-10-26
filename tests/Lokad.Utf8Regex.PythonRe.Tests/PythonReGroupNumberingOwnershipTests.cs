using System.Text.RegularExpressions;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReGroupNumberingOwnershipTests
{
    public static IEnumerable<object?[]> NumberingCases()
    {
        string[] patterns =
        [
            "abc",
            "(a)",
            "(a)(b)",
            "(a)(b)(c)",
            "(?P<w>a)",
            "(?P<w>a)(b)",
            "(a)(?P<w>b)",
            "(?P<a>x)(?P<b>y)",
            "((?P<w>a)b)(c)",
            "(?P<w>a)|(?P<v>b)",
            "(a|(?P<w>b))",
            "(?P<w>a)+",
            "(?P<w>a)?b",
            "(?:a)(?P<w>b)",
            "(?P<first>a)(?P<second>b)(?P<third>c)",
            "x(?P<mid>b(c))y",
        ];
        foreach (var pattern in patterns)
        {
            yield return new object?[] { pattern };
        }
    }

    [Theory]
    [MemberData(nameof(NumberingCases))]
    public void ScalarSlotsFollowPythonOrderWhileRenderedTextKeepsCoreNumbering(string pattern)
    {
        var parser = new PythonReParser(pattern);
        var parseResult = parser.Parse(PythonReCompileOptions.None);
        var translation = PythonReTranslator.Translate(parseResult, true);
        var managed = new Regex(PythonReDotNetReference.Render(parseResult), PythonReDotNetReference.ToRegexOptions(parseResult.Options), Regex.InfiniteMatchTimeout);

        var expected = new int[parseResult.CaptureGroupCount + 1];
        var namesByPythonGroup = new string?[parseResult.CaptureGroupCount + 1];
        foreach (var pair in parseResult.NamedGroups)
        {
            namesByPythonGroup[pair.Value] = pair.Key;
        }

        var nextUnnamed = 1;
        for (var pythonGroup = 1; pythonGroup <= parseResult.CaptureGroupCount; pythonGroup++)
        {
            if (namesByPythonGroup[pythonGroup] is null)
            {
                expected[pythonGroup] = nextUnnamed++;
            }
        }

        var nextNamed = nextUnnamed;
        foreach (var pair in parseResult.NamedGroups.OrderBy(pair => pair.Value))
        {
            expected[pair.Value] = nextNamed++;
            var alias = "g" + pair.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(expected[pair.Value], managed.GroupNumberFromName(alias));
            Assert.Equal(expected[pair.Value], new Utf8Regex(translation.NativePattern, translation.NativeRegexOptions).GroupNumberFromName(alias));
        }

        var program = PythonReScalarCompiler.Compile(parseResult);
        Assert.Equal(Enumerable.Range(0, expected.Length), program.GroupNumbers);
        for (var pythonGroup = 1; pythonGroup < namesByPythonGroup.Length; pythonGroup++)
        {
            var name = namesByPythonGroup[pythonGroup];
            Assert.Equal(name is null ? pythonGroup.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : name, program.GroupNames[pythonGroup]);
        }
    }
}

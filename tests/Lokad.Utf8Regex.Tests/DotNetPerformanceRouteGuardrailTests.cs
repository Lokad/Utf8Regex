using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class DotNetPerformanceRouteGuardrailTests
{
    [Fact]
    public void SherlockEnglishLiteralFamilyStaysOnHybridAutomatonPortfolio()
    {
        var regex = new Utf8Regex(
            "Sherlock Holmes|John Watson|Irene Adler|Inspector Lestrade|Professor Moriarty",
            RegexOptions.None);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, regex.CompiledEngineKind);
        Assert.Equal(Utf8SearchPortfolioKind.ExactAutomatonFamily, regex.SearchPortfolioKind);
        Assert.Equal(Utf8SearchMetaStrategyKind.HybridSearch, regex.SearchPlan.CountStrategy.Kind);
        Assert.Equal(Utf8SearchMetaStrategyKind.HybridSearch, regex.SearchPlan.FirstMatchStrategy.Kind);
    }

    [Fact]
    public void SherlockEnglishMixedLiteralFamilyStaysOnHybridAutomatonPortfolio()
    {
        var regex = new Utf8Regex(
            "Sherlock Holmes|John Watson|Mycroft Holmes|Mary Morstan|Mrs Hudson",
            RegexOptions.None);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, regex.CompiledEngineKind);
        Assert.Equal(Utf8SearchPortfolioKind.ExactAutomatonFamily, regex.SearchPortfolioKind);
        Assert.Equal(Utf8SearchMetaStrategyKind.HybridSearch, regex.SearchPlan.CountStrategy.Kind);
    }

    [Fact]
    public void SherlockEnglishNoMatchLiteralFamilyStaysOnHybridAutomatonPortfolio()
    {
        var regex = new Utf8Regex(
            "Mycroft Holmes|Mary Morstan|Mrs Hudson|Sebastian Moran|Charles Augustus Milverton",
            RegexOptions.None);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, regex.CompiledEngineKind);
        Assert.Equal(Utf8SearchPortfolioKind.ExactAutomatonFamily, regex.SearchPortfolioKind);
        Assert.Equal(Utf8SearchMetaStrategyKind.HybridSearch, regex.SearchPlan.CountStrategy.Kind);
    }

    [Fact]
    public void SherlockChineseLiteralFamilyStaysOnPackedUtf8Portfolio()
    {
        var regex = new Utf8Regex(
            "夏洛克·福尔摩斯|约翰华生|阿德勒|雷斯垂德|莫里亚蒂教授",
            RegexOptions.None);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, regex.CompiledEngineKind);
        Assert.Equal(Utf8SearchKind.ExactUtf8Literals, regex.SearchPlan.Kind);
        Assert.Equal(Utf8SearchPortfolioKind.ExactPackedFamily, regex.SearchPortfolioKind);
    }

    [Fact]
    public void SherlockEnglishIgnoreCaseLiteralFamilyStaysOnIgnoreCaseFamilyPortfolio()
    {
        var regex = new Utf8Regex(
            "Sherlock Holmes|John Watson|Irene Adler|Inspector Lestrade|Professor Moriarty",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals, regex.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, regex.CompiledEngineKind);
        Assert.Equal(Utf8SearchPortfolioKind.AsciiIgnoreCaseFamily, regex.SearchPortfolioKind);
    }

    [Fact]
    public void LettersEnBoundedRepeatStaysOnStructuralRunEngine()
    {
        var regex = new Utf8Regex("[A-Za-z]{8,13}", RegexOptions.None);

        Assert.Equal(NativeExecutionKind.AsciiSimplePattern, regex.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.StructuralLinearAutomaton, regex.CompiledEngineKind);
        Assert.Equal(Utf8SearchKind.None, regex.SearchPlan.Kind);
    }

    [Fact]
    public void ContextBoundedRepeatStaysOnTokenWindowStructuralEngine()
    {
        var regex = new Utf8Regex("[A-Za-z]{10}\\s+[\\s\\S]{0,100}Result[\\s\\S]{0,100}\\s+[A-Za-z]{10}", RegexOptions.None);

        Assert.Equal(NativeExecutionKind.AsciiStructuralTokenWindow, regex.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.StructuralLinearAutomaton, regex.CompiledEngineKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, regex.SearchPlan.Kind);
    }

    [Fact]
    public void AwsFullRelationCaseStaysOnQuotedRelationStructuralEngine()
    {
        const string pattern =
            "(('|\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\").*?(\\n^.*?){0,4}(('|\")[a-zA-Z0-9+/]{40}('|\"))+|('|\")[a-zA-Z0-9+/]{40}('|\").*?(\\n^.*?){0,3}('|\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\"))+";
        var regex = new Utf8Regex(pattern, RegexOptions.Multiline);

        Assert.Equal(NativeExecutionKind.AsciiStructuralQuotedRelation, regex.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.StructuralLinearAutomaton, regex.CompiledEngineKind);
    }

    [Fact]
    public void RuffTweakedStaysOnByteSafeLinearFallback()
    {
        var regex = new Utf8Regex(
            "(?:# [Nn][Oo][Qq][Aa])(?::\\s?(([A-Z]+[0-9]+(?:[,\\s]+)?)+))?",
            RegexOptions.None);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.ByteSafeLinear, regex.CompiledEngineKind);
        Assert.Equal(Utf8SearchKind.FixedDistanceAsciiSets, regex.SearchPlan.Kind);
        Assert.True(regex.SearchPlan.PrefilterPlan.HasValue);
        Assert.Equal(PreparedSearcherKind.ExactLiteral, regex.SearchPlan.RequiredPrefilterSearcher.Kind);
    }

    [Fact]
    public void DictionarySingleStaysOnLiteralFamilyAutomatonPortfolio()
    {
        var regex = new Utf8Regex("absentmindedness|Zubeneschamali's", RegexOptions.None);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, regex.CompiledEngineKind);
        Assert.Equal(Utf8SearchPortfolioKind.ExactEarliestFamily, regex.SearchPortfolioKind);
    }
}

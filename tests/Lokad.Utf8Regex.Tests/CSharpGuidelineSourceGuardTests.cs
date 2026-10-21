using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lokad.Utf8Regex.Tests;

public sealed class CSharpGuidelineSourceGuardTests
{
    private const int DefaultParameterDebtCeiling = 0;
    private const int NullForgivingDebtCeiling = 0;
    private const int UndocumentedPublicDeclarationDebtCeiling = 0;

    [Fact]
    public void ProductionCSharpGuidelineDebtDoesNotGrow()
    {
        var syntaxFiles = ReadProductionSyntax();
        var defaultParameters = syntaxFiles.SelectMany(static file =>
            file.Root.DescendantNodes()
                .OfType<ParameterSyntax>()
                .Where(static parameter => parameter.Default is not null)
                .Select(parameter => Describe(file, parameter)))
            .ToArray();
        var nullForgivingExpressions = syntaxFiles.SelectMany(static file =>
            file.Root.DescendantNodes()
                .OfType<PostfixUnaryExpressionSyntax>()
                .Where(static expression => expression.IsKind(SyntaxKind.SuppressNullableWarningExpression))
                .Select(expression => Describe(file, expression)))
            .ToArray();
        var undocumentedPublicDeclarations = syntaxFiles.SelectMany(static file =>
            file.Root.DescendantNodes()
                .OfType<MemberDeclarationSyntax>()
                .Where(static declaration => IsEffectivelyPublic(declaration) ||
                    declaration is MethodDeclarationSyntax method &&
                    (method.Modifiers.Any(SyntaxKind.AbstractKeyword) || method.Modifiers.Any(SyntaxKind.VirtualKeyword)))
                .Where(static declaration => !declaration.GetLeadingTrivia().Any(SyntaxKind.SingleLineDocumentationCommentTrivia))
                .Select(declaration => Describe(file, declaration)))
            .ToArray();

        AssertDebtAtOrBelow("default-valued parameters", DefaultParameterDebtCeiling, defaultParameters);
        AssertDebtAtOrBelow("null-forgiving expressions", NullForgivingDebtCeiling, nullForgivingExpressions);
        AssertDebtAtOrBelow("undocumented public declarations", UndocumentedPublicDeclarationDebtCeiling, undocumentedPublicDeclarations);
    }

    [Fact]
    public void ProductionProjectsDoNotDeclareInternalsVisibleTo()
    {
        var root = FindRepositoryRoot();
        var offenders = Directory.GetFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(static path => File.ReadAllText(path).Contains("<InternalsVisibleTo", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();

        Assert.Empty(offenders);
    }

    private static bool IsEffectivelyPublic(MemberDeclarationSyntax declaration)
    {
        if (declaration is EnumMemberDeclarationSyntax)
        {
            return declaration.Ancestors().OfType<BaseTypeDeclarationSyntax>().All(static type =>
                type.Modifiers.Any(SyntaxKind.PublicKeyword));
        }

        return declaration.Modifiers.Any(SyntaxKind.PublicKeyword) &&
            declaration.Ancestors().OfType<BaseTypeDeclarationSyntax>().All(static type =>
                type.Modifiers.Any(SyntaxKind.PublicKeyword));
    }

    private static void AssertDebtAtOrBelow(string name, int ceiling, string[] offenders)
    {
        var details = offenders.Length <= 100
            ? string.Join(Environment.NewLine, offenders)
            : string.Join(
                Environment.NewLine,
                offenders
                    .GroupBy(static offender => offender[..offender.IndexOf(':')], StringComparer.Ordinal)
                    .Select(static group => $"{group.Key}: {group.Count()}"));
        Assert.True(
            offenders.Length <= ceiling,
            $"Production {name} grew from its reviewed ceiling of {ceiling} to {offenders.Length}:" +
            Environment.NewLine +
            details);
    }

    private static string Describe(SourceSyntax file, SyntaxNode node)
    {
        var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        return $"{file.RelativePath}:{line}: {node.ToString().ReplaceLineEndings(" ")}";
    }

    private static SourceSyntax[] ReadProductionSyntax()
    {
        var root = FindRepositoryRoot();
        var sourceRoot = Path.Combine(root, "src");
        return Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .Select(path => new SourceSyntax(
                Path.GetRelativePath(root, path).Replace('\\', '/'),
                CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path).GetRoot()))
            .ToArray();
    }



    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Lokad.Utf8Regex.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private readonly record struct SourceSyntax(string RelativePath, SyntaxNode Root);
}

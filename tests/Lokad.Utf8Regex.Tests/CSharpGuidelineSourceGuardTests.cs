using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lokad.Utf8Regex.Tests;

public sealed class CSharpGuidelineSourceGuardTests
{
    private const int DefaultParameterDebtCeiling = 0;
    private const int NullForgivingDebtCeiling = 0;
    private const int DefensiveNullGuardDebtCeiling = 0;
    private const int UndocumentedPublicDeclarationDebtCeiling = 425;

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
        var defensiveNullGuards = syntaxFiles.SelectMany(static file =>
            file.Root.DescendantNodes()
                .Where(IsDefensiveNullGuard)
                .Select(node => Describe(file, node)))
            .ToArray();
        var undocumentedPublicDeclarations = syntaxFiles.SelectMany(static file =>
            file.Root.DescendantNodes()
                .OfType<MemberDeclarationSyntax>()
                .Where(IsEffectivelyPublic)
                .Where(static declaration => !declaration.GetLeadingTrivia().Any(SyntaxKind.SingleLineDocumentationCommentTrivia))
                .Select(declaration => Describe(file, declaration)))
            .ToArray();

        AssertDebtAtOrBelow("default-valued parameters", DefaultParameterDebtCeiling, defaultParameters);
        AssertDebtAtOrBelow("null-forgiving expressions", NullForgivingDebtCeiling, nullForgivingExpressions);
        AssertDebtAtOrBelow("defensive null guards", DefensiveNullGuardDebtCeiling, defensiveNullGuards);
        AssertDebtAtOrBelow("undocumented public declarations", UndocumentedPublicDeclarationDebtCeiling, undocumentedPublicDeclarations);
    }

    [Fact]
    public void AbstractAndVirtualProductionMethodsDocumentImplementorSemantics()
    {
        var offenders = ReadProductionSyntax().SelectMany(static file =>
            file.Root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(static method =>
                    method.Modifiers.Any(SyntaxKind.AbstractKeyword) ||
                    method.Modifiers.Any(SyntaxKind.VirtualKeyword))
                .Where(static method => !method.GetLeadingTrivia().Any(SyntaxKind.SingleLineDocumentationCommentTrivia))
                .Select(method => Describe(file, method)))
            .ToArray();

        Assert.True(offenders.Length == 0, "Undocumented abstract/virtual methods:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
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

    private static bool IsDefensiveNullGuard(SyntaxNode node)
    {
        if (node is InvocationExpressionSyntax invocation &&
            invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name.Identifier.ValueText == "ThrowIfNull" &&
            memberAccess.Expression.ToString().EndsWith("ArgumentNullException", StringComparison.Ordinal))
        {
            return true;
        }

        return node is BinaryExpressionSyntax coalesce &&
            coalesce.IsKind(SyntaxKind.CoalesceExpression) &&
            coalesce.Right is ThrowExpressionSyntax
            {
                Expression: ObjectCreationExpressionSyntax objectCreation,
            } &&
            objectCreation.Type.ToString().EndsWith("ArgumentNullException", StringComparison.Ordinal);
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
        Assert.True(
            offenders.Length <= ceiling,
            $"Production {name} grew from its reviewed ceiling of {ceiling} to {offenders.Length}:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, offenders));
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
        return Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
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

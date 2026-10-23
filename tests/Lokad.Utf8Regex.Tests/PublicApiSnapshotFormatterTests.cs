using System.Reflection;
using Lokad.Utf8Regex.Tests.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Lokad.Utf8Regex.Tests;

public sealed class PublicApiSnapshotFormatterTests
{
    private const string Contract = """
        namespace ReviewFixture;
        public enum PolicyFlag { None = 0, Enabled = 1 }
        public sealed class Surface : System.IDisposable
        {
            public const int DefaultLimit = 100;
            public int Capacity { get; private set; }
            public void Run(int limit = 123) { }
            public void Dispose() { }
        }
        public readonly ref struct ValueView { public int Value => 0; }
        """;

    private static readonly MetadataReference[] References = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator).Select(static path => MetadataReference.CreateFromFile(path)).ToArray();
    private static readonly Lazy<string> Baseline = new(() => PublicApiSnapshot.FormatAssembly(Compile(Contract)));

    [Theory]
    [InlineData("Enabled = 1", "Enabled = 64", "Enabled = 64")]
    [InlineData("DefaultLimit = 100", "DefaultLimit = 200", "DefaultLimit = 200")]
    [InlineData("limit = 123", "limit = 456", "limit = 456")]
    [InlineData("private set;", "set;", "Capacity { get; set; }")]
    [InlineData("private set;", "init;", "Capacity { get; init; }")]
    [InlineData(" : System.IDisposable", "", "sealed class ReviewFixture.Surface\n")]
    [InlineData("sealed class", "class", "class ReviewFixture.Surface : IDisposable")]
    [InlineData("readonly ref struct", "ref struct", "ref struct ReviewFixture.ValueView")]
    [InlineData("readonly ref struct", "readonly struct", "readonly struct ReviewFixture.ValueView")]
    public void MaterialContractChangesCannotKeepTheSameSnapshot(string oldText, string newText, string expected)
    {
        var actual = PublicApiSnapshot.FormatAssembly(Compile(Contract.Replace(oldText, newText, StringComparison.Ordinal)));
        Assert.NotEqual(Baseline.Value, actual);
        Assert.Contains(expected, actual, StringComparison.Ordinal);
    }

    [Fact]
    public void PrivateAccessorIsNotPresentedAsPublic()
    {
        Assert.Contains("Capacity { get; }", Baseline.Value, StringComparison.Ordinal);
        Assert.Contains("readonly ref struct ReviewFixture.ValueView", Baseline.Value, StringComparison.Ordinal);
    }

    private static Assembly Compile(string source)
    {
        var compilation = CSharpCompilation.Create("ApiFixture" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source)], References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return Assembly.Load(stream.ToArray());
    }
}

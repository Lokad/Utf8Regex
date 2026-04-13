using Xunit;

namespace Lokad.Utf8Regex.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class Utf8RegexCacheCollection
{
    public const string Name = "Utf8RegexCache";
}

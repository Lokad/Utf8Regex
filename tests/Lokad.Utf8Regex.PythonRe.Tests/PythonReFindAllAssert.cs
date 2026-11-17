using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

// Test-side decoding of supplied owned bytes only; callers execute the public API directly.
internal static class PythonReFindAllAssert
{
    public static (Utf8PythonFindAllShape Shape, string[] ScalarValues, string[][] TupleValues, int Count)
        Decode(Utf8PythonFindAllUtf8Result result) =>
        (result.Shape, result.ScalarValues.Select(value => Encoding.UTF8.GetString(value)).ToArray(),
            result.TupleValues.Select(tuple => tuple.Select(value => Encoding.UTF8.GetString(value)).ToArray()).ToArray(), result.Count);
}

using System.Text;

namespace Lokad.Utf8Regex.Benchmarks;

internal static class BenchmarkFileWriter
{
    internal static void WriteTextAtomically(string path, string contents)
    {
        var temporaryPath = path + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}

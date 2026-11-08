using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Lokad.Utf8Regex.Tests")]
[assembly: InternalsVisibleTo("Lokad.Utf8Regex.DiffTests")]
[assembly: InternalsVisibleTo("Lokad.Utf8Regex.Benchmarks")]
[assembly: InternalsVisibleTo("Lokad.Utf8Regex.Pcre2")] // PCRE2-INTEGRATION-POINT
[assembly: InternalsVisibleTo("Lokad.Utf8Regex.Pcre2.Tests")] // PCRE2-INTEGRATION-POINT
[assembly: InternalsVisibleTo("Lokad.Utf8Regex.PythonRe")] // PYTHONRE-INTEGRATION-POINT
[assembly: InternalsVisibleTo("Lokad.Utf8Regex.PythonRe.Tests")] // PYTHONRE-INTEGRATION-POINT: deterministic shared-invocation qualification
[assembly: InternalsVisibleTo("Lokad.Utf8Regex.Posix")] // POSIX-INTEGRATION-POINT: shared scalar tree, lowering and admission
[assembly: InternalsVisibleTo("Lokad.Utf8Regex.Posix.Tests")] // POSIX-INTEGRATION-POINT: independent grammar/lowering checks

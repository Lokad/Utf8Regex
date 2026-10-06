# Lokad.Utf8Regex.PythonRe

`Lokad.Utf8Regex.PythonRe` is a `net10.0` side extension for `Lokad.Utf8Regex` that exposes a UTF-8-first Python `re` flavored API through `Utf8PythonRegex`.

It is intended for workloads where the input already exists as UTF-8 bytes and you need Python-style regular expression semantics on top of the base `Lokad.Utf8Regex` library.

## Support Scope

- Semantic target: `Utf8PythonRegex` models the standard `re` surface, while remaining a managed .NET library.
- Implementation model: parser-first translation. Python-style patterns are parsed into a dedicated AST before any backend is selected.
- Native rendering: the AST emits Unicode scalar atoms/ranges for `Utf8Regex`; native execution receives scalar spellings instead of UTF-16 surrogate-class compensation.
- Global operations: admitted scalar programs serve captured iteration, Python empty-match retry, substitution and split over the original UTF-8 bytes, with one operation deadline/token and owned capture snapshots.
- Case folding: admitted Python ASTs lower to the shared UTF-8 scalar VM with owned CPython 3.13.2 / Unicode 15.1.0 data. Literal classes include extra fold partners; references use lexical simple lowercase, with ASCII scopes restricted to ASCII letters. The four formerly skipped case-folding regressions are active.
- Migration state: unserved routes still retain managed-compatible rendering and compensation as temporary R04/R05 debt. Category-version reconciliation, finite result/output bounds and full engine independence remain unfinished.
- Dependency model: the only non-BCL implementation dependency is `Lokad.Utf8Regex`.
- Primary I/O model: the main API surface operates on UTF-8 `ReadOnlySpan<byte>`.
- Capture-aware APIs use PythonRe-specific result/context types, independent from the PCRE2 sidecar.
- Culture model: the profile is culture-invariant by construction; Python `re.LOCALE` is intentionally unsupported.
- Named Unicode escapes: `\N{...}` is intentionally unsupported to avoid embedding a large Unicode-name table in the package.
- `FindAll(...)` is a structural full-match collector.
- `FindAllToStrings(...)` provides CPython-shaped `re.findall(...)` string result shaping.
- `FindAllToUtf8(...)` provides the same host-facing shape with UTF-8 byte outputs.
- `SearchToString(...)`, `MatchToString(...)`, and `FullMatchToString(...)` provide host-friendly scalar helpers.
- `SearchDetailedData(...)`, `MatchDetailedData(...)`, `FullMatchDetailedData(...)`, and `FindIterDetailed(...)` provide non-`ref struct` match snapshots for runtime hosts.
- `ReplaceToString<TState>(...)` / `SubnToString<TState>(...)` support evaluator-driven replacement over match snapshots.
- `Subn(...)` / `Subn<TState>(...)` provide UTF-8 replacement outputs with replacement counts for bytes-first hosts.
- `Replace<TState>(...)` / `Subn<TState>(...)` also expose byte-oriented evaluator overloads for hosts that want to stay UTF-8-native end to end.
- `SplitDetailed(...)` exposes split output with segment/capture metadata.

## Public API

```csharp
using Lokad.Utf8Regex.PythonRe;

var regex = new Utf8PythonRegex(
    pattern: @"(?P<word>foo)-(?P=word)",
    options: PythonReCompileOptions.None);

bool isMatch = regex.IsMatch("foo-foo"u8);
var first = regex.Search("xx foo-foo yy"u8);
int count = regex.Count("foo-foo x foo-foo"u8);
byte[] replaced = regex.Replace("foo x foo"u8, "<\\g<word>>");
Utf8PythonMatchData[] matches = regex.FindAll("foo-foo xx foo-foo"u8);
var iter = regex.FindIterDetailed("foo-foo xx foo-foo"u8);
string?[] parts = regex.SplitToStrings("foo-foo xx foo-foo"u8);
```

String-returning helpers such as `SearchToString(...)`, `ReplaceToString(...)`, and `FindAllToStrings(...)`
are also available as host-friendly conveniences when a runtime needs scalar string results.

## Development Notes

- Benchmarks: [BENCHMARKS.md](./BENCHMARKS.md)
- Packaged qualification: run `./test-packaged-pythonre.ps1 -Configuration Release` from the repository root to pack a version-coherent core/companion pair and execute the complete PythonRe suite from an isolated NuGet cache.
- Project: [Lokad.Utf8Regex.PythonRe.csproj](./Lokad.Utf8Regex.PythonRe.csproj)
- Main runtime: [Utf8PythonRegex.cs](./Utf8PythonRegex.cs)
- Core match/result types: [PythonReCoreTypes.cs](./PythonReCoreTypes.cs)
- Parser: [PythonReParser.cs](./PythonReParser.cs)
- Translator: [PythonReTranslator.cs](./PythonReTranslator.cs)
- Support spec: [SPEC-PYTHONRE.md](../../SPEC-PYTHONRE.md)

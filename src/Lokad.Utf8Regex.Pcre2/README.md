# Lokad.Utf8Regex.Pcre2

`Lokad.Utf8Regex.Pcre2` is a `net10.0`, UTF-8-first managed companion to
`Lokad.Utf8Regex`. `Utf8Pcre2Regex` implements the repository's selected PCRE2
10.47 standard-matcher profile over `ReadOnlySpan<byte>` and `Span<byte>`.
The core `Utf8Regex` matches Unicode scalars over strict UTF-8 with byte coordinates.

Version 0.4.0-scalar.1 is an experimental integration candidate with a distinct
identity from published 0.3.0. Whole producer acceptance, common resource/result
policies and complete adapter qualification remain unfinished. Use the qualified
candidate's exact version, immutable feed and provenance manifest.

The package does not bind to native PCRE2. Its only non-BCL implementation
dependency is `Lokad.Utf8Regex`; there is no P/Invoke, native loader, RID
payload, or external matcher executable.

Release notes for the core and companion packages are tracked in the
[repository changelog](https://github.com/Lokad/Utf8Regex/blob/master/CHANGELOG.md).

## Supported profile

The generic PCRE2 compiler/runtime covers the admitted literal, character,
branching, repetition, capture, backreference, assertion, scoped-option,
branch-reset, duplicate-name, recursion/subroutine, conditional, atomic,
backtracking-control, `\K`, Unicode, and opted-in `\C` families. Normal
operations include `IsMatch`, `Match`, `MatchDetailed`, `Count`,
`EnumerateMatches`, `MatchMany`, and PCRE2 substitution.

`Probe` implements a curated partial-match profile and rejects unsupported
partial shapes explicitly. The DFA matcher, invalid-UTF subject mode,
callouts, split, full capture history, and substitution forms outside the
selected profile are not included. The executable corpus and backlog ledgers
are the authoritative capability boundary; support is never inferred from
similarity to .NET behavior.

Capture names start with a Unicode letter or underscore; later characters may
also be decimal digits. Names alias slots assigned in declaration order. Explicit
.NET numeric definitions such as `(?<100>a)` are invalid PCRE2 syntax and fail
construction with `Pcre2CompileException` / `InvalidAfterParensQuery`. This fixes
their accidental admission and inaccessible sparse capture results in 0.3.0.
Ordinary numbered groups, numeric backreferences, duplicate names and branch-reset
numbering retain their PCRE2 contracts. The core scalar profile is unaffected.

Patterns and subjects must be well-formed UTF-8. Public start positions and
raw match ranges use byte offsets. UTF-16 coordinates are exposed only when
the reported range is exactly projectable. Opted-in `\C` can return a byte
slice that splits a scalar, and opted-in lookaround `\K` can report a
non-monotone range; affected operations follow the explicit dispositions in
[`SPEC-PCRE2.md`](../../SPEC-PCRE2.md).

## Example

```csharp
using System.Text;
using Lokad.Utf8Regex.Pcre2;

var regex = new Utf8Pcre2Regex(
    @"(?<scheme>https?)://(?<host>[A-Za-z0-9.-]+)",
    Pcre2CompileOptions.None);

ReadOnlySpan<byte> input = Encoding.UTF8.GetBytes(
    "Visit https://example.com and http://localhost.");

var matches = regex.EnumerateMatches(input);
while (matches.MoveNext())
{
    var match = matches.Current;
    if (match.Success && match.IsUtf8SliceWellFormed)
    {
        Console.WriteLine(
            Encoding.UTF8.GetString(input.Slice(match.StartOffsetInBytes, match.EndOffsetInBytes - match.StartOffsetInBytes)));
    }
}

byte[] redacted = regex.Replace(input, "<$host>");
```

Replacement strings use PCRE2 substitution syntax, not .NET replacement
syntax. The API also exposes UTF-8 replacement patterns, callback replacement,
`TryReplace`, `ReplaceToString`, detailed numeric-slot/name-table results, and
explicit compile and execution settings.

## Resource-control scope

Controlled cursor copies keep independent positions while sharing the original
cumulative work/depth/working allowance and absolute deadline. Copying a native
or owned-slice cursor no longer creates another finite budget; earlier separately
funded alias advances can now fail. Separate public calls remain independent, and
retained ranges/captures/aliases remain valid within the subject's lifetime.
Working admission checks the deadline before acquisition, records requested peaks
including denial, and preserves outstanding reservations on refusal. The first
resource denial stays sticky with its PCRE2 kind. Exclusive scratch releases and
can reuse capacity; spent work is not refunded. Native step units, polling cadence,
diagnostics and bounded state stacks remain, with scalar-slice diagnostic hooks
excluded from native steps. Fixed enforcement metadata has separate scope.

Work units and enforcement depend on the selected managed route. Owned slice
template replacement with `None` match options and owned slice callbacks with any
match options use one cumulative invocation from discovery through capture
projection, output growth and final copying. Explicit-option slice callbacks use
the retained native program's detailed cursor, preserving each yielded capture and
the original empty/nonempty retry disposition without a second match query. Legacy
backtracking callbacks with captures or suppressed unreset
empty matches also share that invocation, including explicit match options. Their
existing detailed cursor supplies captures and MARK without a separate match query;
native VM steps retain their cadence and diagnostic meaning. The unchanged-subject
result on no match is admitted before
allocation and copied with the same token/absolute deadline. Callback buffer
controls, writer resets and final byte/text output share that allowance; nested
operations restore the outer scope. `MatchLimit` covers the combined work and
`HeapLimitInBytes` covers overlapping modeled working capacities. Cases formerly
funded by separate matching/writing allowances can fail earlier. The first resource
denial retains its PCRE2 error kind. Output publication releases working charges,
while returned arrays, capture views and aliases stay valid; it does not bound
results retained by the caller or process memory. Shared copied-cursor coordinate
maps and linear tables conservatively remain reserved for their invocation after
EOF or physical collection. Those credits never depend on finalizer timing; a
later consumer of the same finite allowance can deny earlier. Borrowed aliases and
retained views remain valid, and unpublished setup/exclusive scratch still release
synchronously.

Fixed slice `ReplaceToString` validates and decodes the staged UTF-8 bytes under the
same invocation. The returned string is admitted while staging remains live, and
there is no extra final encoded byte array. No-match text borrows the unchanged
subject while admitting its returned string. `MatchLimit` includes bounded final
validation/count/decode and `HeapLimitInBytes` includes modeled staging/string
overlap. Capacity charges can differ from the former copied-byte result followed
by detached decoding; final text work and capacity are now covered.

Initial validation/preparation, capture-free optimized replacement routes, legacy
fixed-template output and core-foundation callbacks still have separate
enforcement scopes. These qualified producers do not establish a common
operation-wide policy.
The PCRE2 profile still rejects `Utf8ResourcePolicy.MaxResultBytes`; core and
PythonRe accept their documented cumulative result-capacity policy. Cancellation,
timeouts, depth/backtracking limits and basic state-growth metering remain active.

Owned slice depth admission checks the prospective live frame count before growth,
including assertion markers. An exhausted depth limit prevents acquisition of the
denied frame and assertion repeat/continuation storage. Combined depth/heap limits
can consequently report depth earlier; cancellation and deadlines are preserved.

## Contract and development evidence

The public surface uses explicit overloads rather than default-valued
parameters. Its exact frozen form is
[`PublicApi.Shipped.txt`](../../tests/Lokad.Utf8Regex.Pcre2.Tests/PublicApi.Shipped.txt).
The package contract is [`SPEC-PCRE2.md`](../../SPEC-PCRE2.md), internal
ownership is described in [`ARCHITECTURE.md`](ARCHITECTURE.md), and release
evidence is recorded in [`QUALIFICATION.md`](QUALIFICATION.md). The generated
[`BENCHMARKS.md`](BENCHMARKS.md) page keeps the PCRE2-specific performance
snapshot, native PCRE2 baseline, dependency review, and scaling evidence local
to this companion.

Run `./test-packaged-pcre2.ps1 -Configuration Release` from the repository
root to pack a version-coherent core/companion pair, reject native or RID
assets, replace the PCRE2 test project's sibling references with package
references, and execute the complete PCRE2 suite from an isolated NuGet cache.

- Project: [`Lokad.Utf8Regex.Pcre2.csproj`](Lokad.Utf8Regex.Pcre2.csproj)
- Facade: [`Utf8Pcre2Regex.cs`](Utf8Pcre2Regex.cs)
- Result types: [`Utf8Pcre2CoreTypes.cs`](Utf8Pcre2CoreTypes.cs)

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

Parenthesized comments are ignored by the existing PCRE2 parsers and keep the
same native grammar and resource authority as their bodies. They do not change
default ASCII shorthand, declaration-order capture slots or scalar advancement.
Literal bodies retain the literal search/count kernel and fund required endpoint
projection. Comments end at the next closing parenthesis outside quoted/class
contexts; quantifiers and modifiers retain their association. See the
[PCRE2 pattern rules](https://www.pcre.org/current/doc/html/pcre2pattern.html#SEC27).
Documented core-grammar extensions (Unicode escapes/blocks and balancing captures)
compile with the core grammar and execute its admitted scalar program through the
existing PCRE2 slice driver. Discovery, capture projection, enumeration and
template/callback output share the original work/depth/heap, token and absolute
deadline. Formerly bypassed limits can deny earlier. Final captures, numeric/name
aliases and retained values remain valid. Global empty matches now try a nonempty
alternative at the same position before advancing by one scalar; for `\u0041*?`
on `A😀`, the ranges are `[0,0)`, `[0,1)`, `[1,1)`, `[5,5)`.
Broader per-operation option and partial-probe coverage remain separate profile
qualification work.

Literal probes now fund coordinate projection and the full-match group array with
their original work/heap, cancellation token and deadline. Full-array capacity is
admitted before allocation; partial results keep their inline range. Tight limits
can deny earlier. Non-partial probes reuse owned detailed captures, preserving
names and MARK, and forward native match options without an extra capture copy.
Specialized partial probes also share their original work/heap/token/deadline
through loops, bounded searches, endpoint projection and full-array admission.
Full, soft and hard decisions and scalar advancement remain; tighter limits can
deny earlier. Full `(a)b+` probes now retain the missing capture, adding one group
element to their owned snapshot, and full probes borrow compiled name metadata.
Partial ranges remain inline. Full-array publication also uses cumulative result
admission when a finite result policy is supplied.
Optional, lazy and possessive full probes retain every declared capture slot,
including unset slots and names. When a specialized helper supplies only group
zero, the existing native matcher obtains final captures at the selected start
under the same invocation limits and original search position. This adds native
capture work and storage to those full outcomes; partial decisions and inline
partial results retain their existing paths.
Non-partial specialized-pattern probes with explicit per-operation flags use normal
detailed matching. This fixes ignored anchoring
and line-boundary flags while reusing owned captures/names/MARK and the original
resource policy, token and deadline. Native flag-aware matching can require more
work/scratch than the former option-blind helper. Default helper paths retain their
existing algorithms. Specialized soft/hard helpers now restrict `Anchored` attempts
to the requested consumed start, including compiled anchoring. `\K` can report a
later start and lookbehind can read earlier subject bytes. Leading assertions use
the original subject; an offset creates no new beginning of line. Full/partial
precedence is preserved, with lazy successful alternatives no longer displaced by
a later partial. Lookbehind display text is excluded from partial substring zero.
Other per-operation flags and their combinations retain a separate release gate.

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

Scalar.6 development corrects native literal deadline polling at entry, the
existing bounded search chunks and return, including short hits/misses and empty
or anchored outcomes. Single-result coordinate projection shares the discovery
budget and checks before publication. Required endpoint work can exhaust a
MatchLimit that formerly admitted only discovery: a small ASCII literal first hit
needs one candidate step for IsMatch and three units for Match/MatchDetailed.
Work units remain route dependent. Cold execution and scheduling may consume a
real 1-ms deadline; small input never exempts an operation from Timeout.
The accepted scalar.5 core/PythonRe input remains an independent immutable feed.

Scalar.7 development joins literal-driver `ReplaceToString` publication to the
same discovery/output budget, deadline and token. Strict output validation and
bounded decoding admit the returned string while the existing encoded output and
range staging remain live. This can require a larger heap/work allowance than
the byte-returning replacement, whose algorithm and allocation behavior remain
unchanged. Capture/probe publication and native generic output now enforce public
finite result capacity. Initial subject validation and replacement
preparation are separate from this newly joined publication scope.

Native detailed templates and explicit-option slice template fallbacks also join
their original discovery work/heap/token/deadline through byte or string completion.
The existing detailed cursor supplies captures and MARK, preserving its empty
retry rules. No-match output is admitted before acquisition. Text completion uses
the existing staging buffer directly and avoids its extra final encoded byte
array. Unlimited hits promote the existing shared invocation owner once before
the writer copies it; controlled calls reuse their owner and retire the separate
writing allowance. Working overlap can deny earlier. Published capture arrays
retain their existing result-versus-working scope and stay valid after advancement;
cumulative result-capacity policy is enforced; arbitrary caller retention remains
outside its scope.

Work units and enforcement depend on the selected managed route. Owned slice
template replacement with `None` match options and owned slice callbacks with any
match options use one cumulative invocation from discovery through capture
projection, output growth and final copying. Explicit-option slice callbacks use
the retained native program's detailed cursor, preserving each yielded capture and
the original empty/nonempty retry disposition without a second match query. Legacy
backtracking callbacks with captures or suppressed unreset
empty matches also share that invocation, including explicit match options. Their
existing detailed cursor supplies captures and MARK without a separate match query;
native VM steps retain their cadence and diagnostic meaning. Capture-free native
callbacks also share the original invocation with their selected value cursor,
reusable group-zero array and byte/text output. The optimized literal, character,
repeat and family selectors remain. All callback output enters one scope; there is
no separate Unlimited output allowance. The unchanged-subject
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

Controlled global value-cursor coordinate projection now uses the existing bounded
scalar endpoint helper. `MatchLimit` includes those projection steps in addition to
matching; ASCII endpoint projection adds two steps per successful yield. Long
non-ASCII prefixes advance in at most 2,048-byte scalar-aligned windows under the
original work/deadline/token authority. Denied projection preserves the previous
published value and sticky failure. Native candidate/backtracking diagnostic counts
retain their meaning. These derived UTF-16 coordinates remain interoperability data;
matching and restart positions use UTF-8 bytes and Unicode scalars.

Fixed slice `ReplaceToString` validates and decodes the staged UTF-8 bytes under the
same invocation. The returned string is admitted while staging remains live, and
there is no extra final encoded byte array. No-match text borrows the unchanged
subject while admitting its returned string. `MatchLimit` includes bounded final
validation/count/decode and `HeapLimitInBytes` includes modeled staging/string
overlap. Capacity charges can differ from the former copied-byte result followed
by detached decoding; final text work and capacity are now covered.

The managed PCRE2 profile accepts positive `Utf8ResourcePolicy.MaxResultBytes`,
mapped to `Utf8Pcre2ExecutionLimits.ResultLimitInBytes` (legacy zero is unlimited).
The original invocation cumulatively admits modeled major capture/probe arrays,
replacement range/output staging and returned byte/string roots before allocation,
without refunds. `Pcre2ErrorKind.ResultLimit` reports denial; no truncated success
is returned. Conservative earlier denial is allowed. Scalar range/boolean/count
results and inline partial probes need no owned result capacity. Caller-provided
subjects/destinations, compiled metadata, VM scratch, standalone getters, caller
callback allocations and arbitrary retained memory have separate scope. This is
a capacity model, not process RSS. Published snapshots remain valid after denial;
independent calls reset the allowance, controlled copies share it.

Discovery and required capture/output shaping borrow the original authority.
Initial subject validation and replacement preparation retain separate safeguards;
public construction/default/live-retention policies remain unfinished. Cancellation,
deadlines, depth/backtracking limits and basic bounded state metering remain active.
Broader profile grammar/options/capture completion has separate qualification gates.

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

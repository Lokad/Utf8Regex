# Migrating to the Unicode-scalar 0.4 line

Upgrade `Lokad.Utf8Regex`, `Lokad.Utf8Regex.PythonRe` and
`Lokad.Utf8Regex.Pcre2` together when using more than one package. Both adapters
require exactly the same Core version (currently `[0.4.0-rc.2-dev]` in development). Previous private scalar candidates with
incompatible exact Core pins cannot be combined by overriding dependencies.
All packages target .NET 10. Development has no freshly qualified package candidate; frozen RC1 is unpublished and its PCRE2 bytes must not be published.

The development API removes the formerly public `Utf8CountDiagnostics` and
`Utf8IsMatchDiagnostics` records. Their only producers were internal benchmark/
test operations; they are now internal too. Code that constructed these records
must remove that dependency. Public matching and resource-control APIs remain.

## Match scalars and use byte coordinates

```csharp
using Lokad.Utf8Regex;

var dot = new Utf8Regex(".");
int count = dot.Count("A😀"u8); // 2, not a count of UTF-16 code units
var emoji = dot.Match("😀"u8);
// emoji.IndexInBytes == 0; emoji.LengthInBytes == 4
```

Dot, classes, quantifiers, captures, references, lookarounds, right-to-left
traversal and empty-match advancement operate on complete Unicode scalars.
Surrogate-half outcomes from a UTF-16 matcher are not the contract. Review tests
that compare against BCL Regex on supplementary characters; use explicit scalar
expectations where UTF-16 code-unit behavior differs. Core's ordered alternatives
do not implement POSIX leftmost-longest matching.

Use byte offsets with byte slices and start-offset parameters. In `é😀x`, `x`
starts at byte 6 and UTF-16 index 3. Derived UTF-16 coordinates/string helpers
support interoperability; they do not change the matching unit. Do not feed a
UTF-16 index to a byte-offset API. The PCRE2 profile separately documents its
opted-in `\C` byte atom and reported non-slice `\K` results; do not decode such
reported ranges without checking their slice-validity flags.

Manually constructed successful `Utf8ValueMatch` values now reject negative
active coordinates with `ArgumentOutOfRangeException`. Unsuccessful values still
ignore coordinates; values without byte alignment ignore byte coordinates.

## Validate the entire UTF-8 subject

Malformed, overlong, surrogate-encoding and truncated UTF-8 is rejected. Even a
match at the beginning does not bypass an invalid suffix. The library does not
silently replacement-decode malformed input. Controlled validation shares work,
cancellation and the absolute deadline; interruption or resource denial can win
before a later malformed byte is visited. Keep the subject unchanged during use.

## Choose explicit operation limits

```csharp
using System.Text.RegularExpressions;
using Lokad.Utf8Regex;

var regex = new Utf8Regex(@"(?<word>\w+)", RegexOptions.CultureInvariant,
    TimeSpan.FromMilliseconds(100), new Utf8ResourcePolicy
    {
        MaxWorkSteps = 1_000_000, MaxDepth = 256,
        MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576
    });
using var cancellation = new CancellationTokenSource();
int count = regex.Count("café tea"u8, cancellation.Token);
```

These numbers illustrate API use, not recommended universal budgets. Defaults
and static cached calls remain unlimited. A positive nullable value supplies a
cap; null is unlimited for invocation policies and zero is invalid. Select a
policy for your patterns, subjects and required outputs, and handle typed denial
without treating partial work as success. Units are modeled engine capacities
and work, not machine instructions, elapsed time, cumulative allocations or RSS.
Result/staging capacity is cumulatively charged before acquisition without
refunds; active working capacity includes growth overlap. More precise accounting
is not implied for incidental helper objects or shared storage.

Cancellation is synchronous and preserves the original token. A timeout is one
absolute invocation deadline, including time spent waiting between cursor moves.
Callbacks run in caller code; the engine checks interruption around them but
cannot preempt arbitrary callback execution. Copying/encoding returned output
remains subject to the covered operation controls.

Construction is a separate phase. Use each profile's explicit `Compile` factory
for a `Utf8ConstructionPolicy`, construction timeout and token; pass the execution
token again on later calls. Constructors retain fixed construction backstops.
Cold FullMatch/replacement preparation and retained caches have their documented
separate scopes. Finite execution caps alone are not a bound on construction,
compiled programs, arbitrary retained results or the entire process.
See [HOST-INTEGRATION.md](HOST-INTEGRATION.md) for mappings and host admission.

## Keep subjects and retained results alive correctly

Borrowed views/cursors need unchanged subject bytes and a live memory owner.
PythonRe's `CreateMatchCursor(ReadOnlyMemory<byte>, ...)` has one position and
lifetime across reference aliases: use `using` when stopping early. EOF, failure
and disposal release exclusive scratch synchronously; retained owned snapshots
remain valid. Core copied value cursors have independent positions and share
allowance/reservations. A copy does not create a fresh budget or deadline.

A host must separately admit retained results, programs, subjects, callback/getter
allocations and its own containers. Releasing a cursor does not invalidate an
owned snapshot or justify refunding a host reservation while that snapshot lives.
The Core/PythonRe W+R recipe in the host guide covers only the stated modeled
payloads, not complete application containment.

## Select the intended profile

- Core: documented .NET-style grammar with scalar semantics, owned capture
  history and a supported NonBacktracking subset. BCL Regex is a comparator for
  overlapping behavior, not an execution fallback or UTF-16 parity requirement.
- PythonRe: documented CPython `str` grammar, opening-order capture numbering,
  last-closed `LastIndex`/`LastGroup`, Python empty-match retry and result shaping.
  The pinned data/oracle target is CPython 3.13.2 / Unicode 15.1.0. Bytes-pattern
  semantics, LOCALE and `\N{...}` Unicode-name lookup are excluded.
- PCRE2: selected managed PCRE2 10.47 standard-profile behavior. Partial `Probe`
  support is curated. DFA matching, invalid-UTF subject mode, callouts, split,
  full capture history and unspecified substitution forms are excluded.
  Final-sigma uppercasing intentionally yields uppercase sigma rather than
  copying an observed native replacement anomaly.

Use the profile README/specification and qualified public fixtures for exact
coverage. None of these descriptions promises full flavor equivalence. Published
benchmark rows retain their own package/runtime identities and are not a blanket
performance claim for this release.

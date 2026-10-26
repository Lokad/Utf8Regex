# Thirty-fourth review — 2026-10-26

Checkout `36d3980a67e778928379ffb6ed72ca25d4ed67af`; current Core/PCRE2
Release products identify source `575eeea6`, still `0.4.0-rc.2-dev`.
Product source and PLAN/history are unchanged. Only ignored scratch projects
were built. This pass concentrated on compiler-visible borrowing contracts,
probe error boundaries, result ownership and existing tooling assumptions.

Two concrete API problems merit a bounded follow-up. The writer correction
also exposes one focused omission in the existing API snapshot. No additional
engine-wide purge or accounting redesign is justified by this review.

## 1. Generic PCRE2 partial probing returns unjustified ordinary results (P2)

In `src/Lokad.Utf8Regex.Pcre2/Utf8Pcre2Regex.cs:619`, a probe without a curated
partial implementation calls ordinary `Pcre2Runner.TryMatchDetailed` and:

- returns FullMatch when that ordinary operation finds a complete match;
- returns NoMatch when it finds no match but has a MARK value.

Ordinary matching cannot establish the Hard partial result, nor can a MARK prove
that partial matching found no viable prefix. Both branches produce wrong valid
public results instead of rejecting the unsupported operation.

Observed against the existing benchmark-private PCRE.NET 1.5.0 dependency and its
reported native PCRE2 `10.47 2025-10-21`:

| Pattern / subject | Mode | Current managed result | Native PCRE2 |
| --- | --- | --- | --- |
| `(?<x>a+)` / `aaa` | Hard | FullMatch | PartialMatch |
| `(?<x>a+)b\|a` / `a` | Hard | FullMatch | PartialMatch |
| `(*MARK:tag)(?<x>a+)b` / `aaa` | Soft | NoMatch | PartialMatch |
| `(*MARK:tag)(?<x>a+)b` / `aaa` | Hard | NoMatch | PartialMatch |

The second pattern contains a literal regex alternation `|`; the backslash in
the table only escapes the Markdown column separator.

The fixed nine-pattern/six-subject/three-mode comparison repeats with unlimited
and four-finite policies: 324 observations, 26 result-kind disagreements (13
witnesses repeated under both policies), 96 explicit NotSupported rejections.
All subjects are ASCII, so using the native comparator's string API introduces
no UTF-8 versus UTF-16 matching-unit disagreement. This is a semantic control,
not a throughput result. Native code remains only in the ignored probe; no
shipped dependency or package is changed. Exact identities and all observations
are in `probe-binary-identities.json` and `partial-native-witness.json`.

Bounded recommendation: remove the unsupported Hard fallback and the partial
NoMatch-from-MARK shortcut. Preserve the useful generic **Soft FullMatch** case:
`GenericBacktrackingProbeReturnsAvailableFullMatch` at
`tests/Lokad.Utf8Regex.Pcre2.Tests/Utf8Pcre2RegexConstructionTests.cs:261`
deliberately tests it, and Soft semantics do permit preferring a complete match.
Keep `Probe(None)`, curated partial drivers, literal partial matching, captures,
MARK reporting on supported results, strict validation and original invocation
controls. Reject cases whose partial answer cannot be established with the
existing NotSupportedException; do not build a general partial VM or silently
expand profile completion.

`SPEC-PCRE2.md:387` already requires explicit rejection outside the curated
partial profile. Clarify the proven generic Soft-full exception and mode-specific
boundary in the public profile documentation. A Hard request must not quietly
become a full-match request. This is a PCRE2 delivery issue, independent of
Core/PythonRe delivery; postpone that profile if necessary rather than blocking
the shared writer fix.

Focused future validation: the two wrong-result families, Soft full success,
explicit unsupported misses, None-mode MARK results, curated literal/partial
controls, each/all finite budgets, original pre-cancellation and absolute
deadlines. Do not benchmark incorrect old FullMatch/NoMatch outcomes as if they
were equivalent work. If runtime dispatch changes on preserved live paths,
compare those paths once with an unchanged control at the bounded milestone.

## 2. Replacement writers reject ordinary stack-buffer consumers (P2)

`Utf8ReplacementWriter.Append` takes unscoped ReadOnlySpan<byte>/ReadOnlySpan<char>
parameters at `src/Lokad.Utf8Regex/Utf8ReplacementWriter.cs:56/64`.
Inside an actual Core replacement callback with `ref Utf8ReplacementWriter`,
both natural forms fail compilation against the existing Release DLL:

```csharp
Span<byte> buffer = stackalloc byte[] { (byte)'o', (byte)'k' };
writer.Append(buffer);
```

The corresponding stackallocated char span fails too. SDK 10.0.401 reports CS8352
and CS8350: the public signature permits the input span to escape through the
ref-struct receiver. `writer.Append(buffer.ToArray())` compiles and returns `ok`,
but adds a caller-side array allocation. This restricts the direct UTF-8 writer
for ordinary formatting/temporary-buffer use, despite its implementation copying
or encoding all data synchronously into owned storage.

The common writer serves Core and PCRE2 evaluator callbacks. PythonRe's public
string-returning evaluator is a separate API; no claim is made that it uses this
writer. Source inspection of `Utf8SliceByteOutputBuffer.Append` confirms that
neither overload retains the input span. A separate scoped-signature fixture
accepts the same stack span at compile time. No product fix has been applied.

Recommendation: investigate adding `scoped` to these two public span parameters,
keeping their existing bodies and callback/reset/alias ownership. This is a
compiler-visible lifetime contract, not a need for unsafe pointers, another
writer, an array-only overload or a new runtime guard.

Focused future acceptance: compile and run real Core/PCRE2 callbacks using local
byte and char stack spans, including sliced formatted output; retain UTF-8
validation, reset/alias behavior, finite output admission and original-token/
deadline checks. Update the reviewed public API contract. If emitted method
bodies are identical, establish that directly rather than starting a throughput
campaign for a metadata-only change. Actual candidate/package checks remain at
their normal delivery boundary.

`append-probe/Program.cs`, writer-byte-build.log, writer-char-build.log and
writer-control-build.log preserve the real consumer failures and array control.
scope-fixture/ and scoped-call/ preserve the positive compiler control.

## 3. The current API snapshot cannot see this lifetime contract (P3, with item 2)

`tests/Common/PublicApiSnapshot.cs:100` formats parameter type, ref/in/out/params,
name and default, but not ScopedRefAttribute. The actual existing formatter
produces identical text for two fixture assemblies whose sole declaration change
is `Append(ReadOnlySpan<byte>)` versus `Append(scoped ReadOnlySpan<byte>)`.

Reflection confirms ScopedRefAttribute exists only in the scoped fixture. Both
snapshots nevertheless contain:

```text
ref struct ReviewFixture.BufferSink
  method Void Append(ReadOnlySpan<Byte> value)
```

Thus the proposed writer correction, or a later accidental loss of that
correction, would be invisible to the current declaration guard even though it
changes whether real consumers compile. `api-scope-witness.json` records both
metadata observations and exact formatter outputs.

Extend the **existing** parameter formatter and its small mutation tests for the
relevant lifetime annotation alongside item 2. Keep the real consumer compilation
test as the acceptance for usability. This does not justify a complete CLR/C#
compatibility analyzer, another package qualification layer or an exhaustive
attribute inventory. The snapshot already explicitly disclaims being complete.

## Secondary observations and boundaries

- Undefined PCRE2 execution option bits and partial modes are currently accepted:
  three pattern controls accept match/substitution bit 1<<20, while partial modes
  3/-1 return FullMatch on their hits (`options-witness.json`). Compile flags and
  settings already have closed validation. This is invalid-argument hygiene,
  not a demonstrated supported-input semantic or resource-admission failure.
  It ranks below the two reproduced usability/correctness issues; avoid another
  general validation framework or hot-path guards without a concrete need.
- SPEC-PCRE2's failure bullets still use the superseded phrase ".NET-semantic
  surface" for matching timeout exceptions. Current host docs/code distinguish
  PCRE2 invocation Pcre2MatchException/Timeout from eager construction's
  RegexMatchTimeoutException. Reconcile that wording when documenting the probe
  boundary; do not impose exception symmetry by changing working behavior.
- The durable Python cursor has explicit concurrent/reentrant rejection,
  synchronous cleanup and owned snapshots. Callback scope identity and existing
  name-array copies have real ownership consumers. No removal is justified by
  the paths inspected.
- 42 ordinary-probe controls and 40 CRLF/DotAll comparisons agree with detailed
  matching; the initially suspected generic search and dot-option problems were
  not reproduced. This is bounded evidence, not full PCRE2 qualification.
- The deferred overlap guard, closed checked-access/accounting/layout campaigns,
  immutable package candidates and existing package/cumulative-cost delivery
  gates are not reopened. Existing source CI remains historical for this review;
  no GitHub API request, VM access, product rebuild, full suite, benchmark run or
  package/snapshot refresh occurred.

Only ignored review artifacts were added. integrity-before.json/integrity-after.json
check unchanged plans, six product DLL/PDB files and all 18 protected historical
snapshot/RC1/handoff files. Tracked source and PLAN.md remain unchanged.

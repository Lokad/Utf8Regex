# Thirty-second review — 2026-10-26

Checkout `4aeb0a6e`; the existing Release products identify source `9ea498ad`.
Review only: product source and PLAN/history are unchanged. Scratch programs
reference the existing three DLLs; no product build, full suite, throughput
campaign, package/snapshot refresh, VM operation or GitHub API request was needed.
The local CPython control is the profile's target CPython 3.13.2.

Two bounded adapter issues merit follow-up. No new engine-wide cleanup is
justified by the routes examined. Existing release/package/cumulative performance
gates remain separate; neither observation establishes a public matching failure
or a failed resource limit.

## 1. Fixed finite adapter policies do not reach optional Core preparation (P2)

Core already omits unreachable unmetered verifier/runtime/emitted preparation for
instances with any fixed finite invocation limit. Adapters construct optional
Core instances through the overload without a resource policy:

- `src/Lokad.Utf8Regex.PythonRe/Utf8PythonRegex.cs:243`;
- `src/Lokad.Utf8Regex.Pcre2/Pcre2ExecutionArchitecture.cs:68`;
- the additional finite-language compiler at
  `src/Lokad.Utf8Regex.Pcre2/Pcre2FiniteLiteralLanguageAnalyzer.cs:77`.

The read-only witness constructs adapters with all four finite controls. For
`abc|xyz`, both profiles return the correct Count of two, yet their retained Core
has an unlimited resource policy, a verifier, `Utf8LiteralCompiledEngineRuntime`
and an actual emitted literal-family counter. PCRE2 retains that same Core in its
IsMatch/Count/Enumerate literal-family selections.

Those finite public routes do not use the unmetered runtime. PythonRe supplies
its invocation to Core's prepared-search/scalar helpers; see
`Utf8Regex.NativeLiteralInvocation.cs` and the adapter's scalar/UTF-8 workers.
PCRE2's IsMatch/Count/Enumerate branches in `Pcre2ExecutionArchitecture.cs` near
1939, 2309 and 2560 gate unmetered execution explicitly. Controlled Count uses
the borrowed search/scalar payload, and controlled enumeration uses backtracking.
These live search/scalar consumers must remain.

There is a smaller instance of the same preparation mismatch in PythonRe:
`_asciiLiteralPrefixDigitMatchPrefix` and the two ASCII DotAll FullMatch buffers
are prepared when the timeout is infinite, even when fixed numeric limits forbid
their one-shot native paths for the entire instance lifetime. Witnesses for
`prefix[0-9]+` and DotAll `pre.*post` show the same 6-byte and 3/4-byte payloads in
finite/unlimited instances. Their public Match/FullMatch results remain correct.
Entry guards in `Utf8PythonRegex.Value.cs` and `.OneShotText.cs` exclude these
kernels under finite limits. Their unrestricted measured fast paths stay useful.

One fixed eight-instance constructor-allocation observation per variant gives:

| Direct Core control | Unlimited preparation | Existing fixed-finite preparation |
| --- | ---: | ---: |
| `abc|xyz` | 78,248 B/constructor | 34,024 B/constructor |
| structural keyword/identifier family | 113,312 B/constructor | 53,312 B/constructor |

Every recorded sample has the listed allocation count. This demonstrates the
component-level cost of preparation that Core already knows how to omit. It is
not an adapter end-to-end saving, retained-heap measurement or execution-time
benchmark. The structural control uses its exact Core pattern; PythonRe renders
a different structural pattern, so its 60,000-byte difference is not an adapter
projection. `finite-core-preparation.json`, `finite-native-prefix.json` and the
adjacent probe sources preserve the observations.

Recommended investigation: carry the fixed profile policy into optional Core
preparation where its consumers permit, and avoid the lifetime-disabled native
prefix preparation. Reuse existing policy/preparation behavior, preserving major
admission, native search payloads and the caller's invocation. Do not remove Core
wholesale or introduce a preparation mode/framework. This is separate from the
already-deferred grammar-level optional-backend retention experiment.

An integration detail needs explicit checking: Core's policy influences
`Inspection.OwnedSliceProgram`, which PCRE2's `WithSlice` consumes. Passing policy
can therefore change operation selection, not just allocations. Qualify the
affected native/scalar selections and profile semantics before accepting a fix.
Use focused Release public IsMatch/Count/value/enumeration/replacement checks,
each/all finite controls, captures, strict UTF-8 and interruption. One bounded
public before/after comparison with an unchanged control should separate
construction from execution costs. No new package qualification per small change.

## 2. PythonRe's group aliasing is non-injective and leaks into owned metadata (P2)

`PythonReTranslator.BuildEmittedGroupNames` at 355 preserves ASCII names but
replaces other identifiers with `g` followed by the Python group number. It never
reserves those generated spellings against user names. For example:

```text
Python: (?P<α>a)(?P<g1>b)(?P=α)
Core:   (?<g1>a)(?<g1>b)\k<g1>
```

Two distinct Python captures become one shared Core capture number. CPython
accepts `aba` and rejects `abb`; the generated Core program does the reverse.
The collision reproduces with the opposite declaration order (`g2` plus a
second Unicode name), longer literals and an optional capture.

Public PythonRe IsMatch/Count remain correct in the twelve tested rows (six
shapes, unlimited/finite). Its scalar program uses Python slot numbers for
references, and captured result collection remains scalar. This review does not
claim a reproduced public semantic mismatch. It does demonstrate that the
auxiliary pattern changes the reference language and capture identity.

The collision also has a present eligibility cost. For the captured literal
without a reference, the generated `(?<g1>a)(?<g1>b)` has shared declarations and
declines complete native-literal discovery. Keeping distinct original names in
`(?<α>a)(?<g1>b)` makes that same discovery eligible. Both match `ab`. The current
Core already accepts the original Greek identifier; the ASCII-only
`IsDotNetSafeGroupName` restriction is unnecessary for that demonstrated name.
This is route evidence, not a measured throughput gain.

Separately, `PythonReScalarCompiler.cs:229` puts these Core aliases into the
owned scalar program's names, even though its captures/references use Python
numbers and the public facade keeps original Python names. `translation` is
otherwise not needed by that compiler. Consequently, even
`Translate(..., renderNativePattern:false)` builds an alias dictionary for owned
metadata. Backend naming has become an unnecessary dependency of scalar lowering.

Recommendation: keep original Python names in the owned program and make any
optional native aliases injective. Consider ordinal rendering for all named
captures using the existing slot/name metadata, which may remove the ASCII-name
checker, temporary sort/map and compiler translation parameter. Alternatively,
retain supported original names with a collision-safe mapping for the rest.
Do not assume that Core accepts every Python identifier merely because Greek
works; supplementary identifiers need focused coverage. Avoid another naming
framework or public compatibility switch.

Qualify colliding `gN` names in both declaration orders, BMP/supplementary names,
named/numeric references, conditionals, group names/numbers, LastIndex/LastGroup,
FindAll and replacement shaping against CPython on overlapping cases. Keep owned
snapshots, scalar semantics and strict UTF-8. Retarget tests that currently pin
scalar names to backend aliases. Preserve native candidate-search eligibility
and require any complete native result to have equivalent capture/reference
semantics. A bounded public comparison is needed before claiming a speedup.

Evidence: `name-collision.json`, `cpython-name-control.json`,
`core-original-name-control.json`, `complete-literal-eligibility.json` and
adjacent probe sources. CPython confirms distinct capture names/numbers;
the direct Core control confirms original-name support for the shown example.

## Checks that did not justify another cleanup

The existing literal/scalar preparation has real controlled consumers; deleting
the entire optional Core or live replacement interpreter would remove useful
work. Core's overlapping TryReplace destinations explicitly select admitted
owned staging before native emission. Existing writer scopes and persistent
cursor ownership have real lifetime/interruption roles.

`CanMatchNonEmpty` initially looks like an orphan in shipped source but has a real
benchmark inspection consumer. No deletion is proposed. A textual audit found
all 64 command names mentioned in AGENTS somewhere in current benchmark source;
this is not execution qualification of all commands. No new missing-command or
actionable stale-material finding emerged from that audit. Current package
READMEs, exact Core pins, host contract and checked-access inventory distinguish
development from historical RC1. Do not rewrite frozen receipts or reopen the
closed checked-access/accounting/layout/retention campaigns.

The known out-of-process BDN archive-discovery problem, VM disk-space issue and
fresh immutable package/downstream/cumulative-cost boundary remain recorded
follow-ups, not new findings from this review.

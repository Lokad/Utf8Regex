# Detailed ASCII validation reuse: deferred

Parent a900fe56, scalar.113-dev. This closed prototype changed only the existing
Detailed group-count shaping arm. After full `Analyze`, ASCII input with advertised
runtime support used the existing `TryMatchAsciiWellFormedOnly`; every decline,
non-ASCII input and diagnostic checkpoint hook retained `MatchCore`. Earlier
scalar/capture/RTL/NonBacktracking/Kelvin and finite-limit/token/deadline guards
were unchanged. No helper, engine, mode, allocation or accounting policy was added.

The prototype is **reverted**. Production still calls `MatchCore`. The retained
source changes are eight public test cases and correction of an obsolete BCL-route
comment. No immutable package, benchmark snapshot or Status changes.

## Evidence and decision

One quiet-VM public before/after pair and its sole identical candidate-first
confirmation are complete. Xeon 8573C CPU2, .NET 10.0.12, Release FullOpts with
ReadyToRun off; fixed driver and Core-DLL-only swap. Each phase checks results,
warms each lane for 120 ms, calibrates to 80 ms, and records five alternating
sample orders. Child cap 60 s, batch cap 140 s. Both quiet gates passed; all
processes exit 0. All 680 timed samples exceed 62.32 ms. FullOpts does not qualify
default PGO, the complete engine, or any immutable package.

Confirmation public medians (ordinary / compiled):

| Operation | Before -> prototype | Change |
| --- | --- | --- |
| Detailed, 32 B |280.92->180.26 / 284.17->180.27 ns| -35.8% / -36.6% |
| Detailed, 128 KiB hit |13.474->8.922 / 13.493->8.941 us| -33.8% / -33.7% |
| Detailed, 128 KiB miss |10.961->6.501 / 11.049->6.495 us| -40.7% / -41.2% |
| Whole capture, 32 B |456.18->369.40 / 456.42->372.69 ns| -19.0% / -18.3% |
| Whole capture, 128 KiB |13.708->9.247 / 13.784->9.307 us| -32.5% / -32.5% |
| Literal family, 32 B |351.92->239.38 / 354.21->242.73 ns| -32.0% / -31.5% |
| Literal family, 128 KiB |9.182->4.767 / 9.200->4.741 us| -48.1% / -48.5% |
| Count control, 32 B |118.32->140.56 / 118.84->132.00 ns| **+18.8% / +11.1%** |
| Count control, 128 KiB |9.825->9.804 / 9.883->9.888 us| -0.2% / +0.1% |
| Value control, 32 B |169.56->184.80 / 173.78->183.92 ns| +9.0% / +5.8% |

The initial pair also observes tiny Count +16.9%/+18.1%. Its source path is
unchanged, but the repeated public cost prevents an acceptable overall claim.
This does not establish a changed Count algorithm or its cause. Do not discard
the control, attribute it to proven noise, retune code alignment, add another
confirmation, or reopen the completed checked-access campaign. Defer the prototype
despite its repeatable Detailed benefit.

Allocations are unchanged: literal/family/Value/Count 0 B; whole capture 336 B;
finite Detailed 304 B; mixed Detailed 16,936 B. Confirmation finite/mixed controls
are within 0.9%, structural neighbor within 1.9%, and .NET predecoded within 1.4%.
The structural neighbor still enters the modified Detailed arm and is not an
unchanged instruction-path control. Decode-per-operation .NET is noisier (up to
1.446x sample spread in the first pair); retain its observations without a parity
claim. [All samples, allocations, identities and summaries](measurement.json).

## Correctness and exact inputs

The prototype passes focused Release Core 6,326 plus boundary/control 873,
PythonRe 94+65 and PCRE2 87 checks. All affected test outputs contain the measured
candidate Core DLL. The independent production/public preflight has 312 cases,
80 handled outcomes and zero mismatches; the fixed driver verifies 64 public
results per phase on Windows/Linux. Linux Release Detailed code is 3,846->4,123 B
and retains full validation before the new call. This is evidence, not promotion.

Eight retained public theories cover hit/miss, whole captures, literal families,
case folding/Kelvin, scalar coordinates and nonzero input slices, plus malformed
tails after hits and misses. Ordinary/compiled/RTL/NonBacktracking modes are
included. After restoring the production path, the focused Release shaping,
invalid-input and finite-literal tests pass (85, zero failures/skips).

Baseline Core SHA256:
91993F5EF4BFFFEEC322B5C91FEEB8262EF79E433663FC07061BC9E11D44DF07.
Prototype Core:
2A2C93C75010C4565B31D8F61D743F37A2EC4F4C940AC0A9BB8686AFE13CE9D7.
Driver:
F17C864477988173B26263E659E8A0A43E18D5331FD0B52B691EA165373C98B7.
Downloaded, SHA256-verified archives:
74EFEC75C68F4BA1BB60669D9D128DC2DEDC4663D8B301D05E29B56B8CEF6013 /
41E82B5AB41D75AC4A92ED25D56880FDC757D0947A468615D92F144C60A37C44.

Full patches/codegen/preflight/test receipts remain in
`artifacts/detailed-ascii-reuse-20261020/`. The frozen driver accepts
`--preflight <new.jsonl>` or one new measurement output path. Its existing
`CoreLibraryPath` build override selects an actual DLL without rebuilding Core.
This preserves reproducibility, not an instruction to repeat the closed trial
or a new qualification framework. No package matrix follows this experiment.

# PythonRe byte-offset collection and maintenance checks

Source: `0476e5f3` (summary retirement), `90629095` (PythonRe collection),
`3a77e699` (shared API snapshots). Development remains `0.4.0-rc.2-dev`.
No product package or published benchmark snapshot was created or refreshed.

## Delivered changes and contracts

PythonRe native bulk collection keeps nonzero starts in byte coordinates, using
the existing byte-offset invocation route. This removes the byte-to-UTF-16-to-byte
detour and its single-use GetUtf16OffsetOfBytePrefix helper. The old route built a
complete non-ASCII boundary map and immediately discarded it before native
discovery. The zero-offset native shortcut is preserved; no mapper, execution
mode, public overload or matching algorithm was added.

Complete-subject UTF-8 validation, scalar alignment, native literal/family
discovery, byte/derived coordinates, owned values, capture routes and original
invocation cancellation/deadlines/work/depth/working/result controls remain.
Actual UTF-16 interoperability APIs and required maps retain their consumers.
Malformed input still reports ArgumentException; this path now uses the owned
validator instead of first failing in a prefix decoder.

The three copied API formatters/path readers are replaced by one linked shared
test helper. The existing snapshots now capture enum values/underlying types,
flags, constants, optional defaults, public accessors/init, base types/interfaces,
readonly/byref-like/sealed facts and relevant method/parameter modifiers.
Operator methods are included rather than silently omitted as special names.
The formatter remains a readable reviewed contract, not a complete CLR
compatibility analyzer or replacement for exact package identity checks.

The fixture wrappers still inspect the assembly actually consumed. All three
projects link the helper through the existing Compile/Link mechanism, which the
detached consumer preparation already copies and hashes. Richer rendering of
the frozen before and current assemblies is identical for every profile: no
product public API changed. The reviewed snapshot changes describe existing APIs.

Deleted the unreferenced 106-line bench-summary.ps1 helper. Its first-row-per-
CaseId selection could mix jobs: the synthetic preserved CSV turns two 10x wins
into a reported 100x slowdown. Maintained paired CLI reports and JSON-backed
pages remain. There is no replacement report framework or script compatibility
switch. CHANGELOG documents the retirement and collection improvement.

## Validation

- PythonRe focused offset/resource/result ownership checks: 27 pass. New tests
  cover native literals/families, ASCII/supplementary prefixes, byte and UTF-16
  ranges, strings/UTF-8 values, EOF, malformed tails, scalar-interior starts,
  pre-cancellation identity and work denial. A small allocation-growth assertion
  rejects subject-sized native maps without pinning exact object charges.
- Full affected Release PythonRe suite: 3,918 pass in 10 seconds.
- Combined API/member/offset focus: Core 23, PythonRe 10, PCRE2 6 pass.
  Ten checker tests include independently compiled metadata mutations for enum
  values, constants/defaults, setter visibility/init, interfaces, sealed,
  readonly and byref-like declarations. The original checker witness missed
  these facts; the shared implementation distinguishes them.
- Stronger rendering of all three frozen before/current assemblies is identical
  (public-api-equality.json). These observations used matching source-built
  fixture binaries before final commit metadata; they are not package provenance.
- Final source `3a77e699` passes both Windows/Linux source CI:
  [run 39972986785](https://github.com/Lokad/Utf8Regex/actions/runs/39972986785).
  CI builds once per platform and tests all four source fixtures using matching
  binaries. Local experiments built affected projects and reused their binaries.
- Final benchmark/library closure and matching generated-page checks pass without
  snapshot writes. Four snapshots, nine frozen RC1 files and five original TODOs
  retain exact bytes. PLAN.md and PLAN.HISTORY.md remain ignored.

## Bounded public source-cost comparison: closed

The reused dual-load-context host compares frozen Core/PythonRe producer
`667d1725` with committed `3a77e699`. The source baseline was checkout `9971a8c0`;
its Core/PythonRe implementation is unchanged from the frozen producer (later
changes in between concern PCRE2 or documentation). Actual DLL/PDB identities
are preserved. Release .NET 10.0.13, Windows processor 20; 2.5-second warmup per
version/lane, 40 ms calibration target, alternating whole lanes. No concurrent
local builds/tests ran during timing. Public results/checksums agree first.

Nine pairs plus the sole 17-pair reversed-first confirmation; no third run.
The confirmation was warranted by the initial finite-control lane's noise.
Unicode-prefix subjects contain one é followed by 65,535 ASCII characters and
` foo bar`, with a nonzero byte start at the prefix end. The ASCII-offset case
uses a 65,536-character ASCII prefix. These are end-to-end public operation costs,
including materialization, not isolated kernel timings or broad profile rankings.

| Public operation | Initial time change | Confirmation | Bytes before/after |
| --- | ---: | ---: | ---: |
| FindAllToUtf8, literal, Unicode prefix | -98.25% | -98.33% | 270,608 / 176 |
| FindAll, family, Unicode prefix | -97.55% | -97.42% | 270,696 / 264 |
| FindAllToStrings, family, Unicode prefix | -97.55% | -97.55% | 270,648 / 216 |
| FindAllToUtf8, family, ASCII prefix | -17.72% | -20.78% | 216 / 216 |
| Control: family Unicode offset, four finite caps | +15.01% | +6.73% | 496 / 496 |
| Control: family zero offset | +0.31% | -0.13% | 216 / 216 |

The affected medians improve in both observations. Final controls fit the +10%
allowance, but their spread/drift does not establish a causal finite-limit cost
change: the finite execution branch is retained. Initial/final wall times are
37.25/41.86 seconds; minimum actual samples are 36.91/31.40 ms, below calibration
target, and maximum spreads reach 2.93x/5.38x. The large map-removal gains repeat;
small differences remain uncertain. No further timing follows.

The separate 18-row public allocation witness checks three prefix compositions,
two patterns and unlimited/live-token/four-finite controls. The corrected ordinary
route now matches the live-token route's 176/216-byte native result allocation;
finite calls preserve their existing 328/496 bytes and all values/coordinates.

This closes the scoped experiment. Fresh immutable common-version packages,
isolated consumers and actual-package/host finite-control plus cumulative <=10%
acceptance against fixed correctness-equivalent `844dcbad` remain deferred release
gates. Existing package evidence stays immutable. No complete local package/
platform matrix, reopened checked-access campaign or new qualification framework.

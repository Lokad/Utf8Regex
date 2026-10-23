# Lokad.Utf8Regex

`Lokad.Utf8Regex` is a `net10.0` library that matches **Unicode scalars over strict UTF-8 `ReadOnlySpan<byte>` / `Span<byte>`**, with byte offsets and lengths as the primary coordinates. The user selected this contract on 2026-10-06; exact .NET UTF-16 matching is superseded.

- Dot, classes, quantifiers, captures, references, lookarounds, RTL and empty-match advancement must respect complete scalar boundaries. A supplementary scalar such as 😀 is one dot match, with four UTF-8 bytes.
- `System.Text.RegularExpressions.Regex` is a test/benchmark comparator only where its code-unit behavior overlaps the scalar contract. Shipped BCL matcher/result dependencies are removed in the qualified development source; keep them absent and qualify uncovered behavior in the owned engine.
- Keep useful regex grammar, shared captures, resource admission and synchronous cancellation. UTF-16 coordinates/string APIs are derived interoperability conveniences, not the matching unit or a reason to convert every subject.
- Retarget old surrogate-half parity tests to explicit scalar outcomes. Remove obsolete unit encodings, VM instructions, selectors and parity-only tooling as their qualified scalar replacements land. Do not merely disable failing tests or preserve a dormant second engine.
- `SPEC.md` defines the target contract; current implementation coverage must be proved separately. `PLAN.md` records the active migration and cleanup gates.

## Turn off the .NET “terminal logger”

Use `--tl:off` avoids dynamic output and progress rendering.

```powershell
dotnet restore --tl:off -v minimal
dotnet build   --tl:off --nologo -v minimal
dotnet test    --tl:off --nologo -v minimal --no-build
```

## Prefer the test script

Use `./test.ps1` from the repository root. During experiments, select the affected
test project with `-Project`; its dependencies build incrementally. Reuse matching
configuration binaries with `-SkipBuild` for subsequent focused filters. The
default solution target is for meaningful delivery boundaries, when the complete
relevant configuration/platform/package checks are due. Normal runs do not stop
build servers or test hosts; Windows file-lock retries recover repository hosts.
Test and benchmark script builds disable incidental NuGet packing; explicit
package qualification keeps its separate build/provenance/pack steps.
Windows lock recovery selects test hosts by executable paths inside this checkout;
command-line mentions alone do not establish ownership. Unknown or outside paths
are left running even if that prevents automatic recovery.
Ordinary builds reuse SDK build/compiler servers. Repository test-host recovery
is limited to an observed file-lock failure; do not disable shared compilation
or server reuse on every invocation.

```powershell
./test.ps1 -Configuration Release -Project tests/Lokad.Utf8Regex.Tests/Lokad.Utf8Regex.Tests.csproj -Filter 'FullyQualifiedName~Utf8FiniteLiteralCountTests'
./test.ps1 -Configuration Release -Project tests/Lokad.Utf8Regex.Tests/Lokad.Utf8Regex.Tests.csproj -SkipBuild -Filter 'FullyQualifiedName~Utf8CoreWorkPolicyTests'
```

Use `./qualify-package-candidate.ps1` at a package delivery boundary. It always
includes Core; select `-Profiles Core,PythonRe,Pcre2` for all three packages
(the default is Core/PythonRe). It builds only selected library roots (adapters
include Core), packs each selected package once, then runs detached fixture suites with
exact source/feed/cache/lock/DLL/symbol checks. `-SkipBuild` requires matching
configuration/revision binaries; provenance rejects stale assemblies. Clean
qualification also compares PDB source checksums and the assembly's observed
configuration; relevant untracked source/build inputs block new evidence. Clean
tracked source and a new common version are required. Successful candidates live
under `artifacts/package-candidates/<version>` and cannot be overwritten.
Historical scalar/release candidates remain immutable. The retired per-package
pack scripts and scalar-only entry point are replaced by this command; historical
receipts retain their original paths. Platform, public downstream and performance
acceptance remain separate milestone checks. Do not repeat source/package
qualification for unchanged code or immutable bytes.

Each profile fixture inventories the assembly it actually consumes, including
detached package runs. Core-only tests and qualification require neither adapter
DLLs nor the benchmark executable. Combined CI still tests all profiles.

## Run Benchmarks In Release

Benchmark work must be run in `Release`, not `Debug`, otherwise the performance numbers are misleading. Use commands such as:

```powershell
./bench.ps1 -Configuration Release -Job Short -Filter "*Literal*"
```

## Prefer the benchmark script

Use `./bench.ps1` from the repository root for the standard benchmark loop.

Important benchmark rule:

- Do not use the default fast `Dry` loop to judge performance parity.
- `Dry` is acceptable for quick smoke checks and “did this regress badly?” checks.
- For actual perf investigation, use a warm throughput run, typically `./bench.ps1 -Job Short`.
- Prefer filtering to the specific benchmark family under investigation rather than timing the whole catalog.
- Run benchmark commands sequentially, not in parallel.
- Use `./bench.ps1` rather than raw `dotnet run` for day-to-day perf work. BDN runs clear repository benchmark hosts/artifacts to avoid autogenerated file locks; custom CLI runs reuse their matching host and skip BDN cleanup. Normal runs never shut down build servers.
- Both routes launch the built DLL directly. Matching `-SkipBuild` hosts can be
  reused across platforms without a platform-specific apphost; BDN still builds
  its generated measurement worker through its normal toolchain.
- Child stdout and stderr stream through PowerShell's output stream, preserving
  ordinary redirection and the original exit code. The outer wall-time cap also
  terminates descendants; a cap returns exit code 124.
- Custom benchmark commands have a default 120-second outer wall-time cap. Use
  `-MaxWallTimeSeconds` to choose a smaller bounded investigation window, and
  use `-MaxWallTimeSeconds 0` only for a preflighted milestone refresh.
- Interpret results against both baselines:
  - decode-then-`Regex`
  - predecoded `Regex`

Current lesson from `IsMatch` work:

- cold-start-style numbers overstated the gap significantly
- warm throughput runs show that `Utf8Regex` already beats the decode baseline on several native `IsMatch` families
- the main remaining `IsMatch` gap is fixed-structure candidate search, not generic startup cost

Examples:

`powershell
./bench.ps1 -CommandArgs "--inspect-pattern","ab[0-9]{2}","CultureInvariant"
./bench.ps1 -CommandArgs "--inspect-case","structural/method-family-call"
./bench.ps1 -CommandArgs "--measure-case","literal/sherlock-en","20"
./bench.ps1 -CommandArgs "--measure-case","structural/method-family-call","20"
./bench.ps1 -CommandArgs "--measure-case-deep","lokad/lexer/doc-line","20"
./bench.ps1 -CommandArgs "--measure-case-deep","lokad/imports/module-imports","20"
./bench.ps1 -CommandArgs "--measure-case-deep","structural/keyword-family-to-capitalized-identifier","20"
./bench.ps1 -CommandArgs "--measure-case-deep","literal-family/method-token-family","20"
./bench.ps1 -CommandArgs "--measure-count-attribution-case","literal/sherlock-zh","20","5"
./bench.ps1 -CommandArgs "--measure-slice-policy-lanes","20000","5"
./bench.ps1 -CommandArgs "--dump-dotnet-generated-regex-case","lokad/lexer/doc-line"
./bench.ps1 -CommandArgs "--measure-utf8-validation-profile","three-byte-large","20"
./bench.ps1 -CommandArgs "--inspect-utf8-validation-route","industry/mariomka-email-count"
./bench.ps1 -CommandArgs "--measure-unicode-literal-case","literal/sherlock-ru","20"
./bench.ps1 -CommandArgs "--measure-token-finder-case","lokad/langserv/helper-identifier","20"
./bench.ps1 -CommandArgs "--measure-line-family-case","lokad/imports/module-imports","20"
./bench.ps1 -CommandArgs "--refresh-readme-case","common/email-match","20","5"
./bench.ps1 -CommandArgs "--emit-readme-benchmark-markdown","dotnet-performance,lokad","20","5"
./bench.ps1 -CommandArgs "--rewrite-readme-benchmark-markdown"
./bench.ps1 -CommandArgs "--emit-readme-parity-report"
./bench.ps1 -CommandArgs "--rewrite-readme-parity-report"
./bench.ps1 -CommandArgs "--verify-readme-parity-report"
./bench.ps1 -CommandArgs "--refresh-readme-benchmarks","dotnet-performance,lokad","20","5"
./bench.ps1 -CommandArgs "--resume-readme-benchmarks","dotnet-performance,dotnet-performance-compiled","20","7" -MaxWallTimeSeconds 0
`

README benchmark snapshot model:

- `README.Benchmarks.json` is the source of truth for published benchmark numbers.
- `README.Parity.json` is generated from that snapshot and classifies every
  ordinary/compiled lane against decode and predecoded baselines, including
  allocations and measurement provenance.
- `README.md` is regenerated from that JSON snapshot.
- Benchmark refresh flow is:
  - benchmark CLI measures selected cases or sections
  - JSON snapshot is updated
  - README markdown is regenerated from the snapshot
- Do not think of README refresh as editing inline table blocks directly anymore.
- Prefer selective refresh while working on perf:
  - `--refresh-readme-case` for one case
  - `--refresh-readme-benchmarks` for one or more whole sections when needed
- Use `--resume-readme-benchmarks` for an interrupted milestone refresh. It
  skips only rows whose paired measurements, requested iterations, samples,
  clean source revision, runtime, OS, processor, and tiering mode all match.
- README refresh warms through the actual timing loop before calibration.
  Warmup keeps its duration/call limits and uses bounded batches. New rows use
  AlternatingSixLaneV2; historical V1 rows retain their original provenance/status.
- README refresh calibrates one shared effective iteration count from all six
  target/baseline lanes, alternates lane order sample-by-sample, records the
  protocol and count in the snapshot, checkpoints after every case, and caps
  each child case at 120 seconds.
- Use bulk section refresh sparingly; it is slower and more likely to leave unrelated rows stale while you are iterating.

PCRE2 benchmark snapshot and diagnostics:

- `PCRE2.Benchmarks.json` is the PCRE2 benchmark source of truth for prioritization.
- `src/Lokad.Utf8Regex.Pcre2/BENCHMARKS.md` is generated from that snapshot and is the self-contained published view for the PCRE2 adapter. Do not edit its tables by hand.
- Prefer:
  - `--refresh-pcre2-benchmark-case`
  - `--refresh-pcre2-benchmarks`
  - `--refresh-pcre2-native-baseline-case`
  - `--refresh-pcre2-native-baselines`
  - `--rewrite-pcre2-benchmark-markdown`
  - `--verify-pcre2-benchmark-markdown`
  - `--emit-pcre2-priority-report`
  - `--inspect-pcre2-case`
  - `--measure-pcre2-compatible-case`
  - `--measure-pcre2-special-case`
  - `--measure-pcre2-construction-case`
  - `--measure-literal-family-selector`
  - `--measure-pcre2-workspace-pool-cost`
  - `--measure-pcre2-vm-metering-cost`
  - `--measure-pcre2-ismatch-scaling`
  - `--measure-pcre2-enumerator-phases`
- PCRE2 snapshot refresh uses case-dependent effective iteration counts, similar in spirit to the README refresh logic; do not assume one global floor fits every case.
- Native comparison is supplied only by the benchmark project's private `PCRE.NET` 1.5.0 dependency. Keep it out of every shipped project; unsupported or result-incompatible rows must carry an explicit exclusion reason instead of a timing.
- The workspace and VM-metering commands are attribution replays, not end-to-end
  benchmarks. Use their same-trip-count controls to reject weak mechanisms; do
  not treat their estimates as realizable top-line speedups.
- The IsMatch scaling command accepts `email` or `uri` and compares same-size
  hit/miss subjects in paired managed/native lanes. Use modest explicit trip
  counts while shaping a mechanism; it is diagnostic and does not set Status.
- The enumerator-phase command pairs adjacent managed prefix replays to split
  construction, successful and terminal `MoveNext` calls, and public range
  projection. Its differences are attribution evidence, not top-line Status.

PythonRe benchmark snapshot and diagnostics:

- `PythonRe.Benchmarks.json` is the small comparative snapshot for the optional Python-compatible adapter.
- `src/Lokad.Utf8Regex.PythonRe/BENCHMARKS.md` is generated from that snapshot and is the self-contained published view for the Python-compatible adapter. Do not edit its tables by hand.
- Use `--measure-pythonre-case <id> [iterations] [samples]` for one row.
- Use `--measure-pythonre-paired-case <id> [samples]` for the bounded
  non-persisted paired diagnostic against predecoded CPython. It calibrates
  the two runtimes independently, alternates whole paired lanes on one logical
  processor, records raw samples and harness floors, and does not rewrite the
  snapshot.
- Use `--measure-pythonre-paired-case-reversed <id> [samples]` for the same
  diagnostic with CPython first. Run it only after a normal paired result
  fails the lane-order gate; cross-process sub-sample slicing is deliberately
  not used because it disturbed cache residency in bounded trials.
- Use `--qualify-pythonre-case <id> [samples]` to checkpoint paired evidence
  and Status into the snapshot; use `--qualify-pythonre-case-reversed` for the
  predeclared reversed-first repeat.
- Paired qualification pins managed and CPython work to one common processor:
  Windows selects the least-contended highest-efficiency processor; Linux selects
  the least-contended allowed processor and records efficiency class as unknown.
  The scope restores the original affinity. Linux results do not claim a measured
  efficiency-class preference.
- Use `--resume-pythonre-qualifications [initial-samples] [extended-samples]
  [maximum-cases]` for a bounded milestone batch. It skips only exact current
  source/catalog/runner/runtime/interpreter matches, checkpoints every case,
  and automatically replaces an inconclusive 9-pair row with the predeclared
  17-pair extension. The default maximum is four cases so interrupted or noisy
  experiments stay manageable.
- Use `--emit-pythonre-priority-report` to rank qualified public CPython wins
  separately by relative and absolute gap, and to list qualified bytes-control
  engine wins without merging the two queues.
- Use `--measure-pythonre-shaping-case <id> [iterations] [samples]` to split
  detailed-match discovery, direct public projection, and staged projection.
- Use `--measure-pythonre-findall-phases <id> [iterations] [samples]` to split
  capture-free core enumeration or captured managed discovery, range
  collection, coordinate mapping, and returned-value shaping. Timed result
  lanes retain outputs and report checksum traversal separately.
- Use `--measure-pythonre-replacement-phases <id> [iterations] [samples]` for
  fixed Replace/Subn rows. It separates replacement parsing/translation,
  decode, count, replacement, UTF-8 encoding, current required work, a native
  UTF-8-core control, and a one-pass literal replay. The cumulative lanes are
  attribution controls, not additive estimates or Status evidence.
- Use `--measure-pythonre-one-shot-phases <id> [iterations] [samples]` for
  ASCII exact-literal Search/Match rows. It compares validation, raw literal
  search, direct core matching, generic zero-offset matching, predecoded
  `Regex`, and the public adapter while checking identical byte/UTF-16 ranges.
- Use `--measure-pythonre-one-shot-scaling <id> [minimum-iterations] [samples]`
  for the literal Search pair. It measures 64 B through 64 KiB hit-at-start,
  hit-late, and miss subjects against predecoded and bytes CPython controls in
  one long-lived worker, then reports robust slopes, local slopes, residuals,
  and modeled break-even sizes. It is diagnostic evidence, not Status.
- Use `--refresh-pythonre-benchmarks [iterations] [samples]` for the complete 90-row catalog.
- Use `--rewrite-pythonre-benchmark-markdown` after a snapshot repair and `--verify-pythonre-benchmark-markdown` in validation. Normal snapshot refresh rewrites the page automatically.
- Current PythonRe tooling uses schema10. Retired schema migrations remain in
  repository history; do not rewrite historical snapshots with current tooling.
- Use `--verify-pythonre-semantic-digests` to preflight the complete catalog
  plus the supplementary-plane detailed-coordinate sentinel against CPython.
- Use `--verify-pythonre-qualifications` to recompute paired statistics and
  Status and reject stale source, catalog, runner, interpreter, or pair identity.
- PythonRe records checkout/source fingerprints separately from loaded assembly
  producer versions and SHA256 identities. Paired freshness also requires the
  measured Core/PythonRe binary identities; older rows without those identities
  remain historical. Benchmark identity is reported while operation/shared
  protocol fingerprints retain their existing role. No snapshot is refreshed
  merely to add provenance. The source-cleanliness gate permits exactly the five
  preserved root TODO handoffs; unknown untracked source/data still blocks writes.
  Snapshot qualification and freshness/verification preflight loaded runner,
  Core and PythonRe producer revisions against relevant source/build files.
  Their adjacent portable PDBs must match the loaded PE/module and compiled
  document SHA256 checksums must match checkout source. This also rejects dirty
  builds whose source was later reverted; revision metadata alone is insufficient.
  Later documentation-only commits are accepted; differing or unresolvable code
  or missing/mismatched symbols requires matching Release binaries. These checks
  reuse compiler evidence; they do not add a measurement/protocol identity.
  Non-persisted diagnostics still report
  actual loaded identities and dirty state without claiming source qualification.
- PythonRe refresh and direct-case commands invoke an official CPython executable to measure the stdlib `re`/`_sre` oracle in a long-lived per-case process. They exclude interpreter startup and pattern compilation, and report predecoded plus strict UTF-8-decode-per-operation timings.
- Paired PythonRe qualification applies a one-million-call floor only to
  one-shot subjects at or below 128 UTF-8 bytes. This keeps routes that tier
  below 50 ns over the 20 ms sample gate. Those rows also receive five million
  bounded warm calls before sampling so late tier transitions do not enter the
  paired evidence. Other one-shot rows receive at least 100,000 warm calls.
  Replacement rows receive at least 10,000 warm calls because the counted
  exact-literal route was observed to tier after the former duration-only
  warmup; other result-heavy/global rows remain warmed by duration. Every
  managed lane gets its applicable warmup again after final calibration, then
  a bounded fastest-of-three duration confirmation. It is rescaled before
  sampling if the settled route falls below 30 ms.
- Set `UTF8REGEX_CPYTHON` to an explicit executable when `python` does not resolve to the intended CPython. The runner uses only the standard library and is not a shipped dependency.
- The managed UTF-8 adapter, CPython, decode-then-Regex, and predecoded-Regex columns are compared only on deliberately overlapping semantics. Enumeration and replacement rows include required result materialization.

Current intended uses:

- --inspect-pattern
  - inspect one raw pattern and optional regex options
- --inspect-utf8-case
  - inspect one case from Utf8RegexBenchmarkCatalog
- --inspect-case
  - inspect one case from either DotNetPerformanceReplica or LokadReplica
- --measure-utf8-case
  - run a quick warm measurement for one Utf8RegexBenchmarkCatalog case
- --measure-case
  - run the standard top-line measurement for one benchmark case id
  - this is the default entry point for direct case measurement
- --measure-case-deep
  - run the best available family-specific drilldown for one benchmark case id
  - dispatches internally to the appropriate literal-family, structural-family, prefix-loop, or whole-document drilldown
- --measure-slice-policy-lanes
  - run the R02a focused cost controls over the fixed slice catalog (IsMatch plus Count)
  - reports unlimited/no-token, finite-limits/no-token, and live-token lanes with per-lane results, routes, timings,
    and allocations
  - validates lane agreement within each profile before timing; defaults to a 20000
    iteration minimum and five samples with alternating sample order
  - warms at least 256 public calls before the existing duration warmup, calibrates
    each lane toward 50 ms, confirms duration three times after all lanes warm
    using sampling's GC preflight, and reports effective iterations plus actual
    elapsed ms; calibration caps at 2M calls unless the requested minimum is larger
  - core/PythonRe finite lanes now exercise all four invocation controls; raw
    samples and decode/predecoded .NET controls accompany end-to-end costs
- --measure-pythonre-handoff-bulk [iterations] [samples]
  - measures the Lython handoff's `(a)(b)` over 1K/5K/10K `ab ` repetitions:
    core Count, PythonRe FindIterDetailed/FindAllToUtf8 and cursor first/ten-result
    early disposal, unlimited versus all four finite invocation controls
  - verifies ranges/capture values first, retains outputs, reports raw warm
    alternating samples and allocations, and includes decode/predecoded controls
    for Count and UTF-8 tuple shaping; diagnostic only, does not set Status
  - uses the same public-call warmup and per-lane duration calibration as the slice
    command; the iteration argument is a minimum, and raw duration exposes short samples
- --measure-count-attribution-case
  - run a bounded, alternating production-route Count breakdown for one case
  - reports the selected ordinary/compiled kernels, required UTF-8 validation,
    same-trip validation plus kernel, public operations, and both predecoded .NET baselines
  - emits an untimed selected-route census of candidates, full verifications, matches,
    and whether the selected kernel intrinsically validates UTF-8
  - validates every Count result before timing and defaults to 20 iterations,
    five samples, and a retained 250 ms Tiered-PGO warmup over only the selected lanes
- --dump-dotnet-generated-regex-case
  - generate and dump the .NET source-generated C# for one benchmark case id
- --dump-dotnet-generated-regex-pattern
  - generate and dump the .NET source-generated C# for one raw pattern and option set
- --measure-utf8-validation-profile
  - benchmark UTF-8 validation kernels on frozen profiles
- --inspect-utf8-validation-route
  - emit an untimed full-corpus census of scalar widths, 32-byte block shapes,
    cross-block scalars, constrained runs, and estimated lean/wide policy transitions
  - use this before changing mixed-width validation; estimated policy counts describe
    the current shape policy and are not timed production-branch counters
- --measure-unicode-literal-case
  - benchmark the Unicode exact-literal path end-to-end, including validation and exact-literal kernel variants
- --measure-token-finder-case
  - benchmark the exploratory generic unanchored ASCII token-finder model
- --measure-line-family-case
  - benchmark the exploratory generic whole-document line-family model
- --measure-compiled-microcost-case
  - benchmark the compiled path with a backend/runtime microcost breakdown
  - works across public cases, Lokad script cases, and replica count cases
- --measure-compiled-construction-case
  - benchmark ordinary/compiled construction and first Count separately for
    one replica case
  - uses an explicit fixed instance count (capped at 16); do not replace this
    with a self-calibrating loop that constructs dynamic compiled code
- --emit-readme-benchmark-markdown
  - emit generated README benchmark markdown preview for one or more sections: dotnet-performance, dotnet-performance-compiled, lokad, lokad-compiled
- --rewrite-readme-benchmark-markdown
  - regenerate every README benchmark section from `README.Benchmarks.json` without measuring
- --emit-readme-parity-report
  - emit the generated machine-readable parity report from `README.Benchmarks.json`
- --rewrite-readme-parity-report
  - regenerate `README.Parity.json` from `README.Benchmarks.json` without measuring
- --verify-readme-parity-report
  - fail when `README.Parity.json` does not exactly match its benchmark snapshot
- --refresh-readme-case
  - measure one case, update `README.Benchmarks.json`, then regenerate the affected README output
- --refresh-readme-benchmarks
  - measure one or more sections, update `README.Benchmarks.json`, then regenerate the affected README output
- --resume-readme-benchmarks
  - continue an interrupted section refresh, skipping only current clean rows with matching measurement parameters
- --measure-readme-case-reversed
  - run the same calibrated six-lane replica measurement in reverse order; use it with the normal command to qualify rows in the 0.98–1.02 tie band without rewriting the snapshot
- --measure-readme-public-case-reversed
  - reverse the calibrated six-lane measurement for a public/common or industry case in the tie band

Short-case calibration rule:
- very short public `IsMatch` / `Match` rows use much higher iteration targets during drilldowns and README refresh
- README refresh calibrates those targets against the measured cost of the slowest paired lane and a bounded total case budget; input size alone never forces a slow row to execute the full target
- current maximum targets are:
  - `common/*` public `IsMatch` / `Match`: target `10000`
  - `industry/boostdocs-*` public `IsMatch` / `Match`: target `5000`
  - other public `IsMatch` / `Match`: target `2000`
  - public `Replace` / `Split`: target `1000`, or `2000` for `common/*`
  - Lokad prefix-loop drilldowns / refresh: target `20000`
  - short replica count rows used by `--refresh-readme-case`:
    - `literal` / `literal-family` / `structural` with input `<= 128 KiB`: target `20000`
    - other replica count rows with input `<= 128 KiB`: target `10000`
    - replica count rows with input `<= 512 KiB`: target `5000`
- rationale:
  - sub-microsecond and low-single-microsecond rows are too noisy at small iteration counts
  - slow rows retain the explicitly requested minimum but use a lower recorded effective count so one case cannot silently become a multi-hour experiment
  - if a short row looks suspiciously weak, trust the higher-floor rerun over an older low-floor snapshot

Command-surface rule:
- treat the specialized family drilldowns as internal implementation details
- prefer --measure-case-deep unless you are explicitly working on UTF-8 validation, token-finder, or line-family execution models

Custom CLI invocation rule:
- use `./bench.ps1 -CommandArgs ...` for custom drilldowns; it launches the built DLL directly
- the equivalent commands below require an already-built matching Release host:

`powershell
dotnet .\bench\Lokad.Utf8Regex.Benchmarks\bin\Release\net10.0\Lokad.Utf8Regex.Benchmarks.dll --measure-case-deep common/match-word 20
dotnet .\bench\Lokad.Utf8Regex.Benchmarks\bin\Release\net10.0\Lokad.Utf8Regex.Benchmarks.dll --measure-compiled-microcost-case lokad/imports/module-imports 20
dotnet .\bench\Lokad.Utf8Regex.Benchmarks\bin\Release\net10.0\Lokad.Utf8Regex.Benchmarks.dll --measure-compiled-construction-case literal-alternate/sherlock-casei-en 4 7
`

## PCRE2 isolation rule

If PCRE2 support is implemented, keep it removable by construction:

- put PCRE2 implementation under `src/Lokad.Utf8Regex.Pcre2/`
- ensure every PCRE2-specific public type, internal type, namespace, folder, and test fixture name contains `Pcre2`
- do not add PCRE2-only helpers anonymously to the core project
- mark any unavoidable core hooks with `PCRE2-INTEGRATION-POINT`
- keep the PCRE2 profile strictly managed: no P/Invoke, no `NativeLibrary`, no external PCRE2 binary, and no RID-specific native packaging
- the sole acceptable non-BCL implementation dependency for the PCRE2 profile is the existing `Lokad.Utf8Regex` library

## CI

Anonymous GitHub API calls are capped at 60/hour: space CI status checks minutes apart, never poll in a tight loop.

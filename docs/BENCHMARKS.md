# Benchmark scope and procedure

The README tables describe their recorded workloads, rather than the performance
of arbitrary regex patterns. Historical tables strongly favor pattern-specific
executors. Changing a quantifier, capture or delimiter can select a much slower
general engine. Removing those executors is planned; their historical ratios are
not a delivery requirement.

The representative generality catalog covers structural neighbors and everyday
patterns. Its primary operations are `IsMatch`, captures from a byte offset, and
cursor advance. The mode matrix includes a live token, deadline, finite resource
limits, their combination with caller-owned working storage, and `Compiled`.
Unmetered `Count` and construction are secondary measurements. This first catalog
has 24 Core patterns; exhaustive catalog neighbors and adapter catalogs remain
work in progress.

Inputs are caller-valid, unchanged UTF-8 across all profiles; regex operations
expose no validation mode. .NET receives predecoded text; a separate
lane includes UTF-8 decoding. Matching options and complete capture histories are
checked before timing. .NET byte-coordinate lookup is prepared outside timing;
the comparison includes public result handling, rather than only matching kernels.
Capture matching uses the public caller-buffer byte-start operation or one memory
cursor advance when the case requests capture history.

Run the existing Release driver, using the SDK from `global.json`:

```powershell
# Verify outputs across all modes without timing.
./tools/bench.ps1 -SkipBuild -CommandArgs '--measure-generality','','','verify'

# One bounded selection; operations: IsMatch, MatchCaptures, Cursor, Count, Construct.
./tools/bench.ps1 -SkipBuild -CommandArgs '--measure-generality','word/flexible-literal','Cursor'

# Include the catalog in the existing refresh; a selected phase is not a full refresh.
./tools/refresh-benchmarks.ps1 -Phase Generality -MaxWallTimeSeconds 900
```

The generality command reuses the existing independent lane calibration,
alternating measurement order and allocation probe. It retains three samples per
lane, targets 75 ms, and marks any lane with a sample below 20 ms unqualified.
Raw durations, iteration counts, allocations and loaded producer identities go
to a unique JSON file under `artifacts/benchmark-refresh/generality/`. A focused
selection neither replaces accepted README tables nor qualifies a package.

The initial VM generality phase took 609.9 seconds: 120 rows, 816 lanes, and no
sample below 20 ms. It used source `cf12bac0`, .NET 11 and the existing affinity
policy. The retained raw data is under
`artifacts/benchmark-refresh/generality-vm-20261116/`. Only a completed full VM
refresh establishes the 60-minute target. The earlier 47-minute run predates this
catalog; the enlarged full pipeline has not yet been timed. Keep experiments
selective and bounded, reuse matching binaries and build
servers, and compare equal public outputs. Use one ordinary producer per process
for before/after comparisons. Kernel attribution is not a public speedup.

Comparator scope matters: Python uses a long-lived official CPython worker over
`str`. PCRE.NET is a benchmark-only PCRE2 comparator. The existing focused
`--measure-pcre2-case <id> 1 3` command also compares explicitly labeled native
interpreted and JIT lanes for IsMatch and Count. It verifies actual JIT compilation
and equal results, uses native `NO_UTF_CHECK` and the managed caller-valid contract, and
reports token/finite/deadline costs separately. The legacy snapshots still use
their original interpreted comparator; this does not refresh or qualify them.
JIT catalog coverage and adapter generality catalogs remain pending.
The historical POSIX comparator is PCRE2's
ordered NFA, not a leftmost-longest POSIX reference; use it only for verified
overlap. Native interop floors and tiny subjects do not establish haystack parity.

For whole-match extents, `--measure-posix-case extent/ambiguous-haystack 3` and
`extent/word-haystack` use PCRE2's DFA with longest results first. Both cases
deliberately put the shorter alternative first and verify complete nonoverlapping
byte ranges before timing. They cover Count and cursor operation costs, including
token, finite/deadline and caller-funded lanes. These focused commands are part of
the Generality refresh phase. They do not refresh the old POSIX table.

This DFA reference uses PCRE.NET's predecoded **16-bit** public API, not its 8-bit
reusable match buffer. The byte-coordinate map is prepared outside timing and
the native API's substantial per-match workspace/result allocations remain in
the reported cost. It is an extent-correctness reference and a public-wrapper
cost observation, not a native matching-throughput parity claim. It supplies no
POSIX subgroup, locale or collation reference. Broader reference coverage and a
more representative reusable native throughput comparator remain pending.

Older investigation records remain available at the
`experiments-archive-20261116` Git tag. They describe historical producer versions;
their focused gains must not be multiplied into present performance ratios.

# Benchmark scope and procedure

The README tables describe the measured public workloads. A change to a
quantifier, capture or delimiter can change the cost. Use the generality catalog
to check nearby patterns and ordinary workloads under the same controls.

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

The 18 November 2026 full refresh on `fc72da76` completed in 57m02s on the quiet
VM. Core generality took 605.35 seconds and covered 120 rows and 816 lanes. The
run verified all catalogs, paired comparisons, lifecycle, scaling and resource
checks. Compact summaries are in [bench/Snapshots](../bench/Snapshots/). Raw
durations and the full receipt are under `artifacts/benchmark-refresh/`.

Keep experiments selective and bounded, reuse matching binaries and build
servers, and compare equal public outputs. Use one ordinary producer per process
for before/after comparisons. Report kernel attribution separately from public
operation measurements.

Comparator scope matters: Python uses a long-lived official CPython worker over
`str`. PCRE.NET is a benchmark-only PCRE2 comparator. The existing focused
`--measure-pcre2-case <id> 1 3` command also compares explicitly labeled native
interpreted and JIT lanes for IsMatch and Count. It verifies actual JIT compilation
and equal results, uses native `NO_UTF_CHECK` and the managed caller-valid contract, and
reports token, finite-limit and deadline costs separately. The canonical PCRE2
tables use the interpreted comparator. JIT catalog coverage and adapter generality
catalogs remain pending. The POSIX table also uses PCRE2's ordered NFA, restricted
to verified equal-result workloads. The focused DFA checks below cover longest
whole-match extents on haystacks.

For whole-match extents, `--measure-posix-case extent/ambiguous-haystack 3` and
`extent/word-haystack` use PCRE2's DFA with longest results first. Both cases
deliberately put the shorter alternative first and verify complete nonoverlapping
byte ranges before timing. They cover Count and cursor operation costs, including
token, finite/deadline and caller-funded lanes. These focused commands are part of
the Generality refresh phase.

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

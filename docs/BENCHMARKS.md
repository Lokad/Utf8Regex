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
lane includes UTF-8 decoding. Matching options and capture results are checked
before timing. The comparison includes public result handling with the .NET
byte-coordinate lookup prepared outside timing. `MatchSlots` uses the public
caller-buffer match from a byte offset and reads one range per participating
group. `Cursor` reads complete capture histories. Archived `MatchCaptures` rows
measure one cursor advance with histories.

Run the existing Release driver, using the SDK from `global.json`:

```powershell
# Verify outputs across all modes without timing.
./tools/bench.ps1 -SkipBuild -CommandArgs '--measure-generality','','','verify'

# One bounded selection; operations: IsMatch, MatchSlots, Cursor, Count, Construct.
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

## Complete refreshes on the quiet VM

The full refresh includes both-runtime tests, public catalogs, paired
comparisons, lifecycle, scaling and resource checks. It builds Release once and
reuses the binaries. The completed `refresh.json` records each step and the
total duration. A selected phase serves focused work. The one-hour target
applies to the complete run.

Set PATH to the pinned .NET 11 SDK and set `UTF8REGEX_CPYTHON` before starting.
Check actual available RAM and disk space, including `/dev/shm`. Retire archived
fixture scratch before a full run. Size the reservation for each focused
experiment to fit its workers and retained results.

```bash
timeout --kill-after=5s 3600s pwsh -NoProfile -File ./tools/refresh-benchmarks.ps1
```

This also caps builds and tests. Benchmark children share the remaining
budget. Keep the SSH session open until you have copied the evidence from
`/dev/shm` and checked the archive digest. Logout can clear the remote files.

Track compact summaries and generated README blocks. Keep full samples and
provenance under `artifacts/benchmark-refresh/`. Regenerate tables through the
existing commands. The Verify phase requires matching frozen paired evidence
and binaries, and CI checks the compact summaries offline. Preserve source
identities, uncertainty and lifecycle or scaling evidence on selective refreshes.
[Compact summaries](../bench/Snapshots/)

## Focused comparisons

Use Release with warm throughput, typically `-Job Short`. The Dry job serves
smoke tests. Custom commands default to a 120-second process-tree cap. The
benchmark code sets affinity after runtime startup so it sees the ordinary .NET
processor count. Start it without an outer `taskset`.

Select public workloads, a time bound and the unchanged control up front.
Use one ordinary producer per process. Stage the exact DLL and PDB together for
each producer and check that the DLL hashes differ and the driver, environment
and inputs agree. Retain per-process observations and allocations. Report
kernel attribution separately from public operation measurements.

For `common/match-word` and the Lokad prefix-match loops, Core returns a
whole-match byte range and .NET returns the first value from
`Regex.EnumerateMatches`. The runner checks both endpoints in ordinary and
compiled modes before timing. Timed lanes consume lengths in their native
coordinate units.

Core and PCRE2 refresh lanes calibrate independently. Core's six-lane protocol
targets 75 ms batches with one calibration confirmation. Samples below 20 ms
prevent Core qualification. Python comparison batches consume the result
checksum after timing.

Python uses a long-lived official CPython worker over `str`, with startup and
compilation outside timing. The canonical PCRE2 comparison uses the interpreted UTF-mode matcher
through benchmark-only PCRE.NET, with `NO_UTF_CHECK` on caller-valid text.
Keep incompatible native rows marked with an exclusion reason. Compact PCRE2
rows report `ReferenceMatchOptions` when recorded by the measured lane.
Historical rows keep their original measurements and metadata.
Python paired qualification checks the loaded binaries and PDB source
checksums against clean relevant inputs. Use the existing paired protocol and
refresh when the comparison has changed.

`--measure-pcre2-case <id> 1 3` compares labeled native interpreted and JIT
lanes for IsMatch and Count. It checks JIT compilation and equal results, and
reports token, finite-limit and deadline costs separately. JIT catalog coverage
and adapter generality catalogs remain pending. The POSIX table uses PCRE2's
ordered NFA for verified equal-result workloads. The focused DFA checks below
cover longest whole-match extents on haystacks.

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

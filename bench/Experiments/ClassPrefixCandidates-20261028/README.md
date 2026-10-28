# Scalar Count candidates after class and whitespace runs

4cee890a extends the existing Count candidate window to one leading scalar-class
run, whitespace and an exact ASCII literal. The prepared literal must align with
the actual instructions after those runs; a required fragment elsewhere is not
sufficient. The optional per-call proof is bounded and allocates no payload.

The existing prepared search finds the literal, then the actual VM class
predicate rewinds complete scalars without passing the current floor. Class and
whitespace may overlap: the scalar VM still tests every start in the window,
including later starts needed after a captured backreference fails. This is
candidate discovery, not acceptance or a replacement counting engine.

Full validation, original work/depth/working/result authority, cancellation and
deadline remain. Search windows and both rewinds are metered; EOF publication
checks the original token after the deadline/hook. VM scratch still needs
admission. Calls may now fit smaller work allowances because skipped starts are
not executed; numerical charges remain route/version dependent as specified.
RTL/NB, folding and unproved instruction shapes retain their prior routes.
Match, captures and cursor/result lifetimes are unchanged.

[Raw comparison](comparison.json), [console](comparison.txt), [driver](driver.cs.txt),
[project](driver.csproj.txt). Quiet VM, .NET 11 RC3, CPU2; immediately prior
2881306e versus 4cee890a. Four independent public Count lanes, 500 ms warmup,
three alternating approximately 50 ms samples, equal-result preflight and timed
checks. One comparison took **23.16 seconds**, within its 120-second cap;
no confirmation. The frozen cumulative release baseline remains a40bda03.

| Public Count | Before us | After us | Change | Allocation B/op before / after |
| --- | ---: | ---: | ---: | ---: |
| Sherlock word/space/literal, ordinary | 228474.100 | 525.852 | 434.5x faster | 3552 / 3552 |
| Compiled | 228622.000 | 513.490 | 445.2x faster | 3552 / 3552 |
| 32 KiB, finite limits and live token | 13775.825 | 30.142 | 457.0x faster | 3664 / 3664 |
| Dense mixed-scalar hits | 317.710 | 204.365 | 1.55x faster | 3552 / 3552 |
| Failed captured candidates, later successful start | 466.311 | 4.768 | 97.8x faster | 5744 / 5744 |
| Existing whitespace-prefix control | 90.624 | 88.555 | -2.3% elapsed | 1680 / 1680 |
| Unchanged English literal Count | 69.966 | 70.304 | +0.5% elapsed | 0 / 0 |

The full corpus has 594,930 bytes and 319 results. The program remains 15
instructions in both producer lanes; no new match representation was compiled.
The independent [five-sample README refresh](class-prefix-refresh.txt) reports
**58.16x -> 0.13x compiled**, **27.37x -> 0.06x ordinary**, both Win.
[Snapshot verification](class-prefix-verify.txt) passes. These ratios remain
distinct from the paired experiment's costs.

[Both-runtime Core checks](core.txt) cover Unicode word/category predicates,
supplementary scalars, overlapping class/whitespace runs, padded subjects,
nonzero scalar floors, captures/backreferences, unaligned required fragments and
unchanged scalar enumeration. Every work cutoff preserves the original scratch
loan; denied VM storage and malformed-tail validation remain authoritative.
Public cancellation/deadline checks reach search, both rewinds and failed
candidates; final-poll cancellation cannot publish Count. Affected
[PythonRe](python-net11.txt) and [PCRE2](pcre-net11.txt) public Count/resource/
cancellation checks pass. No public API or grammar contract change.

Exact baseline/candidate DLLs/PDBs, driver, raw results and generated snapshots
are retained in the
[immutable archive](../../../artifacts/experiments/class-prefix-candidates-20261028/parity-class-prefix-4cee890a.tar.gz),
SHA256 `845667B4EE9F65C107E2BECA5F169BAC8A7DEA83A3542276020507A8023E6546`.
No complete catalog or package matrix was repeated.

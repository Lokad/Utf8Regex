# Remove preparation for a short literal pair

Parentfabc865c, development118. The existing simple-pattern finder uses checked
span IndexOfAny over the two first bytes, then full StartsWith verification, for
exactly two nonempty, case-sensitive literals of at most16bytes. It retains the
original branch verifier and earliest candidate starts. Other families keep the
existing prepared search. No new helper, type, field, cache, owner or mode.

The concrete optional-prefix plan is [ab,b], not two one-byte needles. Its old
fallback rebuilt bucket/search maps once per unrestricted Count and per cursor
advance. The bounded pair needs no maps. SIMD first-byte filtering remains in
the BCL span primitive; shared-first-byte pairs still receive full verification.
Original native deadlines are retained, including a checkpoint on rejected
candidates. Finite/token-controlled discovery and numeric charges stay unchanged.
Strict full-input UTF-8, scalar boundaries, captures, RTL/NonBacktracking and
cursor/result lifetimes retain their existing selection and ownership contracts.
Actual Unsafe.SizeOf stays; the checked-access campaign is not reopened.

## One bounded public comparison — accepted

The [fixture](Program.cs) uses the existing calibrated public loop: quiet
Xeon8573C CPU2/.NET10.0.12, FullOpts/R2Roff, Core-DLL-only swap, same driver and
companion DLLs.120ms warmup,80ms target, five alternating lane-order samples,
60s phase/180s batch caps. One75.796s pair;600samples, minimum67.591ms.
No confirmation or further timing. Constructors/startup excluded; enumeration
consumes every public byte range and checks the complete range checksum.

Median ordinary unrestricted public operations, microseconds:

| Subject | Count before -> after | Full enumeration before -> after | Allocations Count / enumeration before -> after |
| --- | --- | --- | --- |
| Hit,13KB/2000matches |146.169->98.980|1543.885->200.471|1792/3585792B ->0/0B|
| Miss,13KB |175.277->92.573|179.091->100.799|1792/1792B ->0/0B|
| Dense false prefix,4099B/1match |49.876->46.630|56.537->52.728|1792/3584B ->0/0B|
| Short hit,8B/2matches |1.111->0.436|8.110->6.061|1792/5376B ->0/0B|
| Shared first byte,15KB/2000matches |160.145->104.788|1475.360->205.340|1720/3441720B ->0/0B|

Compiled results agree with these trends. All changed unrestricted lanes have
within-lane spread<=1.050 and no measured regression. The four unchanged .NET
predecoded/decode controls move-1.05..+0.25%. Finite/live routes retain their
allocations; their dense Count controls vary up to1.139x and move-4.6..-8.1%,
despite unchanged dispatch. Do not attribute those movements to this kernel or
claim universal finite parity. Other finite shifts remain small. Python/PCRE
neighbors stay within the10% allowance; PCRE pooled fractional B/op readouts
slightly differ, without an allocation-improvement/parity claim. The three-literal
neighbor pays the eligibility predicate and retains its1904B general preparation.

The strong affected results and stable true controls settle this decision despite
the explicitly retained finite-control noise. No confirmation is needed to
rescue or refine the result. These are scoped source FullOpts observations,
not default-tier, immutable117, CPython Status or whole-engine qualification.
Finite allowances50Mwork/depth1024/64MiBworking/128MiBresult are not host defaults.
Finite full enumeration still allocates about7.1MB for this2000-match workload;
the new unrestricted result does not solve that separate ownership/cost issue.

## Correctness and identities

Release Core564 focused plus6589 native/folding/validation/offset/deadline,
Python745 finite/native/persistent/ownership and PCRE94 grammar/resource/global/
cancellation/timeout checks pass with no failures/skips. Every test output loads
measured CoreB7D4336A. New public tests cover greedy/lazy priority, shared prefixes,
captures, RTL/NB/folding declines, borrowed offsets, false candidates and tails,
16/17-byte boundaries, malformed suffixes and warmed repeated Count allocation.
Untimed147 public Match/Count witnesses and21 actual plan records agree. Both
timed phases agree on25 first-range/subject and60 complete-operation preflights.
No tests merely assert private selector spelling or preserve a dormant engine.

Baseline CoreBAE88E2A657900B3DCE4BE61C88E4BED386CF58513F684E926575E600FBB841A
retains the previously accepted deadline inlining. Candidate
B7D4336AF24D6F4E4F8F9EF998E6364D11CF8EBD0674E90AB2ED7C9D0E51D926.
Driver2B427BA59DECA76F8F27B5C03526E0317C22966ABF0973D1D6CDE19FB2DEB692.
[Raw samples, identities, checks and summaries](measurement.json). Evidence archive
SHA256176EAB5F391970BB9688FF310A57CD7519BFC0041395D5878B541DB3724F3402.
Private probes/source/patch/logs:artifacts/simple-search-preparation-20261020.
The first diagnostic probe accessed NoMatch byte coordinates and stopped; its
partial log remains, and the corrected complete probe is authoritative.
No package rebuild/qualification, immutable artifact change or consumer pin edit.
Source CI [39378972339](https://github.com/Lokad/Utf8Regex/actions/runs/39378972339)
is terminal green on Windows/Linux for1b19b9df, completed2026-10-20T06:15:26Z;
[receipt](ci.json). It builds/tests source and is not immutable-package qualification.
Broader R06/R07/R08/R09/R10 remains open; do not poll this completed run again.

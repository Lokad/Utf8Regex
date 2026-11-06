# Complete literal windows through checked span search

Parent `ef845b82054e3bd7dbbf1e70c12ab92ea169d585`. Before is the accepted
[continuation classification](../ContinuationSigned-20261106/README.md) AFTER
Core graph, without rebuild. Only Core changes; six adapter/benchmark DLL/PDB
companions and the typed caller are identical in separate Default-context processes.

The previous [cutoff-only proposal](../FiniteLiteralWindow-20261104/README.md)
remains rejected: feeding17-32-byte needles to the existing last-byte-only search
caused a261x opposite-density cliff. This changes the primitive too. Exact17-32
byte search now uses the existing checked span IndexOf/LastIndexOf tier already
used above32 bytes. The9-16 short/anchor paths remain. Controlled complete-needle
windows can then extend to32 bytes without adding a selector, state, table,
helper or API. The old medium helpers retain real9-16-byte consumers.

A scan span still covers at most2048 bytes INCLUDING lookahead, with work admission
before each scan and original synchronous cancellation/deadline and terminal
publication checks. Strict whole-subject validation, scalar boundaries, captures,
RTL/NonBacktracking, aliases and major allocations retain their contracts.
Numerical work requirements can fall because complete bounded searches replace
separate first-byte candidate/comparison charges; [the contract](../../../docs/SPEC.md)
records that route/version-dependent accounting. This is not removed metering.

Focused Release Windows/Linux .NET10/11 checks pass: Core1435, Python206, PCRE127
per runtime plus95 scalar-fallback cases. Existing Count and detailed/value-match
matrix coverage now includes both density orientations at16/17/23/32/33-byte
boundaries, chunk edges and borrowed slices, including reverse captures. Work
cutoff denial, no partial publication, malformed tails, original interruption
and retained views remain covered. FullOpts candidate helper size stays1406B;
this is not an isolated-kernel performance claim.

ONE **101.370-second** A0/B0/B1/A1: startup sees eight CPUs thenCPU2 affinity,
.NET11RC3, unchanged worker1s/min16 producer and250ms/min4 BCL warmup,
independent60ms calibration, five alternating samples per lane/process. All180
owned samples exceed53.4494ms. Full typed results are consumed after timing;
all Count/range oracles agree. BCL lanes overlap results, not owned limits.

| Public operation | Before us | After us | Change | Before / after B/op |
| --- | ---: | ---: | ---: | ---: |
| Unchanged exact ASCII Count control | 2.49 | 2.46 | -1.06% | 0 / 0 |
| Actual Chinese Count finite/live | 1164.40 | 406.30 | -65.11% | 112 / 112 |
| Actual Chinese Count finite/deadline/live | 2067.78 | 415.23 | -79.92% | 160 / 160 |
| Dense-first Count finite/live | 442.72 | 1.21 | -99.73% | 112 / 112 |
| Dense-last Count finite/live | 1.15 | 1.21 | +4.90% | 112 / 112 |
| Reverse dense-first Count finite/live | 1176.80 | 1225.32 | +4.12% | 112 / 112 |
| Reverse dense-last Count finite/live | 1377.30 | 1420.21 | +3.12% | 112 / 112 |
| UTF8 captured Count finite/live | 2.72 | 2.56 | -5.70% | 112 / 112 |
| Unlimited23-byte Match on mixed input | 1.20 | 0.24 | -79.76% | 0 / 0 |

Both after-process medians beat both before medians on the gain rows. Both after
medians are slower on the three adverse rows. Retain the costs: about.06us dense-
last and48.5/42.9us on the reverse cases. The prior307us dense-last cliff is absent
from this measured workload; no broader worst-case speed guarantee is inferred.
Control-1.06% is retained without normalization. These are runtime-version public
operation observations, not attribution to one private instruction/loop.

Accept the substantial actual finite/deadline and value-match gains with these
local costs and unchanged allocations. No confirmation, tuned variant, new catalog
Status, package cycle or complete refresh. [Raw samples, process medians/spreads
and graph identities](cost-summary.json) remain authoritative. Immutable dev38
packages exclude this source change; a future package boundary needs a new common
version. Publication is not authorized.

Immutable artifacts/experiments/balanced-literal-window-20261106, verified off VM:

- evidence.tar.gz: `B170513C994D259ABAF88AAF6D7D66DC2E687C6828536F31896C1D689142043E`.
- windows-evidence.zip: `ED082E5FB15CF69731D12AD7B30C94565CF1A7DB2C46CC255E5AF52F5F0F21BE`.
- Declaration: `E6E7CE23B8CB95EEA429E9307FEBBAE029036616D2F6DA609D1094F88821CB26`.
- Source/test patch: `9547B8CC557F5C39848F7C19D0F320892F698267A960271B5B0E68F7845CB250`.
- Before Core: `B187664C06CC2C5138A03AF85464923C1225F9A24D8F62486811BFC19BD4771D`.
- After Core: `3438719DDA4E14A1E662922D082409C562D962C51EB8B974943148BB4C593C72`.

Original dev38 Full graph, prior graphs/raw, staged packages and symbols remain
unchanged. Source CI is checked after committing.
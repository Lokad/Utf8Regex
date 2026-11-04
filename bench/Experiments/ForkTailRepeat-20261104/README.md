# Greedy class runs before literal alternatives

**Promoted.** The existing bounded greedy tail-exclusion proof now accepts a
plain forward Fork when both alternatives first consume exact literals excluded
by the repeated scalar class. Two capture-marker walks are bounded at32 inspected
instructions each. The existing RunClassRepeat consumes the run; the VM still
executes ordinary alternatives, captures and rollback. Nullable/opaque/folded tails
and any overlapping literal retain backtracking. No dispatcher, opcode, recognizer,
cached metadata, owner, engine or unchecked API was added.

The actual imports program has43 instructions. Its initial whitespace repeat
previously declined the optional-group Fork; both branches require 's' or quote,
which whitespace excludes. Path-class/CaptureEnd/quote and final dot/end fusions
already existed. Removing this dead rewind eliminates journal capacity while the
remaining branch frames stay owned and admitted. Actual work/depth/working
requirements can decrease; proof checks and scalar work remain metered. Public
policies, result admission and original synchronous interruption are preserved.

One **78.7152778s**,15-case public A/B against the frozen dev.27 Full Core,
source `11a26e26`. Six matching Full companion DLL/PDBs are byte-identical; only
Core changes, from exact source overlay `DD147238`. Normal8CPU startup thenCPU2,
managed1.5s/min128 and diagnostic250ms/min64 warmup, independent60ms calibration,
five alternating samples. **150 owned samples, minimum45.1111ms**; typed outputs
are consumed after timing. [All observations](cost-summary.json) retain spreads,
allocations and identities. BCL same-options lanes are equal-result diagnostics
on these subjects; scalar semantics are tested separately. Detailed timing includes
record construction and coordinate consumption, not extra group-to-string calls.

| Public operation | Before us | After us | Change | B/op before -> after |
| --- | ---: | ---: | ---: | ---: |
| Actual imports Count |13.96|12.33|-11.62%|3568 ->1952|
| Actual imports compiled Count |14.11|12.34|-12.55%|3568 ->1952|
| Actual imports finite/live Count |16.81|14.81|-11.92%|3680 ->2064|
| Actual imports deadline/live Count |18.36|16.80|-8.46%|3728 ->2112|
| Actual imports MatchDetailed |160.32|155.88|-2.77%|197976 ->196360|
| Dense ASCII Count |918.42|248.78|-72.91%|3552 ->1936|
| Unicode Count |432.51|409.40|-5.34%|3552 ->1936|
| Overlapping second tail |303.47|324.67|+6.99%|3552 ->3552|
| Folded tail declined |408.42|421.43|+3.19%|3552 ->3552|
| Tiny Count |0.78|0.60|-22.53%|3552 ->1936|
| Capture-dependent detailed |1.34|1.12|-16.68%|6384 ->4768|
| PythonRe dense Count |942.74|273.32|-71.01%|5744 ->4128|

Retain RTL control +9.98%(1114.86 ->1226.07us,3552B), native Count +0.24% and
PCRE Count -0.72%. Do not assign causal credit to unchanged controls. Actual Count
before spreads reach1.11, after1.05; tiny/detailed spans reach1.16. These uncertain
small-case estimates are not precise promises. Dense gains and1616B reductions are
clear; no confirmation, tuned variant, full catalog or package replay is warranted.
Local costs are accepted for substantial gains, not against a universal10% gate.

Read-only public inspection on .NET10/11 Windows/Linux observes the scalar runner
with diagnostics; static shapes do not prove unhooked hit counts or speedups. VM
corpus46429B/SHA507F5A33 matches benchmark output file-for-file; Windows47858B/
SHA32957950 preserves CRLF. The timed driver uses the frozen Linux catalog corpus,
19matches. No stale-output defect was found.

Windows/Linux .NET10/11 each pass1240 focused Core checks, Python176 and PCRE165.
They cover scalar bounds, overlapping/opaque/folded tails, capture history and
dependencies, atomic/balancing/assertion behavior, RTL/NonBacktracking, retained
cursor aliases, every work cutoff/parent loan, working denial, malformed full tails
and original matching cancellation/deadlines. Initial new test expected an internal
deadline exception from a public operation; corrected to RegexMatchTimeoutException
and failure retained. The interruption test does not depend on a helper name.

Immutable `artifacts/experiments/fork-tail-repeat-20261104/`:

- evidence.tar.gz: `E9FD70464CE9348E321857C09884B640BFDBBCDE2147DAA3D90A070C0ED12410`.
- windows-correctness.zip: `04BFDBC669981E8F6DD67212364DBA690D3DBFE836794C3E32BBF523083D45C2`.
- Earlier untimed inspection: `71B4825A6CE1AAA8FD78768221FA8474CC3492F341167970A0CFCAF15AB983AF`.

Immutable dev.27 packages and the44m28.60s Full/table observations still describe
`11a26e26`. This later pilot is separate evidence; no cumulative/parity claim or
package publication. Complete matrices remain at the next coherent boundary.

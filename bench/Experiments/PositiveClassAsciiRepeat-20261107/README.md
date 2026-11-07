# Positive ASCII hits in category repeats

The existing class-repeat loop now reuses its direct mask shortcut for positive,
unfolded category **hits**. A category mask can omit ASCII members supplied by a
table, so misses retain complete scalar decoding and membership dispatch. Plain
Set misses still stop the run. Folded, complemented, Unicode, RTL and
NonBacktracking behavior is preserved. No field, helper, opcode, executor or mode
is added. The original per-scalar charge, terminal control checks and resource
admission remain; skipping an already-proven classification changes no charge.

An ordinary Windows sampled-thread-time profile identified RunClassRepeat at
12.10% exclusive cost on the actual mail corpus. This supported a bounded change
to that live loop, not a rewind rewrite. The necessary class/delimiter window
already exists. Profile shares are not public throughput or VM measurements.

ONE [public comparison](cost-summary.json), **62.99001819s**, separate Default
processes in ABBA order,12workloads/240samples/min73.556ms. Core5DC994CC ->0D0EEFDB
on source38194460 plus the patch; PythonAFDD69EB and caller DLL/PDB bytes fixed.
The [declaration](comparison-declaration.json) and [graph identities](graph-identities.json)
retain scope/identities. Normal eight-CPU startup precedes CPU2 affinity, default
PGO/tiering,750ms/min32 warmup, independent75ms targets, five samples, one
calibration confirmation before sampling. Results are consumed after timing.
No repeat, timing confirmation, new reference lane, package or Full refresh.

| Public Count operation | Before us | After us | Change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| Actual6,839,410B mail |3238.02|3152.13|-2.65%|1544|
| Same finite/live |4498.24|4418.04|-1.78%|1656|
| Same deadline/live |5125.71|5050.72|-1.46%|1704|
| Dense ASCII class union |142.16|125.99|-11.37%|0|
| Dense Unicode class union |152.77|147.59|-3.39%|0|
| Long captured class union |111.14|52.67|-52.60%|64|
| Partial letter mask |124.41|127.19|+2.23%|0|
| Partial digit mask |102.51|105.24|+2.66%|0|
| Folded category control |129.21|133.19|+3.08%|0|
| Negative category control |131.46|133.73|+1.72%|0|
| Plain ASCII Set control |78.86|77.85|-1.28%|0|
| Unchanged Core literal |0.3266|0.3257|-0.26%|0|

**Promote for the captured/dense gains, retaining all neighboring costs.** Mail,
dense ASCII and long captured gains persist in both candidate processes with
disjoint overall sample ranges. Dense Unicode has better process medians but one
after sample overlaps/exceeds the original range. Finite/deadline mail and every
adverse row have overlapping ranges; do not claim stable gains or regressions for
those rows, or normalize using controls. Folded and plain-Set process medians move
in different directions. All allocations remain unchanged. The mail hash7B7F70C9
matches the existing catalog. This does not close its gap or produce a qualified
new .NET/CPython ratio, benchmark Status or universal speedup.

Focused Release checks pass on Windows/Linux, each .NET10/11: Core498,
PythonRe57,PCRE2=46, including13new Core cases. They cover all ASCII values through
partial masks, Unicode members/complements/folding, exact/variable/lazy bounds,
scalar supplementary units, slices, captures/retention, RTL/NonBacktracking,
malformed full tails, every positive work cutoff/exact success boundary,13B
parent loans and original token/absolute deadline inside the live repeat.
Existing specifications apply unchanged; the release notes record the shortcut.

Two preparation defects were corrected before any timing: CRLF patch transport
failed before application, and a baseline preflight caught an incorrect512-match
expectation for a negative-class control that could consume its @ delimiter.
The control now excludes @. Old caller/source/raw are preserved; neither was a
production defect or timed retry. Both preflights pass before the sole comparison.

Canonical evidence in `artifacts/experiments/positive-class-ascii-repeat-20261107/`:

- `evidence.tar.gz`: `D4AE2C0E5C3A4DA3696135967F21E49E3E6D4ED0417C63BBF5F125AA1A8E5ED1`,2919398B.
- `preflight-evidence.tar.gz`: `698F09319718CC25529150F72772BB9B30B29DC2F91A4232A9FE8BD87F6BCB39`,2903181B.

Both are verified off VM. Archives preserve both graphs/PDBs, exact patches,
worker/declarations, original/corrected preparation, checks, raw durations and
allocations. [Acceptance](acceptance.json) records closed sessions. All eight
Full48 DLL/PDB assets and all staged/published bytes remain unchanged. Remaining
performance/delivery work stays active; no publication is authorized.

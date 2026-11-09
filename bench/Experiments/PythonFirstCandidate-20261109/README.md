# Verify a known first Python candidate

Accepted against frozen dev60 Core/Python (`848dbb29`). A necessary ASCII range
already identifies a possible first start. When a delimiter excluded by that
range occurs within64 bytes, verify the candidate before the full delimiter
search/class rewind. Failure resumes the existing window search and scalar
advance. Long, absent and overlapping delimiters keep window discovery. Capture
slots, LastIndex, journals, required context and ordering remain entirely in the
same VM. One call-local Boolean, no retained metadata, cache, executor or mode.
Actual discovery charges can change; original admission and bounded authority
remain. No useful metering is removed.

An untimed probe of the **actual facade program** confirms no native candidate
regex, leading range97–122, direct Set class2, delimiter45 at instruction6 and
gap0. The three new independent public cases cover failed initial candidates,
later bounded starts and supplementary prefixes/floors over offset spans.
All5,012 PythonRe Release checks pass per target on Windows/Linux, .NET10/11,
including each finite work cutoff, shared loans/refunds, original cancellation/
deadline, empty/nullable, backreference/assertion, result and cursor-alias cases.

One **79.775s** quiet-VM .NET11 RC3 ABBA, under180s. One producer per ordinary
process, identical Core and caller,20 public lanes, five independently calibrated
samples per process/lane after warmup. All400 samples exceed27ms. Complete string/
tuple values are checked after timing; the existing range-cursor lane checks count
and final slot shape. No confirmation, package or catalog refresh for this trial.

| Public operation | Before (µs) | After (µs) | Change |
|---|---:|---:|---:|
| Many-capture strings |808.221|691.840|−14.4%|
| Many-capture strings, finite/live |1190.619|1059.292|−11.0%|
| Captured Count |474.778|405.713|−14.5%|
| Captured Count, finite/live |586.311|506.097|−13.7%|
| Range cursor |764.796|695.289|−9.1%|
| Range cursor, finite/live |1125.503|936.829|−16.8%|
| Repeated failed first candidate |293.036|328.465|+12.1%|
| Same rejection, finite/live |397.037|432.411|+8.9%|
| Sparse64KiB prefix |3.986|4.396|+10.3%|
| Unchanged Core Count |0.877|0.873|−0.5%|

Allocations are unchanged in all20 lanes. Main unlimited process medians improve
813.760/795.243→677.716/703.763µs, with disjoint sample ranges; finite processes
also both improve. The sparse pooled penalty overlaps process ranges3.61–4.41µs.
The failed-candidate penalty is clear and accepted for the main consumer benefit.
The unchanged one-capture row is bimodal446–1226µs; its pooled−29% is **not** a
demonstrated improvement. FullMatch/early-search/Core controls move−1.4/−1.0/−0.5%,
without normalization. [Summary](summary.json) retains all lanes and processes.

The original sparse control mistakenly joined a64KiB word directly to a rejecting
delimiter. Baseline preflight exceeded its10M finite work limit before timing;
this existing pathological behavior is not fixed by the trial. The failed fixture
and refusal are preserved. Correcting the control to include a separator passed
both preflights; they now also have30s caps. No timing was repeated. The initial
unbounded-first design passed tests, then gained the64-byte/excluded-delimiter
guard before any performance comparison; only the bounded design is promoted.

Raw protocol, incomplete preflight, patch, matching graphs, public results and
Linux checks are verified off VM at
`artifacts/experiments/python-first-candidate-20261109/evidence.tar.gz`, SHA-256
`5A7AE12B2D5A5BEC132606C8FD2F883ECE8A491AA5FC033A0B095E4E85982DB3`.
Windows checks/refusal logs are alongside it. No new native parity claim; dev60
packages and snapshots remain immutable. Source CI42698788274 is green on Windows/Linux for9bbf54de.

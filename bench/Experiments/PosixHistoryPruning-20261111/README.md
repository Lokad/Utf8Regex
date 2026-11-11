# POSIX completed-prefix pruning — accepted

Baseline6bd946b1; POSIX-only change. After a saved whole match reaches EOF,
completed occurrences of the first root child can prove a path inferior:
future events cannot repair its first differing boundary. Missing/unclosed
occurrences stay inconclusive; participation and redundant-empty rules remain.
Normal admitted trace buffers and rollback perform the pruning. No state merge,
new storage, executor or public mode. Original hierarchical tag order stays intact.

A bounded construction heuristic visits longer straight literal alternatives
first, using unused Fork.C in the private POSIX instruction stream. It changes
search order only, and leaves proven first-acceptance programs untouched. This
avoids visiting successively better complete partitions before pruning can help.

One **92.320s** .NET11 RC3 quiet-VM ABBA per policy,16 public rows/384 samples,
minimum40.531ms. One producer per ordinary process; explicit distinct POSIX
DLL/PDBs, identical Core/caller/control bytes,8CPU startup thenCPU2. At least2s/
1024 actual capture warmups, normal tiering/PGO,75ms calibration target. Complete
results and coordinates are consumed after timing. No confirmation or tuning run.

| Public operation | Validate before → after (µs) | AssumeValid before → after (µs) |
|---|---:|---:|
| Captured history4 |1.784 →0.754|1.739 →0.738|
| Captured history8 |11.953 →1.043|11.886 →1.061|
| Captured history12 |91.642 →1.379|91.117 →1.431|
| Captured history16 |708.367 →2.086|705.792 →2.103|
| History12, finite/live/funded |116.514 →1.890|117.292 →1.853|
| History12, deadline/live/funded |126.136 →2.293|125.415 →2.212|
| History12, cursor lifecycle |114.089 →1.980|114.519 →1.911|
| Nullable history8 |21.801 →1.459|21.665 →1.448|
| Nested history8 |12.035 →1.104|11.906 →1.076|

All those sample ranges are disjoint. Ungoverned captures retain48B/op. Funded
history/cursor allocations fall by1632B/op, modeled shared peak1969→977B including
the unchanged17B parent loan; final outstanding loans are zero. SKU96B/op stays.

Accepted costs: the class-bearing ascending-history control12.079→15.508µs
(+28.39%) and11.938→15.633µs(+30.95%) has disjoint ranges. Its literal-order
heuristic declines, so prefix comparisons add work without effective pruning.
Whole-only/Count add5.08/4.91% strict and7.74/7.89% trusted, also disjoint.
Construction+10.97/+22.39% has overlapping ranges;7320B stays7320B. SKU−1.33/+0.30%,
unchanged Core+0.61/+4.87% and predecoded.NET+0.49/−0.06% overlap; no normalization.
The large captured-operation gains justify these explicit local costs. Follow-up
is a broader useful traversal/dominance design, rather than more flag/storage
variants or repeated timing to hide the adverse rows.

Full Release POSIX **253 checks/target** pass Windows/Linux on.NET10/11, including
independent scalar-extent oracles, shorter-output equivalence, parent/whole
counterexamples, finite refusal/refunds, cursor aliases and original-token/deadline
injection inside both general comparison and pruning. Final EOF/non-EOF oracle
assertions additionally pass4/target on Linux; its DLL/PDB exactly equal measured
candidate FA0D35FF24EDA73072A47E9CE22C9F17855550801D83EEF72F07D6A3D791E1E3 /
0DDD7D5735A6733A0EC2DF166CAF06D8C0ED533CFEBC0B18118B5A9946D0D35A.
Foundation6bd946b1 CI42973344608 is green Windows/Linux.

Hash-verified offVM evidence, `artifacts/experiments/posix-history-pruning-20261111/`:

- Primary/source/pairs/Linux: `FAEE1F05136634D37510CCFA6A1C009758EEEC08EECE87B4583DE12203525C94`.
- Final patch/Windows/additional Linux checks: `F717A33ED4DFCC78487735689DB97F023CD024EAB6C9407FEF350635C4FD5EEE`.

Only EOF permits this dominance proof. Other higher-priority open enclosures,
non-literal alternatives and different endpoints can still require exponential
exploration. No general polynomial bound or native parity claim. Specifications
document the scope; numeric resource requirements may change, guarantees remain.
Immutable packages and generated benchmark tables stay unchanged.

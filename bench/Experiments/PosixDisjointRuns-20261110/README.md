# Unique disjoint ASCII run selection

Accepted after `1df925f7`. The SKU consumer's alpha-plus/digit-plus pattern kept
enumerating alternatives after its winning acceptance. Extend the existing
first-acceptance construction proof to flat chains of positive-minimum greedy
ASCII class runs whose sets are mutually disjoint. Each transition has one
possible boundary; greedy selection maximizes the final run, including any
anchor constraint. Tags outside the bodies have no competing occurrence history
at that endpoint. There is no new executor, retained field, buffer or mode.

Decline overlapping, nullable, negated/non-ASCII classes, alternation, compound
repeat bodies and repeated tags. The existing VM still executes all instructions,
captures and anchors before acceptance. The construction traversal and fixed
16-byte membership inspections are work-charged. Strict whole-input validation,
scalar floors, retained values/cursor aliases, original interruption, admission,
depth/working/result controls and synchronous refunds stay. Proven acceptance
can reduce numerical work requirements; exact old-path cutoffs are not promised.
The [POSIX contract](../../../docs/SPEC-POSIX.md#interruption-and-admission) records
the restricted proof. General history competition remains nonlinear.

One **94.950s** separate-process ABBA on the quiet .NET11 RC3 VM, within180s:
24 public lanes,480 samples, minimum56.606ms. Reuse the existing POSIX worker,
adding only actual SKU finite/funded/deadline controls. Explicit DLL/PDB staging,
distinct loaded producer hashes, identical Core/caller and false/true actual
facade proof preflights precede timing. Full captures/checksums and zero loans
are checked outside timing. No confirmation or package/catalog/Full repeat;
immutable dev62 packages and benchmark snapshots stay unchanged.

| Public operation | Before (µs) | After (µs) | Change |
|---|---:|---:|---:|
| SKU,4096 calls |5110.463|4106.655|−19.6%|
| SKU, finite |6536.433|5345.591|−18.2%|
| SKU, funded |7169.500|5968.331|−16.8%|
| SKU, deadline |7811.220|6473.967|−17.1%|
| Empty captures control |0.718|0.744|+3.6%|
| Unicode-longest control |1.131|1.177|+4.1%|
| Nullable captures control |1.021|1.032|+1.1%|
| History12 control |97.604|97.113|−0.5%|
| Unchanged Core Count |0.834|0.838|+0.4%|

SKU process medians5064.553/5116.013→4106.011/4107.300µs have disjoint ranges;
the controlled SKU gains also have disjoint ranges. All allocation counts are
unchanged, including393216B per unlimited SKU batch; funded SKU peak stays1008B.
Retain adverse control movement without attribution or normalization. SKU is an
unranked consumer, not a new POSIX-specific headline or refreshed native ratio.
The native comparator and SKU compilation cost were not timed. Existing empty/
alternative construction controls move+0.6/+0.4%, with unchanged allocations.
[Summary](summary.json) preserves every lane, process and range.

All204 POSIX Release checks pass per target/platform, Windows/Linux .NET10/11.
Six new cases establish actual eligibility/declines, independently expected byte
captures, offset spans/supplementary prefixes, bounded maxima, nested wrappers,
Newline anchors and public Count/Boolean/token calls. Existing history/empty/
alias/refusal/deadline/refund cases pass. Initially203 passed before timing; the
additional public Newline case then passed in the full affected suite without a
production change or timing repeat. An initial test compile used a nonexistent
coordinate property; its failed log is preserved separately from accepted checks.

Before POSIX DLL `2B9B32C70C9F4579833CD8B43CD30E467F0CCD0BA56F05A46B05DD0684BD48F9`;
after `9AA98F0D161A6DCB9E6B02BAF6169CE992FA7E5EC5EC1D89A8995821AAAD0B7D`.
Common Core `772F0B099ACB0D56045E693DFDAB6622CEBA971D831AB95E81D57744FADB6D0E`.
Raw protocol, patch, graphs, preflights, checks and samples are verified off VM at
`artifacts/experiments/posix-disjoint-runs-20261110/evidence.tar.gz`, SHA-256
`9F96D2CA310EFE2F06AEE4CFFC11124F479701AB6AC6C03BA5D0342173AA1958`.
Final204-check logs/fixture are alongside it. Source CI42734417139 passes
Windows/Linux for0f0a513d597c244c9f0fc883b8e64d49b2afa045.

# Structural operations request only needed subject facts

Accepted source `3dc2e65a4ca822fa4b046c316de70032e13114e5`, compared with
`9df5eb7cde15fd400b67560280c6a98842b61012`. IsMatch and Count no longer construct
discarded whole-subject UTF-16 descriptions. Scalar-aware window Count needs only
the selected validity authority; other structural kernels need ASCII eligibility.
Match and mapped enumeration retain their complete descriptions. The shared
admission helper loses its unused input parameter. No new state, cache, engine,
public API, algorithm, allocation mechanism or unchecked read.

One Linux .NET11 RC3 **127.721s** separate-process ABBA per policy, under180s.
Same caller DLL/PDB and inputs; distinct Core DLL/PDB pairs; one producer per
process,8 processors visible before CPU2 affinity, no runtime overrides.
12 public lanes/policy,9 samples/process,864 samples; minimum38.915ms. All
results/refunds pass. Actual public route,3445 candidates/verifications,408-byte
search descriptor and window primitive IL hash are unchanged. No codegen campaign,
Full/package repeat or confirmation.

| Public Count | Policy | Before µs | After µs | Change |
|---|---|---:|---:|---:|
| river/compiled | Validate | 3321.387 | 3148.677 | -5.20% |
| river/ordinary | Validate | 3319.774 | 3138.667 | -5.46% |
| river/compiled | AssumeValid | 3901.060 | 2613.786 | -33.00% |
| river/ordinary | AssumeValid | 3876.733 | 2620.567 | -32.40% |
| holmes/compiled | Validate | 116.076 | 121.523 | +4.69% |
| holmes/compiled | AssumeValid | 138.566 | 104.840 | -24.34% |

Those ranges are disjoint. All allocations and modeled workspace peaks are
unchanged. Finite/funded/deadline river movements (-0.3% to -1.3%) overlap.
ASCII river is an affected variant, improving2.5%/17.8%; it is not an unchanged
kernel control. Unicode-overlap and separator observations retain their variation.

Retain default Holmes **+4.69% (+5.447µs)**: the simpler preparation saves about
173µs/default river call and1.287ms/trusted river call. No blanket regression veto
applies. The unchanged-definition strict literal-IsMatch control moves-4.24% in
Validate process slots and+4.00% in AssumeValid slots, with disjoint ranges; do
not normalize either observation away. Literal Count overlaps its baseline.
The large river benefit exceeds these small-control movements. No native/.NET
parity verdict is composed from this change and the earlier baseline.

Focused Release checks813 per .NET10/11 target pass: default malformed-suffix
rejection, both-policy byte and UTF-16 results, enumeration, native structural
eligibility, overlaps, finite controls, cancellation, deadlines and cursor loans.
Published/staged dev66 bytes and generated tables remain unchanged.

Raw samples, complete process ranges/medians, allocation/workspace costs, source
patch, identities and exact driver are retained in
`artifacts/experiments/structural-input-facts-20261110/evidence.tar.gz`, SHA256
`5DD78D3B4DC84AD157E01A121496B69D4E3DAFA2FDFF3D2187413AC262EA3FE3`.
The complete derived summary is beside the archive. Source/package qualification
and external parity remain separate; the roadmap continues with Python metadata,
preparation and the remaining shared matching/output costs.

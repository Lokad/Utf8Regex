# Bounded current POSIX history assessment

Source `e5e51d265589788c92e067ddcf6a9984d2e07a50`, clean matching Core/Posix/
benchmark source checks. One **15.092s** .NET11 RC3 run using the existing
`--measure-posix-case CASE 9 Validate` command, capped120s,25s per command.
Three existing history rows and the unambiguous SKU control; no code, catalog,
snapshot, README-table or package change. Default runtime settings,8 CPUs visible
before in-process CPU2 affinity. Exact loaded graph/DLL/PDB identities archived.

| Captured `(a\|aa)*` input bytes | Public Match µs | Sample range µs | Managed B/op | Modeled peak working B |
|---|---:|---:|---:|---:|
| 4 | 1.663 | 1.654–1.680 | 48 | 960 |
| 8 | 11.846 | 11.576–12.041 | 48 | 1952 |
| 12 | 88.689 | 88.225–89.569 | 48 | 1952 |

All9 samples per lane exceed20ms; history minima76.230/73.170/73.933ms.
Each+4 input bytes multiplies observed cost by7.12 and7.49. These timings qualify
the bounded absolute observations; they do not fit a general complexity class.
Native ordered captures differ, so all three rows retain their semantic exclusion
and no native ratio. Captures, byte/UTF16 coordinates, funded preflight and refunds
are checked by the existing driver.

Source explains the cause independently of the three points: PosixWorkspace
performs depth-first path enumeration; `(a|aa)*` admits Fibonacci-many complete
partitions, and accepting paths are compared using hierarchical occurrence
histories. No frontier/dominance merge prunes equivalent suffix work. Current
uniform-literal/repeated-capture and disjoint-run proofs correctly decline this
ambiguous grammar. Earlier whole-match/trace/origin shortcuts are already present.
Thus workspace/pool changes can reduce constants; they cannot remove this path
explosion. Finite work/depth/working limits, cancellation and deadlines bound it.

A shared history optimization needs an explicit dominance proof. Final capture
slots or(program counter,byte position) alone cannot merge states: repeated and
nested/nullable occurrences affect priority. Candidate future work is to compare
closed hierarchical history prefixes once a longest possible endpoint is known,
or to develop a tagged-state frontier with admitted storage. Neither candidate
is implemented or benchmarked here; do not claim generic polynomial performance
or weaken capture priority. Keep an independent small exhaustive occurrence
oracle/counterexamples before accepting any such algorithmic change.

The unchanged SKU control remains useful separately from history selection:
4096 public calls take2632.282µs,0.64265µs/call; equal-result native681.803µs,
0.16646µs/call. This is a contextual control, not a source before/after speedup.
Unlimited allocates about96B/call. Existing finite/live, funded/live and deadline/
live batches take3560.573/4014.542/4616.347µs, about856/856/904B/call; modeled
funded peak784B. Preserve the actual useful resource cost; no native policy match
is claimed. Shared accounts return to zero. Raw row includes confidence/order data.

Live allocation audit: PosixWorkspace always allocates a fresh exact int[] for
repeat/open/last state after admission; Release discards it. SKU's16-int state
fits the existing bounded exact-state pool, whereas4-int history state is outside
its retention buckets. A bounded reuse pilot can preserve exact capacities and
all ledger admission, reset the full borrowed span before use, and use the same
retention eligibility/global caps as existing state buffers. Do not widen pool
buckets, add another owner/cache or assume reduced allocation means faster calls.
Measure public SKU/governed/cursor and unchanged Core/PCRE2 consumers before
promotion; preserve multiple simultaneous same-capacity vectors and cursor aliases.

Raw frozen graph, protocol, commands and all observations:
`artifacts/experiments/posix-history-current-20261110/evidence.tar.gz`, SHA256
`A66DD9C5C8E75367AA7C1214E0D8FB8750D264839389D709486E1698ACBE52FD`.
Derived rows are beside the archive. Copied offVM/hash verified. All workers closed;
no confirmation, broad benchmark campaign, source test rerun or package matrix.

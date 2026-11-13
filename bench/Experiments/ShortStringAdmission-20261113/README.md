# Bounded capture-string decoding with conservative admission

Accepted producer `b38f14fa`, compared with `ae614dc0` on .NET11 GA. Python's
string-only path no longer counts UTF-16 units before decoding an unknown valid
range of at most4096 UTF-8 bytes. The existing result ledger preadmits the string
capacity for byteLength UTF-16 units, then calls checked Encoding.UTF8.GetString
between original checkpoints. Known lengths and larger bounded sizing/decoding
retain their paths. No new mode, cache, owner or ledger is introduced.

This intentionally revises modeled capacity: short Unicode strings with no known
length can consume more working/result budget and be denied earlier. Admission
still precedes allocation, result traffic stays cumulative and working loans
refund synchronously. Returned text, captures, offsets and allocation counts are
unchanged. This differs from the closed decoder trials that retained the caller's
exact sizing pass; no ASCII-only decoder or extra eligibility scan is added.

One bounded ordinary-process public FindAllToStrings A/B covered the existing
one/many capture cases, mixed-width captures and a finite/live-token tuple case.
Valid unchanged UTF-8, equal capture-string output and positive2048/2048/1024/2048
match counts were asserted. Native capture strings were not inferred from Count.
The unchanged predecoded .NET string materializer is a drift control, not a
Python compatibility/parity oracle; public Python Count is a separate unchanged
operation. All values/checksums were consumed after timing, retaining outputs.

| Public string operation | Before µs | After µs | After/before, lower better | B/op before/after |
|---|---:|---:|---:|---:|
| Unicode capture tuples | 479.956 | 477.533 | 0.995 | 115536 /115536 |
| One ASCII capture | 295.237 | 280.955 | 0.952 | 94344 /94344 |
| Many ASCII captures | 522.399 | 480.415 | 0.920 | 242008 /242008 |
| Many, finite/live token | 837.094 | 716.782 | 0.856 | 246208 /246208 |

Nine samples per lane/case exceeded40ms. Each producer process was capped at120s;
retained graphs were bounded to64MiB using output shape/lengths, with a separate
bounded active worker. Transient GC traffic was measured, not reserved as live
retention. Producer DLL/PDB/source documents and unchanged caller/control identities
matched; one producer was loaded per ordinary process.

The unchanged unlimited Count medians moved+0.44/+0.76/−0.45%; finite Count+7.11%.
Allocation-heavy .NET controls moved−13.71/+36.01/+39.42/+5.34%, and allowed CPUs
differed. Retain the public observations and uncertainty without normalization,
an exact causal percentage or a new CPython ratio. Primary ASCII/finite sample
ranges support keeping the change; Unicode is effectively unchanged. No
confirmation or further tuning run was needed for this decision.

Both-runtime checks pass: complete PythonRe5098, focused Core output/admission1253
and PCRE2 replacement/output1154 cases pertarget. Production ledger tests cover
exact known lengths, unknown4095/4096/4097-byte bounds, result/working denials before
decode, cumulative traffic, parent-loan refunds and retained text. Existing Unicode
scalar/block-tail, cancellation/deadline, ownership and result suites remain.
PCRE2's own decoder still calls the unchanged bounded buffer helper; no PCRE2
throughput claim follows from this Python comparison.

Specifications, migration and the Python profile README describe the conservative
charge. Raw caller/samples/source identities and checks:
`artifacts/experiments/python-short-string-admission-20261113/`.
The immutable [dev81 checkpoint](../DeliveryDev81-20261113/README.md) is unchanged;
these source changes need the next coherent package/Full boundary, not repeated
per-change matrices. Published/staged package bytes remain intact.

# Stop ASCII classification after a negative proof

Accepted source `544e0169`, compared with frozen `15b248dc` on the .NET11 GA VM.
Trusted or already-checked governed input stops ASCII classification at its first
non-ASCII block. The accumulated Boolean and remaining useless scans are removed.
Core's default whole-input validation still precedes this eligibility query;
actual block metering, original cancellation/deadlines and storage admission stay.
No mode, cache, ownership mechanism, SIMD or public API change.

One public Python `Search` comparison used `([a-zé])`, a1MiB existing valid subject,
finite work/working/result policies and a live original token. Both producers
materialized the same byte/UTF16 whole range. The unchanged predecoded BCL Match
lane projected that same range as a drift control, not a Python/.NET parity claim.

| Governed public Search | Before | After | Allocations before/after |
|---|---:|---:|---:|
| Unicode member at byte0 | 14.213 µs | 0.316 µs | 176 /176 B |
| Entirely ASCII subject | 14.179 µs | 14.534 µs | 176 /176 B |

Keep the roughly2.5% all-ASCII cost for eliminating the unrelated suffix scan.
Different allowed CPUs and control movements of−2.1%/−3.7% limit exact causal
percentages. Nine samples perlane exceeded20ms; no confirmation was needed.
Ordinary separate processes loaded explicitly staged DLL/PDB pairs; their source
document checksums, unchanged caller and unchanged control assembly were checked.
Each process was capped at120s; owned sample storage at64MiB. Inputs were prepared
before timing and complete result tuples were checked afterward.

The new public regression test failed against the old producer at work65 under a
64-step limit, then passed with identical scalar/byte/UTF16 results and original
pre-cancellation. Focused Core/PythonRe/PCRE2 policy, subject and governed-class
checks pass on both runtimes, including Core strict malformed-tail rejection.
Numerical work requirements can decrease because actual work is avoided.

Raw samples, caller source, hashes and identities are under
`artifacts/experiments/ascii-classification-20261113/`. No package or Full matrix
was repeated. Immutable dev80 remains the accepted contract-delivery candidate;
this source improvement joins the next coherent delivery batch.

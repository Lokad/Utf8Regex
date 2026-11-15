# POSIX invariant case-insensitive selection

Accepted: immutable `PosixCompileOptions.IgnoreCase` reuses Core's invariant
Unicode 16.0 simple-lowercase relation for literals and brackets. Both scalar
execution and closed capture selection preserve leftmost-longest selection,
hierarchical capture priority and original byte coordinates. Folded literal
chart bounds admit up to four original bytes per scalar. Existing case-sensitive
greedy/disjointness proofs decline folded atoms; the unique straight-line proof
remains sound. No normalized input, alternative matcher, framework or new owner.

The relation is single-scalar, with invariant I/i and independent dotted/dotless
I. It is not full Unicode folding or locale collation; see the
[contract](../../../docs/SPEC-POSIX.md#input-and-syntax). Pattern validity,
already-valid borrowed subject text, synchronous interruption, major admission,
capture spans and cursor/result lifetimes retain their contracts. Existing default
matching stays case-sensitive. The new option is not in the immutable 0.5.0 feed.

Windows and SAME-byte Linux pass all465 profile Release cases per .NET10/11,
including API inventory, dependency isolation and checked-member inventory. The
shared independent corpus now includes all three handoff examples, width-changing
Kelvin/Angstrom partners, supplementary pairs, classes/ranges/negation, empty vs
absent captures, last-iteration reset, newline/floors and scalar cursor progression.
Existing executors check every caller capture-span size. Focused extensions cover
ambient-culture independence, alias retention, refused working admission and
original cancellation/deadlines inside folded execution and closed capture
membership/projection. No new oracle or test execution framework.

ONE78.262s ordinary-process ABBA,180s cap,0 confirmations. Normal eight-CPU
runtime startup then CPU2; one producer/process; five150ms-target samples per
process/row with20ms floor. Compile/input preparation excluded. Reused caller
capture storage; complete returned coordinate structs consumed after timed
batches. Both subject lanes require valid unchanged UTF8. Finite/live adds a
one-million-step policy, depth1024, working1MiB, result1 byte, original token,
five-second deadline and caller working allowance; result limits do not cap
caller-owned capture destinations.

| Existing public path | Before µs | After µs | Change | Sample ranges |
|---|---:|---:|---:|---|
| Ambiguous captures | 1.102 | 1.096 | -0.59% | Overlap |
| Nullable capture | 0.458 | 0.459 | +0.22% | Overlap |
| Unicode/nonzero-floor captures | 1.134 | 1.124 | -0.89% | Overlap |
| SKU captures | 0.230 | 0.224 | -2.70% | Overlap |
| Longest Count | 0.375 | 0.367 | -2.11% | Overlap |
| Longest capture cursor | 1.852 | 1.818 | -1.85% | Separated |
| Capture after4096 failed starts | 125.252 | 130.943 | **+4.54%** | Separated |
| Identical Core Count control | 24.471 | 24.348 | -0.50% | Overlap |
| Identical .NET capture control | 0.164 | 0.170 | +3.49% | Overlap |

All existing allocation medians are unchanged: zero on SKU/nullable/Count,
680B on general captured selection,1048B on the capture cursor. Retain the
late-search regression and all adverse/control observations. These small controls
do not establish broad speedups or refresh canonical parity tables.

The old provider rejects option2. New capability costs have **no before
implementation**, so they are not before/after gains:

| New folded public operation | µs | Observed sample range µs | B/op |
|---|---:|---:|---:|
| `(a\|aa)(a*)` captures on `AAA` | 1.393 | 1.380–1.407 | 680 |
| `(k\|kk)(k*)` captures on `KKk` | 1.407 | 1.393–1.416 | 680 |
| Same, finite/live | 1.860 | 1.832–1.905 | 1744 |
| Longest Count on `AAAA` | 0.391 | 0.389–0.394 | 0 |
| Longest capture cursor on `KKK` | 1.351 | 1.338–1.374 | 1048 |
| Unambiguous `(k+)([0-9]+)` captures on `KK007` | 3.809 | 3.768–3.919 | 1920 |
| Private native comparator on that equal-result input | 0.337 | 0.335–0.340 | 0 managed |

The private PCRE.NET1.5.0/PCRE2 10.47 ordered NFA uses UTF+NO_UTF_CHECK,
caseless/dot-all/true-end-dollar options, a reusable match buffer and byte/UTF16
coordinate projection. Its captures agree on the unambiguous input only. The
ambiguous `AAA` capture row is explicitly excluded: native group1 is `A`, POSIX
group1 is `AA`. Different folding relations and ordered selection prevent a
general POSIX parity claim. Native workspace is excluded from CLR allocations.
The~3.47us unambiguous capture gap and1920B general-selection cost are remaining
work, not a reason to weaken selection or reopen the checked-access campaign.

Baseline source4ffc82e3; only the POSIX producer changes. Actual DLL SHA256s:

- Before POSIX `0DAC27E057D741E3E89FDC8485A068AFAC61CDAEF047F8C6666436354F4898F0`.
- After POSIX `890E4BA9520F5ED3572C5D29BD4FEE67CD9185D584DB00579B278972A362A1D0`.
- Common Core `A057EAF55A3FFE8A7013AD8EC81F0B894C4F037B0BC44DF98D9E9E6ADC0E1D2D`.

Exact loaded DLL/PDB/compiler-document checks and unchanged control identities
pass; no VM producer rebuild. A local JSON serialization defect was corrected
in untimed preflight before this single comparison; no timing was repeated.
Raw: `artifacts/experiments/posix-ignore-case-20261115/evidence.tar.gz`, SHA256
`A447D816589378A13448A452AB1BC2E5B733D1FC5E28655B714A01E3D3FA0FD8`.
Original worker/collector handles are terminal0; raw was verified offVM before
keeper release. No package or Full refresh for this bounded capability change.

# One policy-aware validity authority for value Match

Accepted: remove GetWellFormedOnlyValidation and call the existing CheckSubject
authority directly. The helper chose a separate whole-input BCL Utf8.IsValid
gate after an ASCII probe; it always returned default metadata. Count's other
helper consumer now passes that same constant directly. Twenty net production
lines removed, including an unused import. No matcher/projection algorithm,
state, owner, selector, public API, resource model or unchecked access is added.
Strict whole-tail rejection and AssumeValid bypass retain their contracts.

Windows and SAME-byte Linux .NET10/11 each pass focused Core1014, PythonRe892,
PCRE2 396 and POSIX50 Release cases. Core also passes511 focused value/policy/
literal/work cases per Windows target. Borrowed ranges, ASCII/BMP/astral text,
misses, RTL/NonBacktracking, policy authority, malformed tails, original tokens/
deadlines, finite refusal and retained results remain covered. Existing adapter
hosts are reused with the changed Core pair; only Core/its test host build.
Scoped no-intrinsics public preflight passes.

One102.179s public ordinary-process ABBA,180s cap,0 confirmations: thirteen
operations, normal8CPU startup then CPU2, five150ms-target samples per process/
row, >=20ms floor. Valid unchanged UTF8; complete byte/UTF16 Match values consumed
after timing. Strict whole-input validation is timed unless labelled trusted.
Finite control retains original token, deadline and work/depth/working/result caps.

| Public operation | Before µs | After µs | Change | Sample ranges |
|---|---:|---:|---:|---|
| Actual Russian corpus Match | 2071.335 | 94.434 | -95.44% | separated |
| Early literal, Unicode then ASCII tail | 39.063 | 19.674 | -49.64% | separated |
| Early literal, BMP tail | 176.132 | 61.453 | -65.11% | separated |
| Early literal, astral tail | 1076.287 | 62.298 | -94.21% | separated |
| Late literal, BMP prefix | 407.685 | 282.784 | -30.64% | separated |
| Missing literal, BMP subject | 198.297 | 79.786 | -59.76% | separated |
| Trusted early BMP control | 0.222 | 0.224 | +0.67% | overlapping |
| Finite early BMP control | 110.138 | 89.593 | -18.65% | separated |
| RTL control | 853.529 | 1085.821 | **+27.22%** | overlapping |
| Short ASCII control | 0.154 | 0.156 | +0.92% | overlapping |
| Count control | 110.389 | 88.727 | -19.62% | separated |
| IsMatch control | 83.135 | 61.701 | -25.78% | separated |
| Unchanged .NET Match control | 0.078 | 0.080 | +3.19% | overlapping |

Keep the smaller implementation and clear affected-Match improvements. Retain
the adverse RTL observation and material movement on unchanged Core control
routes. Do not attribute their gains to this patch, normalize results or claim
an exact universal effect size. Allocations agree:0B ordinary,160B finite and
208B.NET. This does not update canonical Count parity, profile tables or the
remaining email/search gaps. No confirmation or further tuning run.

The first local preflight exposed an old private observer reading byte getters
on NoMatch, which correctly throws. Fix only that shared observer before any
VM transfer/timing; both producer pairs remained unchanged. No failed library
behavior or timed samples were discarded.

Baseline is2bafb1ac's measured producer; after is that source plus the patch.
DLL/PDB identities, all343 compiler documents and checked managed-member
inventories verify; caller/.NET identities are identical. No VM producer build.
SHA256s:

- Before Core `0EA0AC17E582CF9110C55E6EDF36744F53FBFCB9052055F952A28214663DF4E2`.
- After Core `6B0A0685AABBDCE97CA466F539DE2D400B8B9ABECE6D566C904E6294B9D0BD92`.
- Raw `artifacts/experiments/value-match-validity-20261116/evidence.tar.gz`:
  `9FD7F277FF34F2B50F2812AA4971DE784EBDA9E43AF14305D396A42AB1DD8D2A`.
- Validation archive: `9367984B9A017CDE29246F145846F66AB7849EE8764E1086C76705916CF824F3`.

Original worker/collector handles terminal0; raw verified offVM before keeper
release. Owned VM task retired only after accepted archive hash and zero live
process/cwd/FD/map references. Qualified dev.99 assets rehash unchanged;660 feed
files remain. No package/Full qualification, publication or downstream adoption.

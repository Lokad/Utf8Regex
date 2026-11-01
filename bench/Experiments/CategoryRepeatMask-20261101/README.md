# Rejected category-repeat ASCII mask shortcut

**Rejected and fully reverted.** Baseline `42355523`; no execution, tests,
API/resource contract or snapshot changes from this pilot remain.

The existing repeat helper only shortcuts positive plain Set masks. The pilot
also accepted positive unfolded CategorySet mask hits, falling back to Rune/table
membership on misses. Complete class-program predicates were excluded because
they own their semantics and work charges. No new field, matcher or allocation.

The actual Python date [inventory](before-inventory.txt) has 17 instructions,
three positive digit category masks and an already-working ASCII head range.
[Before/after](inventory-comparison.txt) preserves all instructions/masks, the
14 candidate attempts' exact work and loan refunds, and public3568B allocation.

One [public A/B](comparison.json), **84.071 seconds**, quiet Ubuntu VM/.NET11RC3,
CPU2. Ten equal-result IsMatch/Count cases, four producer/predecoded/decode lanes,
warm1.5s/min64, independent60ms calibration, five alternating samples; minimum
**58.844ms**. Result batches consumed after timing. [Driver](driver.cs.txt),
[summary](summary.json), [identities](identities.json). The .NET lanes cover only
overlapping BMP/ASCII semantics; they do not update the official CPython Status.

| Public operation | Change | Both lanes B/op |
| --- | ---: | ---: |
| Actual Python date miss | -1.20%, negligible | 3568 |
| Finite/live date miss | +0.24% | 3688 |
| Deadline/live date miss | +2.43% | 3736 |
| ASCII date hit | -0.18% | 3568 |
| Unicode date hit | +3.84% | 3568 |
| Dense ASCII digit Count | -4.58% | 1680 |
| **Dense Unicode digit Count** | **+13.46%** | **1680** |
| **Folded-set control** | **+122.95%** | **1680** |
| **Class-program control** | **+23.45%** | **1680** |
| Unchanged metadata Count control | -0.13% | 0 |

Folded-set paired penalties118.67–124.15%, class-program21.95–23.82% and Unicode
13.27–14.06% are clear public regressions. Their causes are unproven; do not
attribute them to hot bounds checks or inlining without evidence. No confirmation,
selected refresh or package/platform matrix is justified. Preserve the qualified
Python date4.793x gap; this mask extension does not address it meaningfully.

[Release checks](verification.txt) passed on both runtimes: focused Core462,
complete PythonRe4390 and focused PCRE277/runtime. The rejected tests covered
predicate-only ASCII hits through intentionally incomplete program-owned masks,
BMP/supplementary digits, captures, malformed tails, every work cutoff/refund and
class-program authority even when its ignored wrapper mask claims extra bytes.

Immutable `artifacts/experiments/category-repeat-mask-20261101/` archives:

- `evidence.tar.gz`: exact before/after Core/Python DLL/PDBs, patch, driver/tests
  and public samples, SHA256
  `21FE600A81A4C6B030F477D6FCBE0C68B7E0585C3778485D9F142F4E75C2FA40`.
- `correctness-source.zip`: tests, rejected source, inventories and identities,
  SHA256 `B06D9C31EB64FC3D2CB42D94037433FFB5BEA393E6F8CF3B2DBED657B1DCAF50`.

The fixed final cumulative baseline remains `a40bda03`. Published/staged bytes
are untouched. Continue a different shared cause rather than tune this loop.

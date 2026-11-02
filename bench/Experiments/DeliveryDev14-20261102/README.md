# Qualified 0.5.0-dev.14 delivery candidate

Core, PythonRe and PCRE2 share **0.5.0-dev.14**, exact Core dependencies and
.NET 10/11 assets. Packages and symbols are staged under `artifacts/nuget/`;
immutable records are under `artifacts/package-candidates/0.5.0-dev.14/`.
**Not published.** Earlier staged and published bytes remain unchanged.

| Gate | Result |
| --- | --- |
| Windows packages | 468.335s, two producer graph builds and one pack per profile; both-runtime detached checks pass |
| Linux packages | Exact Windows bytes, both-runtime checks pass, no repack |
| Original consumers | Copied Awk/Lython and native/funded API probes pass on both targets/platforms; final root loans zero |
| Source/restore identity | 464 original Windows compiler documents including generated inputs; six assets, 24 Linux and 12 original Windows restore checks |
| Fixed-original operating costs | All 172 public entries pass against a40bda03; maximum adverse median 9.83%, no confirmation or allocation increases |
| Complete VM refresh | **2568.410s / 42m48.41s**, Full/Complete, all 83 exact expected steps passed, exit zero |
| Imported data | All seven snapshots/Markdown files match final VM hashes; existing .NET 10/11 snapshot guards pass with matching binaries |

The package/cost producer is `895f98a06c4b6b28abb79ab3231f0b277b848fe8`.
The complete refresh uses `16839b38bce3eafce4e20785cbcfe02d57a1dcee`, which adds
only one test collection attribute. Its `src` and `bench` Git trees are identical
to the package freeze; revision metadata differs. [Source equivalence](test-fix-source-equivalence.json)
records both revisions. Package bytes were not regenerated after the test fix or
table import. Source CI is green for both revisions; data/receipt publication
has its own CI result.

The first Full attempt failed during correctness after 180.905s, before any
measurement: one PCRE2 warm allocation assertion observed 32,792 bytes, consistent
with an 8,192-int pool miss. Its fixture missed the existing serialized allocation
collection. The one-line fix preserves the zero-allocation assertion, warmup and
all production code. Focused checks pass on Windows/Linux, and the fresh Full
passes the complete suites. The [failed receipt](failed-full-refresh.json) and
exact source/binaries/logs remain preserved. Its clock is not combined with or
relabeled as the completed Full clock.

This batch retains the accepted boundary-only retry simplification and exact
measured immutable-policy cache. The cache's initial 3.84% gain was rejected
under an unnecessary agent-imposed 5% floor; that decision remains in its original
receipt. Restoring the exact patch used no variant or new parent measurement.
The unchanged [172-operation cumulative gate](cumulative-acceptance.json) now
passes: native Search is +6.05% versus original, folded FullMatch +9.83% and the
17-byte control +9.24%. The separate internal-guard deletion failed its useful-gain
criterion and remains reverted. No fixed baseline or 10% allowance was relaxed.

The full refresh includes one build, both-runtime correctness/disabled-dynamic
checks, complete .NET 11 catalogs, paired comparisons, lifecycle/scaling, finite
resource costs and final verifiers. It meets the 60-minute target. Package,
original-consumer and cumulative-cost clocks are separate scopes.

[Current rankings](ratio-summary.json) preserve uncertainty and exclusions.
Core has 110 wins, one tie candidate, 61 gaps and six unqualified rows. PythonRe
has 30 ManagedFaster, 43 CpythonFaster, 13 inconclusive and four unqualified rows.
PCRE2 has 77 ManagedFaster, one equivalent, 11 NativeFaster, 12 inconclusive and
25 excluded rows. Unqualified Core rows have short measurement lanes; PythonRe's
four rows retain material harness-floor reasons. They were not resampled to
improve their status. Catalog changes include comparator movement and do not
establish causal parent speedups or universal equivalence.

Largest qualified gaps remain Core keyword 8.53x compiled, imports 7.06x compiled
and Chinese literal 7.00x ordinary; PythonRe early literal 5.56x, date miss 4.02x
and empty-progression FindAll 3.58x; native PCRE2 date hit/miss 9.61x/9.14x and
subroutine 4.13x. Preserve closed experiments and separate larger designs,
optional PCRE2 scalar projection and later POSIX milestones.

[Verification and archive hashes](verification.json), [full refresh](full-refresh.json),
[Windows qualification](windows-qualification.json),
[Windows consumers](windows-consumers.json), [Windows restore audit](windows-restore-audit.json),
[Linux packages](linux-packages.json), [Linux consumers](linux-consumers.json) and
[Linux identity audit](linux-identity-audit.json) retain the evidence. Raw archives
are under `artifacts/experiments/delivery-dev14-20261102/`. Windows archive creation
hit an empty-directory error in native tar; the successful ZIP inventories and
original failed scratch outputs are preserved. No protected cleanup target was
touched and no planning-history file was created.

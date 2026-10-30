# Native PCRE2 pattern compilation

Public operating-cost checkpoint for producer `2f70e6db` against fixed
`d203d93f`, before PCRE2 lexical/parser migration. The chosen patterns are
correctness-equivalent across both versions. Escaped emoji class members,
fixed separately by this producer, are excluded from timing.

One capped 120-second quiet-VM run completed in **90.807s**: nine cases,
34 lanes, Release .NET 11 RC3, processor affinity mask 4. Each lane warmed
for two seconds, calibrated independently toward 80 ms and retained three
alternating samples. All actual sample durations exceed 20 ms. Public results,
funding balances and unchanged production IsMatch/Count selectors were checked
outside timing. No confirmation or further campaign was run.

Count lanes reuse expressions. Compile lanes include public eager preparation;
funded compilation also closes the owner. Source encoding is prepared outside
both versions. This producer comparison does not refresh comparator catalogs,
claim a new benchmark Status, qualify packages or replace the cumulative release
comparison against the frozen release baseline.

| Public operation | Before text / native (us) | After text / native (us) | Allocation before → after (B/op) |
| --- | ---: | ---: | ---: |
| core-literal-count-control | 0.558 | 0.568 | 0 → 0 |
| pcre-literal-compilation | 1.222 / 1.383 | 1.453 / 1.615 | 1840 / 1880 → 1840 / 1880 |
| pcre-capture-compilation | 126.039 / 126.383 | 132.213 / 139.701 | 65704 / 65792 → 65608 / 65424 |
| pcre-property-compilation | 61.485 / 61.566 | 63.672 / 66.436 | 34696 / 34744 → 34560 / 34496 |
| pcre-literal-count | 9.640 / 9.863 | 9.923 / 9.959 | 0 / 0 → 0 / 0 |
| pcre-guided-count | 78.004 / 78.315 | 79.159 / 77.911 | 3552 / 3552 → 3552 / 3552 |
| pcre-unicode-count | 104.171 / 104.590 | 103.589 / 103.704 | 4064 / 4064 → 4064 / 4064 |
| pcre-finite-count | 164.378 / 165.008 | 166.536 / 166.391 | 3992 / 3992 → 3992 / 3992 |
| pcre-funded-finite-count | 163.933 (funded) | 166.541 (funded) | 4016 → 4016 |

Funded native capture compile+close is separately **138.246 → 150.644 us**
(+9.0%), **65,904 → 65,536 B/op**. Count allocations are unchanged, and
sampled Count medians change by less than 3%; the unchanged Core control shifts
by +1.7%. Literal/guided samples vary, so these are descriptive operating costs,
not precise equivalence or speedup claims.

**Cold construction has demonstrated regressions.** Short literal text/native
compilation rises **19.0% / 16.8%**, about **0.23 us**. Captured native
compilation rises **10.5%**; captured text +4.9%, property text/native +3.6% /
7.9%. These costs are retained visibly. The completed checked-access campaign's
execution-time allowance does not turn cold compilation into matching throughput
or justify calling these changes equivalent. This migration supplies a required
native ownership/parsing contract; it is not presented as a performance win.

Public source ownership is now direct UTF-8 for literal, character and
backtracking grammar, including Core preparation. Pattern text is explicit
uncached interoperability. Selected names/properties/conditional operands can
project admitted lexemes; native template/metadata work remains open. Native
construction extent/work uses bytes, so coarse Unicode AST admission can grow
relative to text input. Numeric and shared admission still precede acquisition.

## Correctness and setup

- PCRE2 Release: **6,333/runtime**, .NET 10/11; shared Core parser/native
  construction coverage: **1,892/runtime**. The benchmark project builds both.
- Public native grammar/capture/discovery tests include caller-buffer reuse,
  absolute byte starts/prefix context, strict scalars, duplicate names, branch
  reset, subroutines, conditionals, properties, quoting and funded ownership.
  Existing interruption/admission/result/cursor coverage remains. A large ASCII
  comment compiles with less capacity than a whole-source UTF-16 string requires.
- All timed PCRE2 lanes retain identical production IsMatch/Count selectors
  (`Pcre2Literal` or `Pcre2Slice`); the raw receipt records each lane's selector.
- The initial preflight used a copied unsupported PCRE2 `\u` escape in one
  compilation case. It failed before timing. The corrected driver uses literal
  Unicode, passed every preflight and was used for the sole measured run.
  The failed setup/initial driver are preserved separately in the archive.
- Source CI `41083452555` passed Linux but Windows/net10 hit the existing Core
  cursor test's thread-pool handoff deadline. PCRE2, PythonRe, both diff suites
  and Core/net11 passed. The fixture now uses a dedicated worker and awaits it
  in finally before disposal, with the same ten-second bound/assertions. Focused
  cursor/funded-owner checks pass 52/runtime; follow-up CI is tracked in PLAN.md.
  Earlier oracle-fix and PythonRe receipts passed CI `41080917915` / `41080955755`.

## Evidence

- [Raw samples](comparison.json) and [exact measured driver](Compare.cs.txt).
- Immutable VM archive:
  `artifacts/experiments/pcre2-native-compilation-20261030/utf8regex-pcre-native-2f70e6db.tar.gz`.
  Includes exact before/after Core and PCRE2 DLL/PDB files, driver/executable,
  build/preflight/raw output and the failed preflight. SHA-256:
  `8E8B0F9557BE64ADA8CAED88F20D54A78B3DC4C928966BA9A19B1912E1E366FF`.
- Companion `correctness-source-logs.zip` preserves the source bundle, local
  focused/full test and build logs, intermediate failures and VM identity.
  SHA-256: `79E74D0AB8BA62C713F8342D281BD46A699C669DF5429FA2131390D068F33B4F`.
  Subsequent cursor-test coordination failure/fix evidence is in the separately
  archived `ci-followup-logs.zip`; it changes no shipped/measured binary source.

No package bytes, published benchmark catalogs or historical artifacts changed.
The remaining H2/H3 metadata/templates/funded replacement/consumer boundary,
current-source full refresh and cumulative performance qualification stay open.

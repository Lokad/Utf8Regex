# Bounded full-needle discovery for small candidate families

Accepted producer **e658c1e100d7a03b34596d2f1088772c10fcf3cf**, original
baseline **0b5ad679678eddefcf80575d4dc632b39196914a**. Initial producer
**9a2eb3ea** improved the target but regressed a control; it is not accepted.

AWS Count's existing quoted-relation proof supplies eight necessary quote+prefix
needles. Its owned public route uses PreparedSearcher.TryFindMatch and the metered
candidate loop. The standalone automaton's diagnostic timing is a different path;
it was not changed or subtracted from the public cost. The
[diagnostic](before-diagnostic.json) identifies the real 32514627-byte catalog
subject; Core and ordinary/compiled .NET all return zero.

Small raw exact families of three to eight nonempty needles, each <=1024 bytes,
now use checked span IndexOf over bounded windows. Complete-needle lookahead stays
within each 2048-byte primitive, and a full window advances >=1025 starts. Every
eligibility/scan/publication step uses the original invocation. Earliest starts
and original branch priority are retained; the caller still verifies the regex.
No allocation, prepared storage, unsafe access, engine or compatibility switch.
Long/empty families retain spent work and decline to the existing loop; folded,
RTL, pair and confirmed searches retain their qualified implementation.

The initial generalization changed the shared core loop. Its
[70.41s comparison](initial-comparison.json) showed ~13.6x target gains, but finite
pair Count regressed **30.0% ordinary / 26.2% compiled** with unchanged allocations.
That complete candidate was rejected. The repair isolates only three-to-eight raw
exact discovery; a source assertion confirms the qualified shared core body and
file suffix are identical to baseline. It does not tune or reopen the pair path.

One [44.57s focused repair comparison](repair-comparison.json), under a 60s cap,
then compares the same original baseline against the repair. No noise confirmation
or further campaign. Quiet Ubuntu 24.04.4/Xeon Platinum 8573C VM, CPU mask 4,
.NET 11 RC3. Both comparisons use eight independent public lanes: before/after
ordinary/compiled, .NET predecoded ordinary/compiled and decode ordinary/compiled.
Each gets 500ms warmup, an 80ms independent calibration target and three alternating
samples. Actual minimum samples are 19.86ms initially / 20.95ms in the repair;
these custom pilots are not V3 catalog qualification. Results are retained/verified after timing. The repair filters the
original driver's case list to the five rows below; its generic header refers to
the original catalog, not extra repaired measurements.

| Public Count | Before / repaired ordinary | Speedup | Before / repaired compiled | Allocation B/op before / after |
| --- | --- | --- | --- | --- |
| Real full corpus | 148129.7 / 11603.38 us | 12.77x | 147058.4 / 11569.89 us | 0 / 0 |
| Full corpus, finite/live token | 149296 / 11826.56 us | 12.62x | 148519.5 / 11876.45 us | 120 / 120 |
| Mixed-scalar hits, 128 matches | 332.65 / 328.61 us | 1.01x | 336.74 / 339.60 us | 3632 / 3632 |
| Unchanged finite pair control | 24.79 / 23.78 us | 1.04x | 24.78 / 23.77 us | 120 / 120 |
| Unchanged literal control | 0.992 / 0.996 us | 1.00x | 0.992 / 0.995 us | 0 / 0 |

Finite/live Count adds about 1.9% ordinary / 2.6% compiled over the repaired
unlimited route. These policies enforce extra producer guarantees; BCL Count
does not emulate them. No control variation is credited as a producer gain.
Initial-only ASCII separated hits, prefix-present miss and category control remain
in their original evidence, not relabeled as final measurements.

Correctness on both .NET 10 and 11: initial full Core **40115/runtime**;
final focused Core **323/runtime**, complete PythonRe **4224/runtime**, affected
PCRE2 **902/runtime**. New family cases cover cross-window shorter/longer overlap,
3/5/8 and excluded 9-member families, long-needle decline, Unicode byte sequences,
borrowed offsets/tails, every work cutoff, sticky denial, retained caller loans
and actual synchronous cancellation/deadlines. Existing public quoted-relation,
required-family and native invocation tests pass. [Verification](verification.txt).
An initial new miss fixture contained another branch (`bc` in `abc`); `abx`
corrected that test data. Both failed/passing logs are preserved.

Existing `--refresh-readme-case aws-keys/full 5 5` qualifies only the two affected
rows at clean e658c1e1, with minimum sample 54.45ms. Ordinary ratio **1.6451x -> 0.11973x**, compiled
**7.57098x -> 0.58222x**: both Win. Generated README tables reorder accordingly;
every other case is unchanged and parity verification passes. The separate
[refresh](refresh-aws.txt) medians are 10.91/10.92ms; they are not substituted into
the paired comparison. No full catalog/package/platform matrix was repeated.

Immutable evidence under artifacts/experiments/parity-aws-count-20261030/:

- aws-count-e658c1e1.tar.gz: original, rejected and repaired DLL/PDB; source
  bundles; benchmark/profile identities; raw drivers/diagnostics/measurements;
  final snapshots. SHA256
  `C19BA0DA13C2FB17A0FDEC9971758007324BB617D0DE200DB21E25DA0653142C`.
- correctness-logs.zip: original local builds/tests, including the corrected miss
  fixture and distinct initial/final validation. SHA256
  `D3EF97DA7B28D18F2A18768E5BBB69EA5632AC22293248B30F7F9F25AB1673F7`.

Final cumulative cost against a40bda03, a current complete sub-hour refresh,
Windows PCRE2 uncertainty and exact package/actual consumer acceptance remain open.

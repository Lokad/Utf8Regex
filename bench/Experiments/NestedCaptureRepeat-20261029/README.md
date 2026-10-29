# Disjoint repeats through nested capture endings

**Accepted, source 819ab078.** Imports' path class repeat is followed by two
CaptureEnd instructions and an excluded closing quote. The existing no-rewind
proof looked past one ending. It now looks past at most 32 adjacent endings;
all still execute through their original capture trails and publication checks.
No jumps, new matcher, state, grammar, API, unchecked access or policy switch.
Overlap, wider control flow, required ends and RTL/NB keep existing admission.

[Instruction inspection](initial-instructions.txt) identifies the real shape.
That diagnostic uses reverted prefix prototype 1d024a3a, whose IR/preparer was
unchanged from accepted source; its prefix modification is absent from this
candidate. [The earlier census](../AnchoredPrefixCensus-20261029/README.md)
disproved prefix filtering before timing. This experiment optimizes successful
VM verification rather than borrowing its unsupported fixed-set start authority.

[One public A/B](comparison.json), [console](comparison.txt),
[driver](driver.cs.txt), [project](driver.csproj.txt): exact accepted 538a356f
and 819ab078 binaries, .NET 11 RC3, quiet VM CPU2, symmetric load contexts,
500ms warmup, independent calibration, three alternating samples. **40.78s**
under the 120s cap; minimum timed batch **45.79ms**. All producer/predecoded/
decode-then-.NET result preflights and timed checks pass. No confirmation.

| Public operation | Before us | After us | Before / after B/op |
| --- | ---: | ---: | ---: |
| Imports Count, ordinary | 38.917 | 31.887 | 3584 / 3584 |
| Imports Count, compiled | 38.922 | 32.012 | 3584 / 3584 |
| Full corpus, finite limits/live token | 44.366 | 36.908 | 3696 / 3696 |
| Dense ASCII Count | 99.442 | 59.749 | 3584 / 3584 |
| Dense Unicode Count | 104.800 | 62.706 | 3584 / 3584 |
| Boundary hit Count | 2.071 | 1.714 | 3584 / 3584 |
| Detailed Match | 161.673 | 163.327 | 197992 / 197992 |
| Unchanged literal Count | 68.835 | 69.160 | 0 / 0 |

Actual imports Count is **17.8-18.1% less time**, finite calls **16.8% less**,
dense ASCII/Unicode about **40% less**. Allocations are unchanged in every row.
Finite misses +0.9%, detailed Match +1.0%, literal control +0.5%; other negative
control variation, including RTL -6.3%, is not credited as an isolated gain.

[Selected V3 refresh](selected-refresh.txt), five samples/lane, all >=48.51ms:
compiled producer/predecoded **8.40x -> 6.95x**, still Gap; matching-flag ordinary
**4.83x**, still Gap. The former ordinary 8.36x used compiled .NET; that denominator
correction is not a producer gain. [Only these two rows](snapshot-scope.txt)
changed. Markdown came from the existing rewrite, retaining decreasing-ratio
ordering and passing the parity verifier. No other rows or packages refreshed.

[Both-runtime Release checks](verification.txt) cover nested/shared capture
values and histories, literal overlap declines, Unicode/scalar repeat bounds,
assertions/required ends, 31/32/33 capture endings, depth/working denial, every
small work cutoff with a retained parent loan, and original-token/deadline
interruption. Relevant Core guards, the complete PythonRe suite, PCRE2 guards
and API/snapshot checks pass. An initial diagnostic reflection visibility error
is archived. No change to public resource contracts or lifetime ownership.

Immutable files under `artifacts/experiments/nested-capture-repeat-20261029/`:

- `parity-nested-capture-repeat-819ab078.tar.gz`: exact DLL/PDBs, host, source
  bundle, driver/binaries, raw measurements and before/after snapshots.
  SHA256 `721F5ED1F3CEE504D0B845313D1E6D48F1CCA717F42B076C372237E011FD120C`.
- `correctness-and-probes.zip`: fixture, focused checks, instruction and refresh logs.
  SHA256 `0BDD02CF0E99C393DD8250557A9CB6C4FDD683F220C3DCFB97176BB6AE87E51F`.

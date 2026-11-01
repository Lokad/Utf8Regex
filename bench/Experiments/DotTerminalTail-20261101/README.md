# Rejected terminal Dot-tail selection prototype

**Rejected and fully reverted**, baseline `bfe582c0`. No execution, tests,
documentation contract or snapshot change from this prototype survives.
Current production remains the green `50b91b4d` engine.

The prototype extended the existing no-rewind class-repeat helper after its
necessary first-byte filter. A bounded proof collected at most 64 unfolded ASCII
tail bytes ending at Accept/Jump-to-Accept. For greedy Dot maxima 8..511, it
selected the last valid scalar boundary, then ran original RepeatExit/capture/
literal instructions. Other shapes retained their paths. Work/depth costs would
have fallen without new program payload, matcher, switch or unchecked access.
Those proposed resource changes were reverted with the source.

The [initial public Count comparison](comparison.json), **78.955 seconds**, had
twelve equal-result cases/four lanes. Quiet Ubuntu VM/.NET 11 RC3/CPU2, warm
1s/min64, independent 60ms calibration, five alternating samples. Actual corpus
ordinary/compiled improved about27%, finite about28%, dense Unicode hits69%,
false tails74%, shared River30%; allocations unchanged. The unsupported captured-
tail control regressed7.76%. These were prototype observations.

A new direct-production cutoff test then demonstrated an internal bug: final
work denial could return IsMatch=true alongside a breach, preventing TryMatchAt
from clearing partial capture slots. The prototype was corrected and the test
passed. The [final selected comparison](final-comparison.json), **40.316 seconds**,
uses that corrected source and six cases with the same protocol. It is a changed-
source check, not a noise confirmation. Total timed experiment119.272s; no third
stage, selected catalog refresh, full platform/package matrix or noise confirmation.

| Corrected-source public Count | Before us | Prototype us | Change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| Actual corpus, compiled | 241.937 | 178.556 | -26.20% | 3552 |
| Finite limits/live token | 281.832 | 201.253 | -28.59% | 3672 |
| Deadline/live token | 318.761 | 244.956 | -23.15% | 3720 |
| **Unsupported captured-tail control** | **57.783** | **64.270** | **+11.23%** | **3552** |
| Captured literal-repeat control | 11.999 | 12.129 | +1.08% | 6928 |
| Folded literal-repeat control | 22.606 | 22.841 | +1.04% | 6928 |

The captured-tail control regression is stable: all paired samples+11.12–11.50%,
both spreads<1.01. The shortcut declines that capture-start tail; its additional
cost is demonstrated, its compiler/layout cause unproven. Defer this design
rather than use the target gain to conceal the regression. No further tuning.
Final minimum sample58.842ms. [Final summary](final-summary.json),
[initial summary](summary.json), [driver](driver.cs.txt), [identities](identities.json).
The driver is the final version; its selected-stage flag admits only six rows.
The immutable archive retains the distinct original driver and both DLL/PDB sets.

[Validation](verification.txt): initial complete Core41025/PythonRe4387/PCRE26442
per runtime passed. The new denial witness failed before the correction; final
focused Core468/PythonRe82/PCRE236 per runtime passed, net10/net11. Cases cover
scalar widths, greedy endpoints, outer/gap captures, branch priority, 64/65-byte
tails, required child ends, malformed subjects, aliases and every work cutoff/
caller-loan refund, original cancellation and deadline. Final full matrices were
not repeated for a rejected prototype.

Immutable `artifacts/experiments/dot-terminal-tail-20261101/` archives:

- `evidence.tar.gz`: before/initial/corrected DLL/PDBs, drivers, corpus and both
  raw comparisons/patches, SHA256
  `B1191C3945065759EBBE5D8EA071155DD04CB13D9271E3FB32D10C4508A762BA`.
- `correctness-source.zip`: test logs, failed/fixed witness, complete rejected
  source/tests/docs, patches and identities, SHA256
  `5B8BE9384C1FB5735F69E3D76F895E5093E64FEB8037FACA2055ECA3725EAE32`.

Keep the qualified residual window **4.455x** gap and this deferral visible.
`a40bda03` remains the fixed cumulative release baseline; published/staged bytes
are untouched. The rejected control slowdown is not a shipped release debt.

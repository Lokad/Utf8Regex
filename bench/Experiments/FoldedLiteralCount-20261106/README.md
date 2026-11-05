# Folded literal Count: alignment and validation pilot

**Rejected and fully reverted**, including its tests. Source remains280dff2d.
The prototype added single invariant folded literals to the existing scalar-
alignment rule and replaced discarded Count coordinate validation with strict
validity-only plus the existing Kelvin check. No new state, flag or executor.

The real899232B English corpus0D40805F contains non-ASCII text and noKelvin.
[Untimed before](route-before.json) selects owned scalar execution for the single
literal and native for the family; [Windows candidate](route-after-windows.json)
selects native for both, retaining522/725matches. The initial diagnostic probe
used a method where the oracle was a delegate property; corrected before timing.
Existing6.149s phase attribution is diagnostic, not a public speedup or additive
promise. Email is mostly validation cost (~820us vs~273us selected matching).

ONE **91.002-second** separate-Default-process A0/B0/B1/A1 compares accepted
Core55924DE4 with prototype65388AF4; caller and six adapter/benchmark DLL/PDBs are
identical. Normal8CPU startup thenCPU2, warm1s/min16, independent60ms calibration,
five alternating batches. All160 owned batches exceed58.24ms. Results are consumed
after timing and agree with predecoded/decode-then BCL lanes on these languages.
No CPython comparison, catalog Status, confirmation or tuned variant.

| Public Count | Before us | Candidate us | Change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| unchanged exact control | 2.45 | 2.13 | -13.27% | 0 |
| actual folded single | 154.65 | 142.63 | -7.77% | 0 |
| actual compiled single | 155.16 | 142.79 | -7.97% | 0 |
| actual compiled family | 139.44 | 160.09 | +14.81% | 0 |
| actual finite/live single | 1252.46 | 1145.99 | -8.50% | 112 |
| actual deadline/live single | 2019.97 | 1693.40 | -16.17% | 160 |
| dense ASCII single | 51.29 | 52.11 | +1.61% | 0 |
| Kelvin fallback sibling | 326.56 | 334.86 | +2.54% | 0 |

[Raw observations](cost-summary.json) retain all process spreads and control
movement, without normalization. Family before137.55/140.20us is consistently
below candidate159.35/160.11. Single targets improve, but less than the control's
unexplained movement. Controlled calls keep their original route; their favorable
observations do not establish the intended dispatch/validation mechanism. Do not
invent a causal explanation from these numbers. The modest target result with a
larger family cost does not justify promotion or more qualification. This is a
value decision under the user's local-cost policy, not a universal10% gate.

Focused Release Windows/Linux both.NET10/11: Core3412/Python242/PCRE79 and59
NonBacktracking checks pass. Added cases exercise all public outputs for single,
scoped/captured literals, Kelvin/long-S/Turkic/astral coordinates and malformed
suffixes. FullOpts CountCore7793->7743B; code size is not throughput evidence.
After reversal, restored Core3403/Python242/PCRE79 plus59NB pertarget/platform
pass with fresh matching binaries. No producer/test changes remain. Both accepted
and rejected frozen graphs and all historical staged packages remain intact.

Immutable artifacts/experiments/folded-count-ascii-proof-20261106:

- evidence.tar.gz: BE7D898F4464DF9ACC43CBF675B94356F5067344B1DF39868B575FE8E3F858B1.
- windows-correctness.zip: A37275F2E89ED82071E7E8D05003A24AC578442FC94F713CA4E3E4F11962D675.
- restored-tests.tar.gz: 3C30964F5DD15FBCCDC82C283643D4AC74418D86C6246055B0563360B72EADC0.
- restored-windows.zip: 65E2DBF0A743FDDE2A0A2C6FB627F4C7FBA89552F38C2F2F3F81C08C6D642E94.

Predeclaration2C713259 and source/tests patch19963B54 are preserved in the raw.
Attribution archive71D57CEE lives in artifacts/experiments/count-gaps-attribution-20261106.
No package generation, Full refresh or repeated source CI. Do not tune this closed pilot.

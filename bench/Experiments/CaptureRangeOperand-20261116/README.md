# Readonly range operand: rejected

One `in` parameter on GetUnfoldedAsciiRange avoids copying its readonly
20-byte instruction. No algorithm, allocation, validation or metering change.
Release FullOpts capture-root code shrinks2070→2031 bytes and removes both
argument copies. This is not the sampled PGO body or proof of public benefit.

One51.915s ABBA comparison,180s cap, no confirmation. Ordinary single-producer
processes start with8 CPUs, then select CPU2. Five calibrated samples per row/
process, all>=20ms; complete public results checked after timing. Caller-valid
unchanged UTF8, Python re over str; funded rows include finite limits/deadline.
Core literal control keeps Validate. Python/caller/.NET binaries are unchanged.

| Public operation | Before us | After us | Change |
|---|---:|---:|---:|
| One captured string | 2623.115 | 2525.273 | −3.73% |
| Finite one captured string | 613.857 | 637.386 | +3.83% |
| Two captured strings | 377.300 | 366.460 | −2.87% |
| Funded/deadline two captured strings | 1200.943 | 1222.508 | +1.80% |
| Unicode false candidates | 64.251 | 65.205 | +1.48% |
| Unchanged Core literal path | 12.362 | 12.270 | −0.75% |
| Unchanged .NET Count | 23.359 | 22.867 | −2.10% |
| Long first run | 20.720 | 21.131 | +1.98% |
| Missing delimiter | 1.306 | 1.260 | −3.52% |
| Failed first candidates | 124.298 | 127.165 | +2.31% |
| Lazy-root contrast | 185.994 | 185.573 | −0.23% |

Only failed-first-candidate sample ranges separate, adversely. Other ranges
overlap; one-capture timings are particularly broad. Allocation medians agree
except long-run amortized131495.86→131492.36B. No demonstrated public benefit:
restore the source rather than confirm or tune this leaf further. No parity,
native-comparator or canonical-table claim follows from this trial.

Windows and SAME-byte Linux per target: Core388/PythonRe930/PCRE2396/POSIX50;
software preflight passes. Restored production source rebuilds and passes388
Core checks on each runtime. No contract/API documentation change. Initial
private setup-script error was corrected before producer mutation/timing.
Comparison exit0, collector exit0, raw verified offVM; no live worker references.
Qualified dev.100's eight assets remain unchanged,668 feed files total; no
package/Full/platform matrix repeated. Existing source CI remains GREEN/closed;
this receipt is excluded by the existing CI path rule.

Artifacts: artifacts/experiments/capture-range-operand-20261116/.
Baseline e8302d08, Core4F8E1F6D→990CC793; unchanged Python830674FB.
Full DLL/PDB/source identities, process ranges, raw results, codegen and reverted
patch are retained. SHA256 raw:
F1EFD664F59070C09DDF8A6F0712FBBCCB6253F3CADA4EE1EFA51521D18B417A;
validation:D8E6A21AF0DA738B1122F836A31C48E153A32A005D713EB6338430DFAEC9DE0F.

# Count already validated native-value coordinates

Successful PythonRe native literal values now use the existing bounded UTF-16
counter for their prefix and matched extent. Whole-subject strict UTF-8 validation
and complete scalar literal discovery already prove those ranges. Two calls change;
no field, helper, execution mode, allocation or public API is added. Matching units
remain Unicode scalars and primary coordinates remain bytes.

The existing sizing helper polls at scalar-aligned blocks of at most 4096 bytes,
replacing coordinate-only validation blocks of 2048 bytes. Numerical work charges
can fall. Original/current cancellation, absolute deadlines, sticky resource denial,
parent loans, payload admission and retained values stay under the same invocation.
Match, FullMatch, text and capture routes retain their existing implementations.

ONE **98.142680736s** public source A/B, separate Default-process ABBA,19workloads/
380samples/min66.8218ms. Clean baseline `27a5922b` plus only the two substitutions:
Python `1CFE7EE5` to `9B88CA5C`; Core `2377ABAE`, PCRE2 `10360E34` and typed caller
`344A9BCC` stay identical. Existing 750ms/min32 warmup, independent75ms calibration,
five samples,120souter/35sproducer caps and post-timing result checks remain.
No confirmation, retuning, Full refresh or package qualification followed.

| Public Search value operation | Before us | After us | Change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| Actual small Unicode hit | 0.143 | 0.130 | -9.28% | 0 |
| Same finite/live | 0.304 | 0.258 | -15.21% | 112 |
| Same deadline/live | 0.471 | 0.380 | -19.29% | 160 |
| Full Unicode hit finite/live | 1.586 | 0.923 | -41.83% | 112 |
| Same deadline/live | 1.705 | 1.038 | -39.08% | 160 |
| Two-byte prefix | 1.513 | 0.938 | -37.99% | 0 |
| Three-byte prefix | 2.056 | 1.311 | -36.22% | 0 |
| Supplementary prefix | 14.063 | 8.250 | -41.34% | 0 |
| Mixed prefix finite/live | 49.217 | 34.049 | -30.82% | 112 |
| Long ASCII prefix | 0.334 | 0.298 | -10.72% | 0 |

**Promote the change and accept the explicit local costs.** The intended gains
persist in both processes with disjoint overall ranges. All19allocation medians
are unchanged. The Unicode miss costs +28.86%/+104.82ns: before-process medians
0.431/0.295us vary substantially; after0.465/0.468us are slower than both, with
disjoint ranges. The Unicode-family case costs +12.33%/+33.69ns, also disjoint.
Keep these observations rather than claiming a universal speedup.

The [route probe](route.jsonl) confirms the hit routes execute the substitutions.
Misses return before them; the family case takes another route. Identical-PCRE
control+6.59%/+20.74ns and anchoredFullMatch+4.91%/+13.86ns are disjoint too.
BCLcontrol-5.28% is retained without causal credit. Core+1.03%,earlyASCII+1.59%,
captured+1.71% and tinyASCIIcomplete-4.71% overlap. No causal explanation,
normalization or composed .NET/CPython ratio is inferred from these movements.
[Every process/sample](process-observations.json), [summary](cost-summary.json)
and [acceptance](acceptance.json) retain uncertainty and scope.

Windows/Linux each pass121distinct focused checks per .NET10/11, covering complete
mixed-width endpoints across4093..4097byte boundaries, retained values, malformed
tails, nonzero floors, every relevant work cutoff, parent loans, original token
identity and absolute deadlines. Windows reused unchanged passes after correcting
two new fixture assumptions: an overlapping prefix and64-step diagnostic cadence.
VM detached checkout and patch newline staging were corrected before Linux builds;
no producer retuning or timed attempt was repeated. Raw failures/corrections remain.

Canonical immutable evidence, verified off VM:

- `artifacts/experiments/python-coordinate-count-20261107/evidence.tar.gz`:
  `B1B00D51BEBB6713F9407D0719733647156BDC9CD1889CAEC72A71308D1C9E6D`,4783826B.
- `windows-checks.zip`: `5593310D2A0E61501963D071AB49658247B586FBE334DFB2F903A25BCD70ABA4`.

Dev.51 packages, symbols, qualified catalogs and all276staged assets stay immutable.
This source change does not replace the [qualified dev.51 checkpoint](../DeliveryDev51-20261107/README.md)
or refresh benchmark Status. Further package qualification waits for a material batch.

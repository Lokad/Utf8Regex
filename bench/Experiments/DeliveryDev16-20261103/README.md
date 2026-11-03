# Dev.16 fixed-original delivery gate: one remaining failure

Common `0.5.0-dev.16` source is frozen at
`aeeeed9512cb3ef81f36ad3a0ca0bf8ab8550ec3`, including the accepted
[Core Match plan borrow](../CoreMatchPlanBorrow-20261103/README.md) and
[gap-width rewind](../GapWidthRewind-20261103/README.md).
Freeze CI `41643702132` is green on Windows/Linux. No dev.16 package
qualification, staging or Full refresh was started; canonical dev.14 stays intact.

One quiet-VM .NET 11 RC3 run completed **215 public cases in 982.320s**,
minimum sample **26.185ms**. Every existing 189 definition/setting was retained;
24 previously measured entry controls and two H5 predicate pairs were added.
The baseline remains `a40bda03`, immutable archive SHA256 `333FF4E5...`.
Loaded identities and all named outputs preflighted before timing. Existing
warmup/calibration/five alternating samples, post-clock result consumption,
allocations and normal startup before CPU2 affinity remain. The added part uses
only original/candidate producer lanes, with compatible .NET diagnostics separate.

**All 189 earlier controls pass**, maximum adverse 8.37%, including the previous
Search-miss, 17-byte FullMatch and Core gap failures. Their failed historical
receipts remain valid for those earlier sources. No allocations increased.

The [complete acceptance result](cumulative-acceptance.json) is **false**:

| New public control | Original us | Dev.16 us | Change | B/op |
| --- | ---: | ---: | ---: | ---: |
| PythonRe Search hit, `needle` on `xxneedlezz` | 0.075844 | 0.086948 | **+14.64%** | 0 |

All five pairs are +13.20% to +14.90%. This is a stable failure, with no
confirmation, dropped control, successful-part replay or baseline reset.
The corresponding miss passes (-4.49%). Source/parent acceptance does not
override the failed cumulative gate. Next work traces actual successful native
discovery/value projection before changing implementation.

The H5 consumer compiles `^[A-Z]{3}[0-9]{4}$` once, CultureInvariant/infinite
timeout, and checks immutable `ABC0123=true`/`abc0123=false` under None or one live
CTS outside timing. Each iteration performs both public IsMatch calls and consumes
their complete pair result after timing. None is +3.86%, 0B. Live improves
**1.22594 -> 0.47397us/pair (-61.34%)**, **3,584 -> 224B/pair**, or
**1,792 -> 112B per call**. This fixed-original comparison supplements the separate
eleven-route current-source/funded audit; their timing protocols differ.

Immutable raw evidence under `artifacts/experiments/delivery-dev16-20261103/`:

- `cumulative.tar.gz`, SHA256
  `EFB7A409F81B3A6EF7F64441273DA665C3C7CEDEA4DD2DEA4C5D00563C909D25`:
  exact baseline/candidate DLLs/PDBs, frozen source, every driver/project and
  definition hash, preflight, all timings/allocations, scripts/exits and failed
  assessment. RAM evidence was archived and copied off-VM before logout.
- `prepared-consumer-input.zip`, SHA256
  `AF10B6DD68C093947691284CDA969891C466C0BC132A942563C5DD07EB5266AE`:
  original consumer input/audit and preparation scripts. All 1,565 source files
  were audited; only three exact dev.8 -> dev.16 pins changed. This is prepared
  input, not qualification or downstream adoption.

No NuGet publication, package/platform replay or generated-table refresh occurred.
All 215 controls remain required for a fresh coherent candidate after a demonstrated
correction; the completed earlier Full evidence and staged bytes remain immutable.

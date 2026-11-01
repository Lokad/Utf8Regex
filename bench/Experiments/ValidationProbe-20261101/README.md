# Rejected validator-selector prefix search

**Rejected and fully reverted.** Frozen baseline `50b91b4d`; no production,
dispatch, tests or contract changes from this pilot remain. Published snapshots
and immutable packages are unchanged. Its gains do not qualify a release change.

The existing validity-only cost selector counts E0/ED/F0+ bytes in its first
256 bytes and chooses the BCL validator at eight leads. The prototype used a
checked range search to skip bytes below E0 before the existing count. Validator
selection and invocation charges remained identical; no new mode, payload or
unchecked access. This was a performance pilot, not a renewed access-removal campaign.

One [public comparison](comparison.json), **84.826 seconds**, quiet Ubuntu VM,
.NET 11 RC3, CPU2, ten equal-result workloads/four lanes. Warm 1.5s/min64,
independent 60ms calibration, five alternating samples, minimum **60.972ms**;
result batches consumed after timing. [Driver](driver.cs.txt), [summary](summary.json).

| Public operation | Before us | Prototype us | Change | Allocated B both |
| --- | ---: | ---: | ---: | ---: |
| Actual common/backtracking IsMatch, ordinary | 0.2033 | 0.1697 | -16.55% | 0 |
| Same, compiled | 0.2027 | 0.1694 | -16.46% | 0 |
| Same, finite/live | 0.2094 | 0.1755 | -16.22% | 120 |
| Same, deadline/live | 0.3069 | 0.3046 | -0.78% | 168 |
| Large ASCII IsMatch | 1.9568 | 1.7735 | -9.37% | 0 |
| Two-byte IsMatch | 0.6281 | 0.4661 | -25.80% | 0 |
| **Common three-byte IsMatch** | **0.6365** | **1.0389** | **+63.22%** | **0** |
| Constrained three-byte IsMatch | 1.6513 | 1.6571 | +0.35% | 0 |
| Four-byte IsMatch | 0.7241 | 0.7329 | +1.21% | 0 |
| Unchanged metadata-validation Count control | 4.2864 | 4.2797 | -0.16% | 0 |

The three-byte public regression is stable: every paired sample **+61.83–63.44%**,
both lane spreads below 1.02. No noise confirmation is justified. Other public
gains, including controlled calls, do not isolate the selector's causal cost.
The unchanged Count control uses metadata validation and retains its path.

[Focused Release checks](verification.txt) passed on both runtimes: Core425 and
additional223/run, PythonRe203, PCRE2105. The rejected tests check the original
selector threshold over every byte, sliced storage/window boundaries, valid
scalar widths and malformed whole-subject offsets. Existing cancellation,
deadline, work/admission and dependency/API checks passed.

[FullOpts diagnostic codegen](selector-codegen.txt), tiering disabled, shows
145 ->219 bytes and extra call/register preservation. Both versions retain
range-checked slow clones and a hot loop without per-byte bounds checks. This
does **not** establish the cause of the default-tier public regression. Do not
assert an extra hot bounds check or tune another version without a distinct cause.
Console preflight output interleaves part of this raw diagnostic listing.

Immutable `artifacts/experiments/validation-probe-20261101/` archives:

- `evidence.tar.gz`: exact DLL/PDBs, patch, driver, samples and codegen, SHA256
  `2B1AFE417DE6A1953955E3B6421D2E637BAD94CD3A0075CF2A21C15B7B10BC8A`.
- `correctness-source.zip`: test logs, complete rejected source/tests and
  identities, SHA256 `CCB7796A279876BC1BE298B4516FF4CBD21FE2E1D1A1BB6242EB2C4086D14EC0`.

Defer this selector change. Keep common/backtracking's qualified **4.536x** gap
visible; continue the remaining queue without repeating this comparison or
qualifying unchanged packages. `a40bda03` remains the final cumulative baseline.

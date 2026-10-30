# Core byte-native template preparation and grouped output

Producer `98c2a5b2`, fixed baseline `c784498f`. Existing Core UTF-8 template
overloads now parse/cache source bytes directly. Lowered plans retain byte
instructions and original syntax flags; redundant original text, parser-token
roots, instruction text and the string interpreter are removed. The existing
grouped native discovery/capture maps emit byte slices through the existing
output writer. String results decode only final bytes; destination results avoid
another final array. Scalar fall-throughs request their actual output kind.

The first public comparison took **63.367s**, fifteen cases/thirty lanes, under
a 120s cap. The sole focused confirmation took **54.767s**, seven cases/fourteen
lanes, under a 90s cap. This experiment is closed. No catalog or package refresh.

Release .NET 11 RC3 on the quiet Linux VM, affinity mask 4. Each lane calibrates
independently to 80ms. Initial warmup is 1.5s/at least 1,000 calls, three alternating
samples; confirmation uses 3s and five samples. Minimum samples are **57.229ms /
80.706ms**. Public operations include validation, preparation, matching and output;
setup is excluded. Whole output batches are checked after timing. Cold cases
cycle 32 equal-size templates through the existing sixteen-entry cache. Warm
cases reuse one template. Destinations reuse fixed caller buffers with invariant
outputs. Finite controls retain a live cancellation token and all numeric limits.

## Public costs

Initial medians; allocations are actual managed B/op:

| Operation | Before (us) | After (us) | B/op before -> after |
| --- | ---: | ---: | ---: |
| Unchanged Count control | 0.4227 | 0.4242 | 0 ->0 |
| Warm text grouped | 6.2954 | 7.3989 | 1032 ->400 |
| Warm text grouped, compiled | 6.1960 | 7.4121 | 1032 ->400 |
| Warm native grouped | 6.6008 | 7.3524 | 1288 ->400 |
| Warm native grouped, compiled | 6.5674 | 7.3508 | 1288 ->400 |
| Grouped string result | 4.7223 | 5.7155 | 976 ->416 |
| Grouped text template, caller destination | 4.7613 | 5.5701 | 976 ->344 |
| Grouped native template, caller destination | 6.5297 | 7.3137 | 1232 ->344 |
| Cold text grouped | 6.4904 | 7.0139 | 3240 ->2416 |
| Cold native grouped | 6.6172 | 7.3399 | 3360 ->2520 |
| Cold text Unicode ~2KiB | 35.6355 | 18.0671 | 40368 ->26856 |
| Cold native Unicode ~2KiB | 51.1551 | 22.1666 | 42528 ->26960 |
| Native finite/live control | 4.4842 | 4.1833 | 3416 ->3160 |
| Native scalar/Unicode control | 2.6720 | 2.5380 | 7000 ->6744 |
| Native no match | 4.4792 | 5.0105 | 336 ->40 |

Confirmation medians:

| Operation | Before (us) | After (us) | Change | B/op before -> after |
| --- | ---: | ---: | ---: | ---: |
| Unchanged Count control | 0.5473 | 0.5442 | -0.6% | 0 ->0 |
| Warm text grouped | 6.0624 | 7.0667 | +16.6% | 1032 ->400 |
| Warm native grouped | 6.5061 | 7.2465 | +11.4% | 1288 ->400 |
| Grouped string result | 6.2247 | 7.3684 | +18.4% | 976 ->416 |
| Cold native grouped | 6.5827 | 7.2096 | +9.5% | 3360 ->2520 |
| Native finite/live control | 4.0305 | 3.8257 | -5.1% | 3416 ->3160 |
| Native no match | 4.4171 | 4.9912 | +13.0% | 336 ->40 |

Small warm grouped replacements have a demonstrated **0.6–1.1us throughput
cost**, despite lower allocation; this is not a <=10% performance pass or a
claim of equivalent matching speed. The unchanged Count control is stable within
each run, but its absolute timing shifts between runs. String-result absolute
timing shifts too. Keep that variability visible; the grouped direction repeats.
Cold large Unicode templates improve ~49–57% in the first comparison. Those rows
were not repeated in the focused confirmation. Do not extrapolate their gains
to ordinary matching or erase the small-operation costs.

The source is retained for the required native pipeline, simpler retained model
and byte output, with this operating-cost debt recorded before release freeze.
No further confirmation or accounting-precision campaign is authorized by this
receipt. The final fixed-baseline cumulative comparison and consumer boundary
remain outstanding. Basic metering must remain intact.

## Route and correctness evidence

The measured grouped pattern is `(ab[0-9]d)` over `ab1d/ab2d`, with `[$1]` and
Unicode literal prefixes. Both sources prove `AsciiSimplePattern`, null from
the actual scalar-replacement selector, template-worker eligibility and single
capture wrapper 1. This demonstrates the changed worker's public route; exact
literals or scalar-only sparse patterns alone would not. Reflection/IL bridges
run eligibility checks outside timing and bridge public span APIs in timed calls.
Finite/live and Unicode controls explicitly retain scalar selection. The JSON
records the selector evidence per lane.

Local Release evidence on both .NET 10 and 11:

- Full Core: 40,849 passes/runtime before later test-only additions. Final
  focused parser/preparation/authority/cache-boundary run: 120 passes/runtime,
  including native retained charges and two added foreign-failure cases.
- Cross-profile PythonRe: 4,368 passes/runtime; PCRE2: 6,415 passes/runtime.
- Coverage includes malformed-tail precedence, scalar/empty/unmatched/sparse
  captures, ECMA/undefined spellings, NB/RTL, aliases, untouched short destinations,
  cold admission refusal, warm borrowed keys, original cancellation/deadline and
  conservative exact cache boundaries. The old decoded-string admission fixture
  now caps the actual native key array; no limit was weakened.

Native/text keys remain independent entries under one existing bounded cache.
Cold key/parser overlap shares construction admission. Discarded construction
intermediates remain conservatively charged; retained values no longer charge
deleted roots. Coordinate maps used by native shaping remain derived metadata,
not matching units. Funded Core/PCRE2 replacement is still separate pending work.

## Preserved evidence

Raw [comparison](comparison.json), [confirmation](confirmation.json) and exact
drivers [initial](Compare.cs.txt) / [confirmation](Confirm.cs.txt) are adjacent.
The immutable local archive under
`artifacts/experiments/core-native-templates-20261030/` contains before/after
DLL/PDB/builds, both drivers, source bundle, preflight and raw output:
`utf8regex-core-templates-98c2a5b2.tar.gz`.
SHA-256: `0CA0D5E6496BF97DCCF62CD4D8B5D8870A58A8C130FB0882EB681B6CE884C52C`.
The `source-correctness-logs.zip` companion preserves source and the full/cross
and focused logs, including earlier failed preparation fixtures.
SHA-256: `17403F19C285BAEE8598E791F2C17A45DD3ECB5F147C4A518AC4757405B9786A`.
Published/staged package bytes are unchanged.

# Direct lowering for ASCII bracket unions

Accepted for duplicate-work and construction-allocation savings. Positive
parser-owned ASCII `\w/\d/\s` operands inside resolved brackets merge into the
existing admitted 16-byte Set mask. Outer negation remains a scalar complement;
explicit U+001C membership survives ASCII whitespace's narrower predicate.
Unicode/complemented predefined operands keep their existing lowering. Three
parser-owned fragment constants replace duplicated spellings. No runtime engine,
fields, mode, cache, public API or unchecked operation is added.

Baseline `27c2bdae`; Python-only producer change. Selected instruction/mask shapes
are identical. Pattern/input policy, captures, coordinates, original interruption,
major-buffer admission and cursor/result ownership remain. Construction can use
less work/storage because eligible leaves no longer render/reparse through Core;
SPEC-PYTHONRE records that numerical change.

One **corrected 63.366-second** quiet .NET11 RC3 VM comparison: 15 public rows,
ordinary-process ABBA, 300 samples, minimum 55.536ms, 75ms calibration target and
120s cap. Common Core/caller, explicit distinct Python DLL/PDB pairs, eight CPUs
visible before CPU2 affinity. Every process's loaded hashes and complete result
checksums verify independently. Construction uses Validate; warm captures include
both Validate and AssumeValid. Disposal and returned-result checks occur after
timing. Untimed public allocation probes established engagement before this run.

| Public operation | Before → after µs | Before → after B/op |
|---|---:|---:|
| Two captured ASCII unions, construct | 423.235 → 355.999 | 95800 → 86984 |
| Same, construct + first Count | 381.454 → 333.009 | 96072 → 87256 |
| Outer complements, construct | 137.278 → 107.830 | 100832 → 87936 |
| Three unions, construct | 148.682 → 130.856 | 121056 → 107320 |
| Scoped fold/Unicode, construct | 88.654 → 84.480 | 92128 → 87368 |
| Byte-pattern constructor | 95.452 → 89.706 | 95848 → 87032 |
| Warm complete capture strings, Validate | 674.498 → 683.983 | 242008 → 242008 |
| Warm complete capture strings, AssumeValid | 673.664 → 686.310 | 242008 → 242008 |

All timing ranges overlap: constructor medians improve 4.7–21.5%, while warm
medians rise 1.41/1.88%. These are uncertain movements, not demonstrated public
speedups or regressions. Unchanged Unicode/Core controls move +0.58/+1.46% for
construction and +0.92/+0.93% for warm capture output, also overlapping. Accept
the 4760–13736B (5–13%) construction savings and removal of parsing; no warm/native
parity claim, further confirmation, tuning variant or catalog/package refresh.

The **initial 64.661s comparison was unengaged**: eligibility mistakenly used
standalone `[fragment]` spellings, whereas bracket operands contain bare fragments.
Identical allocations prompted the route audit. Its observations are preserved,
but qualify no parsing-removal benefit. The corrected producer uses the parser's
actual constants and first proves allocation reductions for four public constructors.

Full PythonRe Release **5119 checks per target** pass on Windows/Linux, .NET10/11;
Linux additionally passes 47 affected checks per target with hardware intrinsics
disabled. Independent scalar-membership guards cover ASCII/BMP/astral, folds,
outer negation, whitespace, byte/text patterns and both policies. Existing scoped
global/cursor checks and peak-admission/refund tests exercise the new path.

Hash-verified off-VM evidence in artifacts/experiments/python-ascii-bracket-unions-20261111:

- Corrected caller/pairs/patch/Linux/raw: `D89606EA32BD626DC7AED326696CF963376722182E9F0EB39099BE1336347FE7`.
- Initial unengaged run: `FA157E89D6CBF48118E297B2F2A50FACCCFAC8BE7F411E93BC328C558CD0A66E`.
- Windows correctness/pairs: `921A1094C9F11F134882304DC20609F304F26BE5647BF709361E67B7B31A8FFA`.

Complete summaries and independent identity verification accompany the raw archives.
Immutable dev.70 packages and Full tables predate this change. Material warm Python,
strict-input Core and general POSIX history gaps remain active.

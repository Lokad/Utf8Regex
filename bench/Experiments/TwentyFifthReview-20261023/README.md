# Package evidence and bounded PCRE2 grapheme scans

Source changes: `1f03f5b1` (package evidence), `6af80c63` (PCRE2 scans).
Development remains `0.4.0-rc.2-dev`; this receipt is source evidence, not a new
package candidate or release qualification.

## Package evidence

New evidence written through the existing package helper now checks the actual
`AssemblyConfigurationAttribute` and portable-PDB source SHA256 values against
the checkout. A revision label alone does not attest compiled inputs. Relevant
untracked source/build inputs reject, including ignored top-level build inputs.
The existing PE/PDB, SourceLink, revision and package-shape checks remain.

Only known SDK assembly-info, global-usings and target-framework attribute
documents under the matching `obj/<configuration>/net10.0` directory are exempt
from source matching. Unknown generated or unresolvable code rejects. This does
not attest every compiler/JIT option. Historical identity-only readers and
immutable receipts retain their original scope.

`provenance-before.json` demonstrates the former acceptance of dirty-build/revert
and Debug-as-Release fixtures. `provenance-after.json` rejects both and accepts
clean Release-as-Release and Debug-as-Debug controls. Root-input and document
negative receipts cover ignored root targets, unresolved code, unexpected
generated code and missing source documents. Real Core/PythonRe/PCRE2 layouts
pass (329/41/55 PDB documents); final PCRE2 source also passes. Existing negative
fixtures pass nine provenance and three shape checks against copies of frozen
RC1 bytes. No fresh engine NuGet candidate was made.

`provenance-fixture.ps1.txt` preserves the small separate-repository reproduction.
To reproduce, copy it as a `.ps1` under a fresh `artifacts/<name>` directory and
run it there; its paths assume that two-level scratch location. It creates its
own fixture binaries and synthetic package wrappers. Do not run in an old fixture
directory or overwrite historical packages.

## PCRE2 execution

The existing invocation authority now reaches the extended-grapheme (`\X`)
helper through character, repeat, backtracking, candidate-advance and partial
probe routes. Controlled scans charge at entry and each bounded 256-scalar block.
There is no replacement executor, resource-policy mode or new allocated state.
The existing segmentation algorithm and unlimited fast paths remain.

Original-token cancellation, deadlines and finite work limits now apply inside
a large cluster; finite work may deny earlier. Full-subject strict UTF-8
validation, scalar/grapheme results, captures, retained results/aliases and the
remaining resource controls retain their contracts. This closes an unbounded
matching segment that a prior whole-subject validation charge did not protect.

`Pcre2GraphemeInvocationTests` covers six public routes with both character and
backtracking patterns, in-helper cancellation/deadline/work denial, fresh-call
recovery and long combining/ZWJ/regional/Hangul/supplementary boundaries. All 37
cases pass; the affected full PCRE2 suite passes 6,077 tests. Core API/shared
dependency guards pass 13 tests. The benchmark friend closure, generated pages
and existing full producer/PE/PDB/source preflight pass. Windows and Linux source
CI both pass for `6af80c63`; see `ci.json` and `validation.json`.

## Bounded public-operation cost experiment: closed, inconclusive

The same driver loaded frozen before (`4627fda4`) and after (`6af80c63`) libraries
in separate load contexts. Release .NET 10.0.13, Windows processor 20, 2.5-second
warmup per version/lane, 40 ms calibration target; nine alternating pairs plus
the sole 17-pair reversed-first confirmation. Public results/checksums agree.
These are engine-before/after costs; BCL Regex has no overlapping `\X` operation
for a decode/predecoded comparator. No builds/tests ran alongside timing.

| Public lane | Initial change | Confirmation | Bytes before/after |
| --- | ---: | ---: | ---: |
| Long grapheme IsMatch, unlimited | +12.38% | +7.93% | 0 / 0 |
| Long grapheme IsMatch, four finite controls | +0.68% | +2.80% | 112 / 112 |
| Short graphemes Count, unlimited | +14.37% | +5.22% | 0 / 0 |
| Short graphemes Count, four finite controls | +73.23% | -8.73% | 112 / 112 |
| Backtracking grapheme, four finite controls | -4.96% | +5.06% | 112 / 112 |
| Separated-runs IsMatch | -1.58% | +6.63% | 1,680 / 1,680 |
| Unchanged Core Count miss, 1 MiB | +3.29% | +3.90% | 0 / 0 |

Initial/confirmation wall times: 43.52/48.77 seconds. Actual minimum samples
were 16.35/27.82 ms, below the calibration target; spreads reach 5.07x. The
short-cluster penalty did not reproduce. The reversal and spread prevent a
causal regression/gain, equivalence or cumulative <=10% claim. Keep mandatory
metering and close this bounded experiment: no third run or optimization campaign.
Actual-package finite-control and fixed-baseline cumulative acceptance remain
open release gates. Raw samples, exact library hashes and driver/host source are
preserved here; this folder introduces no permanent qualification runner.

Four snapshots, nine frozen RC1 files and five original TODOs retain exact bytes
(`immutable-files.json`). No local full package/platform matrix, intermediate
product package, normal build-server shutdown or VM campaign was performed.

# Inline match-outcome field stores — accepted

Source `44f353940f8accb48622e6254b0b938b259c1461` versus frozen dev.87/d7e28bd3. One AggressiveInlining
annotation on the existing Utf8SliceMatchOutcome constructor exposes field
stores in hot repeat/capture returns. No algorithm, API, fields, ownership,
admission/work units, interruption or allocation change. No new result type,
executor, cached selector or capture-root variant.

The [current capture CPU observation](../PythonCaptureOutputCpu-20261114/README.md)
motivates one warmed codegen inspection. Before: RunClassRepeat3311B/eight static
constructor call sites, terminalCaptureRoot6833B/seven. After:3006B/6537B, no such
calls; root bulk-write-barrier sites8→2. These are static codegen counts, not
calls executed per match or a speedup attribution. Normal default tiering/PGO;
one before and one after bounded public-caller replay. Managed API inventory,
matching DLL/PDB/source and unchanged common Python/caller verified.

ONE61.643-second ordinary-process ABBA, eight rows/five150ms-target samples per
process,160 samples above20ms,180s cap;0 confirmations. Inputs/options and complete
requested outputs agree, checked after timing. No control subtraction or new
CPython ratio. [All process medians/ranges/identities](acceptance.json).

| Public operation | Before us | After us | Observed change | Sample ranges |
|---|---:|---:|---:|---|
| many-capture-strings | 448.510 | 443.299 | -1.16% | overlap |
| one-capture-strings | 158.776 | 152.464 | -3.98% | separated |
| finite-many-capture-strings | 571.091 | 565.870 | -0.91% | overlap |
| detailed-captures | 774.716 | 770.697 | -0.52% | overlap |
| unicode-capture-strings | 503.858 | 485.395 | -3.66% | separated |
| full-class-strings | 213.621 | 205.268 | -3.91% | overlap |
| unchanged-core-literal-count | 12.021 | 12.222 | +1.67% | overlap |
| unchanged-dotnet-count | 21.973 | 22.127 | +0.70% | overlap |

Accept the separated one-capture3.98% and Unicode3.66% improvements and smaller
compiled helpers for this two-line hint. The main many-capture1.16%, finite0.91%,
detailed0.52% and full-class3.91% observations overlap; do not claim those exact
benefits. Retain Core control+1.67% and unchanged.NET+0.70%, also overlapping.
All allocations unchanged, including242008B many strings and246208B finite.
The roughly2x canonical multi-capture gap is not solved. This experiment is closed.

Focused Release Windows.NET10/11 each Core5350/Python1304 and Linux.NET11 the
same counts pass, covering captures/history/closing order, nullable/RTL/NB,
resources, original cancellation/deadlines and retained values/aliases. Tests
use the candidate Core image. No new implementation-mirroring tests or complete
package/platform matrix. Public contracts and immutable staged packages remain.

The private caller's initial local build incorrectly used IDisposable on Core;
corrected before semantic preflight or timing. Raw before/after images, both
callers, PDB/source checks, every sample and default codegen are verified offVM
under `artifacts/experiments/match-outcome-inline-20261114/evidence.tar.gz`, SHA256 `9A19622ACDE34D3EF6A562628D842D0FC3867AD822CB4F7964E3402B610615C1`.
Before-codegen raw `2236EC676D5121D7DB3DEFCAC037D5D674A3CF9D80C54CA89D40030328662687`. All workers/SSH keepers terminal0. No package,
Full refresh, publication or consumer changes.

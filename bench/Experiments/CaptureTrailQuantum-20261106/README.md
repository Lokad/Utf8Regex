# Smaller capture-trail chunks

**Accepted.** Parent `556983ea`; one production constant changes16 ->8 entries
per capture-trail chunk. Every chunk uses the new quantum. Containers, growth,
admission, capture opening/closing journals, rollback, algorithms, SIMD and
ownership remain. Real Unsafe.SizeOf queries are unchanged. No new format,
executor, mode, retained field or accounting mechanism.

An [untimed production allocation probe](probe-before.json) accounts for all400B
of the canonical Python named FullMatch miss: constructor64B, matching336B,
disposal0B. Cursor/state/workspace are values, so a separate executor would target
the wrong cause. With eight-entry chunks, [the same probe](probe-after.json)
measures64/208/0 =272B, exactly matching the public call. The program has one
capture and needs its opening journal; entries also support Python LastIndex.
Modeled first trail reserve falls288 ->160B. No entries are elided.

One **88.632-second** comparison uses four ordinary Default producer processes in
ABBA order. Fourteen public workloads/280 samples, minimum38.575ms; normal
eight-CPU startup thenCPU2,1s/min128 warm calls, independent75ms calibration and
five samples. Only Core DLL/PDB changes; PythonRe/PCRE2 and the worker remain
byte-identical. Ref-struct range/capture metadata projects into ordinary value
observations; result batches and complete FindAll strings are checked after timing.
No confirmation or tuning. [All measurements and uncertainty](summary.json).

| Public operation | Before us | After us | Change | B/op before/after |
| --- | ---: | ---: | ---: | ---: |
| Native Count control | 0.31321 | 0.32586 | +4.04% | 0/0 |
| Actual Python named FullMatch miss | 0.42392 | 0.40918 | -3.48% | 400/272 |
| Python named FullMatch hit | 0.46573 | 0.44831 | -3.74% | 400/272 |
| Python finite/live miss | 0.62909 | 0.61957 | -1.51% | 512/384 |
| Python deadline/live miss | 0.79699 | 0.78992 | -0.89% | 560/432 |
| Python detailed/LastIndex | 0.76856 | 0.75433 | -1.85% | 512/384 |
| Python long captured value | 17.561 | 17.705 | +0.82% | 22296/23984 |
| Python captured FindAll strings | 71.526 | 74.115 | +3.62% | 19904/19776 |
| Core backreference value miss | 1.59605 | 1.59688 | +0.05% | 400/272 |
| Core long capture history | 23.847 | 25.491 | +6.90% | 26824/28512 |
| Core many groups | 0.41872 | 0.43290 | +3.39% | 320/320 |
| Core Unicode rollback/history | 34.482 | 35.749 | +3.68% | 20280/21968 |
| PCRE2 backreference Count miss | 2.06674 | 2.12086 | +2.62% | 400/272 |
| PCRE2 long-backreference Count | 22.525 | 23.069 | +2.42% | 32304/35784 |

Accept the32% short-operation allocation reduction and repeatable named hit/miss
time gains without adding machinery. Both targeted after-process medians are
below both before medians. Preserve the tradeoffs: long-history Core time+6.90%,
Core/Python history allocation+1688B and PCRE2 long allocation+3480B/+10.77%.
Core long/rollback and PCRE2 long timings have disjoint before/after ranges.
FindAll, many-group and PCRE2 miss process movement limits timing precision.
The native control's+4.04% is retained, with both after medians higher; its cause
is unproven. No control normalization or claim that adverse costs are harmless.

Separate equal-result compiled .NET FullMatch diagnostics complete: miss0.05479us
predecoded/0.07243us decode-then; hit0.11451/0.12814us. They neither implement the
finite policy nor establish new CPython/catalog Status. Published tables retain
their actual earlier identities until the coherent Full refresh.

Release checks pass on Windows/Linux and .NET10/11: Core337/PythonRe551/PCRE2111
per target. Existing cases exercise7/8/9,15/16/17 and larger chunk/table crossings,
capture histories/rollback, nested aliases, backrefs, LastIndex, closing order,
subroutines, original cancellation/deadlines and work/depth/working/result caps.
No mirrored test was added. [SPEC](../../../docs/SPEC.md) and
[migration guidance](../../../docs/MIGRATION.md) explain that short captures reserve
less while long histories can require more headers/growth and working capacity.

Freeze CI81c66fc6 found nine outdated assumptions per runtime on both OS. Eight
elision cases computed their value allowance from the trail they omit, reducing
it to1055B below1120B needed by unchanged frame/journal storage. They now use a
concrete1280B public allowance while retaining detailed/cursor working denials.
The explicit modeled trail-charge pin changes288 ->160; size queries remain.
The corrected four fixtures pass113 checks per runtime on Windows/Linux, with
production unchanged. The first Full failed357.03s in tests before any catalogs;
its immutable raw4AC78D1C is preserved under the delivery44 evidence directory.

Immutable raw in `artifacts/experiments/capture-trail-quantum-20261106/`:

- `evidence.tar.gz`: exact before/after assets, allocation probe/driver, public
  driver, declaration, measurements/process logs, Linux checks and source patch;
  SHA256 `ADF25E3E98384DA9D31858BC13F87397D053188C607CD59E377073C1FA80C943`.
- `correctness-source.zip`: Windows checks, exact source/docs, patch, probes,
  analyzer and summary; SHA256
  `7F00752457B13F5C72A3ACAEA315BEF8E4D0FA21ADAF9F5F51472ADDBBD040E0`.
- `working-witness.zip`: failed freeze CI, corrected tests/patch and focused
  Windows/Linux results; SHA256
  `29961A5AB81AFAEAAA9AAC5B103B6B5D7047AEF7E4F751651565AD7942AB797F`.

An initial private-driver compile error selected FindAll rather than
FindAllToStrings; the failed log remains. No production workaround was needed.
All eight frozen Full assets remain unchanged. This experiment did not repeat
package/Full qualification; the batch proceeds to a fresh common candidate.

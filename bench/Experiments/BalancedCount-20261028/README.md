# Reuse the proved balanced-block counter in owned Count

059c3127 connects the existing complete balancing-grammar proof and checked
counter to already-validated owned Count, after required-literal rejection.
There is one counter implementation. Its bounded 128-cell stack is admitted
before use; active nesting counts toward depth, and searches examine at most
2,048 bytes per checkpoint. A deeper input releases local scratch and retries
the scalar VM from the original start with spent work intact. Other operations,
nonzero starts, excluded grammars, RTL and folding retain existing executors.
[SPEC.md](../../../docs/SPEC.md#cancellation-deadlines-and-resource-limits) documents
the modeled depth/working-capacity units. No grammar, API or engine was added.

[Initial route](initial-route.txt), [comparison](comparison.json),
[console](comparison.txt), [driver](driver.cs.txt), [project](driver.csproj.txt).
Quiet VM, .NET 11 RC3, CPU2; exact 14ef506b baseline versus 059c3127.
One **48.93s** public comparison under a 120s cap: four equal-result lanes,
one-second warmup, independent calibration and three alternating samples.
The benchmark assembly supplies the unchanged 53,352-byte catalog subject;
all lanes return exactly ten matches. Counts, not Boolean reductions, are checked.

| Public operation | Before us | After us | Before / after B/op |
| --- | ---: | ---: | ---: |
| Catalog Count, ordinary | 6644.913 | 7.493 | 1904952 / 0 |
| Catalog Count, compiled | 6634.100 | 7.587 | 1904952 / 0 |
| Finite limits and live token | 7429.900 | 6.833 | 1905064 / 112 |
| Dense Unicode blocks | 204.063 | 15.553 | 7392 / 0 |
| Failed outer with complete nested matches | 291.673 | 7.894 | 90880 / 0 |
| Required-literal miss control | 0.831 | 0.833 | 0 / 0 |
| Deeper-nesting scalar fallback | 27.841 | 29.747 | 37800 / 37800 |
| Excluded lazy-grammar control | 508.447 | 543.616 | 56752 / 56752 |
| Unchanged IsMatch control | 404.429 | 438.439 | 24872 / 24872 |
| Unchanged literal Count control | 0.280 | 0.278 | 0 / 0 |

Control/fallback differences range from -0.5% to +8.4%, with unchanged
allocations. Do not claim exact cost equivalence or improvements for controls.
No confirmation was needed. Earlier snapshot and A/B baseline timings differ;
the published old ratio and this same-process before/after are separate evidence.

The [selective six-lane refresh](refresh.txt) qualifies compiled Count at
**0.0157x predecoded .NET 11**, previously **27.36x**; ordinary is **0.00173x**.
Both rows have Status=Win, zero allocation and sample durations above 20ms.
[Parity verification](parity.txt), [Markdown generation](markdown.txt), and
[snapshot/managed-dependency tests](verification.txt) passed. Every unselected
snapshot/parity row was checked unchanged. a40bda03 remains the fixed cumulative
release baseline; no package or complete catalog qualification was repeated.

Focused Release tests pass on both runtimes across Core, PCRE2 and PythonRe.
Coverage includes the existing exhaustive token sequences, failed outer matches,
aliases, Unicode/scalar tails, padded windows, deeper fallback, captures/retained
results, offsets and excluded modes, malformed tails, original cancellation and
deadlines after validation, final publication cancellation, every work cutoff,
depth/stack denial and synchronous local release with an existing parent loan.
Initial attempts had a wrong exception-property name and included invocation
creation in a direct helper's clock witness; both fixtures were corrected.

Immutable local archives under `artifacts/experiments/balanced-count-20261028/`:

- `parity-balanced-count-059c3127.tar.gz`: exact DLL/PDB/driver/bundle, build,
  comparison and final snapshots/reports. SHA256
  `D9ED10BAA03B91180F2DDCC6DDC9314041E7E43C0F1F3D8E6FB20B4470F29A66`.
- `correctness-attempts.zip`: initial and corrected focused checks across profiles.
  SHA256 `DE3417A7001F6A182213FCE8C182040EB750D231D615EE7793292CA9E33C860D`.

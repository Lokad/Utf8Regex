# Four current PythonRe operation costs

The existing paired runner refreshed four selected rows against predecoded
official CPython 3.12.3 on the quiet VM: .NET 10.0.12, default tiering, Ubuntu
24.04.4, one common least-contended logical processor per row. All 91 catalog /
supplementary-coordinate semantic digests pass. The batch completed in 36.715
seconds without a VM build, restore or pack. Frozen current DLLs and host are
bound in [identities](identities.json); source `03981159` has the same production
and benchmark C# as `c6dade89`.

Public operation medians, including required returned results:

| Case | Managed / CPython, µs | Paired ratio, 95% interval | Decision |
| --- | --- | --- | --- |
| Captured findall, 5,376 bytes / 768 strings | 172.180 / 77.617 | 2.210 [2.146, 2.264] | CPython faster |
| Early literal search, 65,542 bytes | 2.009 / 0.168 | 12.029 [11.888, 12.188] | CPython faster; survives harness-floor bound |
| Anchored match, 16,397 bytes | 0.461 / 0.236 | 1.972 [1.952, 1.989] | CPython faster; survives harness-floor bound |
| Limited split, 2,944 bytes / four strings | 1.288 / 0.460 | 2.763 [2.627, 2.853] | **Inconclusive:** lane-order medians differ 7.77% |

Use the split values as diagnostic evidence, not a qualified winner. Its normal
nine-pair measurement received the existing predeclared 17-pair extension, then
stopped. Other rows retain nine pairs. Every final managed/CPython sample exceeds
49 ms. Findall allocates 46,952 managed bytes; the two value-result one-shot rows
allocate zero. Split allocates 6,032 bytes. [Complete summary](summary.json).
Raw paired arrays and their protocol, source, product, interpreter, CPU and
semantic identities are in the four updated `PythonRe.Benchmarks.json` rows.

The managed operations retain strict complete-input UTF-8 validation; CPython's
strong baseline receives a predecoded string. The early-hit cases still validate
their complete subjects. Their ratios do not justify skipping that work. Native
ASCII bytes controls are recorded where the existing eligibility permits them;
they do not replace the public strong baseline or set Status. The limited-split
and captured-findall rows retain their required eager string materialization.

Exactly these four case objects changed. The other 86 rows remain historical;
existing freshness reports **current=4 / managed product changed=86**. Full R10 is
open. Historical independent point timings/routes are separate from new paired
evidence; the priority CLI now labels paired source and point source/route so a
current ratio is not mistaken for execution through an old BCL route. The
generated benchmark page and a local CLI smoke check pass. No timing/protocol or
shipped implementation changes accompany that label clarification.

Initial preflight reuse found missing Git parents and stopped before any timing.
A fresh clean shallow checkout completed the batch; prior shared repositories
were not repaired or modified. Scratch checkout is not assumed to survive later
SSH sessions. Durable per-command row/snapshot checkpoints and both archives are
preserved under `artifacts/pythonre-current-four-20261020`:

- Preflight-only archive: `C3162F76B36775C644BA650C9B8A2EB647000A4B6B86ACA02AFE3FF5B75F7C8E`.
- Complete batch: `94C7FC3998ACB614109F231638F1CF89AD628053CE8C6379782FF36549E4C107`.
- Quiet gate: aggregate CPU 0%, other observed dotnet CPU ticks zero.
- CPU masks: `0x40` for findall/split, `0x20` for early search/prefix; Linux efficiency class unknown.

These are current source costs, not new immutable-package qualification, causal
package-to-package improvements or finite-policy overhead. Actual scalar.115
finite/token/cursor evidence stays separate. No package, consumer pin or broader
PCRE2 qualification is changed.

One subsequent existing findall-phase attribution uses the same frozen host,
source and CPU6, 1,000 effective calls / five samples, default tiering and a
120-second cap. Public findall observes 170.445 µs; prepared string projection
21.781 µs and final shaping 0.491 µs. These are cumulative controls, not additive
costs or a predicted speedup. In particular, `PredecodedDiscovery` and
`CaptureRangeCollect` call the benchmark's BCL comparator, so their 158.627 /
183.686 µs timings do **not** attribute owned-VM discovery. The trailing-capture
replay is noisy (84.529–226.288 µs). No row is requalified from these diagnostics.
The existing no-rewind class-run proof already handles a trailing capture end /
Accept; do not add that proposed shortcut again without a new demonstrated gap.

The attribution source checkout is recreated within its SSH session because
scratch storage was absent on a later session; the failed guard produced no
timing. All prior checkpoints remain durable. Attribution archive:
`8DCCAEB02726DA35F3F99C113E15E7C6298CCBAEED9606BBC14266CB5D5CEC81`.

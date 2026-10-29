# Bounded terminated-prefix value Match

Source **538a356f** adds the existing complete anchored-prefix-until-byte proof
to the <=2048-byte, fully validated ASCII value-Match envelope. The unchanged
helper finds the first terminator after the literal prefix. Original invocation
checks surround that bounded work and the existing projection publishes the
range. Nonzero contiguous-anchor origins, Unicode/long subjects, detailed
captures, folding and NB/RTL retain their existing routes. No grammar, executor,
unchecked access, public API or resource-contract change.

[One public A/B](comparison.json), [console](comparison.txt),
[driver](driver.cs.txt), [project](driver.csproj.txt): exact accepted 47dbeeeb
and 538a356f binaries, quiet Ubuntu VM, .NET 11 RC3, CPU2, symmetric load contexts,
500ms warmup, independent calibration and three alternating samples. **37.34s**
under the 120s outer cap; minimum timed batch **37.58ms**. Equal per-sample
results/range lengths are checked in all four producer/predecoded/decode lanes.
Unicode sinks consume derived UTF-16 lengths in all lanes. No confirmation.

The catalog workload is **31 public Match calls / 1406 UTF-8 bytes** per loop.
The driver uses the real sample file and catalog's appended-LF rule; it excludes
compilation and consumes every result. Other rows retain their own loop sizes.

| Public operation loop | Before us | After us | Before / after B/loop |
| --- | ---: | ---: | ---: |
| Doc-line, ordinary | 20.444 | 4.641 | 52080 / 0 |
| Doc-line, compiled | 19.910 | 4.500 | 52080 / 0 |
| Finite limits/live token | 26.360 | 6.653 | 55552 / 3472 |
| ASCII misses | 5.420 | 1.069 | 3744 / 0 |
| Finite misses | 6.608 | 1.424 | 4640 / 896 |
| Different prefix/terminator | 3.231 | 0.672 | 6784 / 0 |
| Unicode, excluded from shortcut | 2.311 | 2.561 | 5104 / 5104 |
| Long, excluded from shortcut | 40.528 | 42.220 | 3360 / 3360 |
| Incomplete proof control | 2.792 | 2.973 | 3424 / 3424 |
| NonBacktracking control | 99.379 | 98.430 | 19840 / 19840 |
| Detailed-result control | 21.059 | 22.069 | 58032 / 58032 |
| Count control | 27.715 | 28.472 | 52080 / 52080 |
| Unchanged literal control | 0.220 | 0.220 | 0 / 0 |

The target improves about **4.4x**, finite calls about **4x**. Record the control
costs: the four-call Unicode loop is **+0.250us / +10.8%**; other positive control
changes are <=6.5%, with unchanged allocations. The Unicode route is ineligible
for the new condition; the experiment does not attribute its machine-code cost
or establish a universal <=10% bound. Accept the small, existing-helper reuse
with that measured tradeoff disclosed. Negative NB variation is not a gain claim.

[Selected V3 refresh](selected-refresh.txt), five samples/lane, all >=40.62ms:
compiled producer/predecoded **9.20x -> 1.90x**, still Gap; matching-flag ordinary
**1.41x**, still Gap. The former ordinary 9.38x used a compiled .NET denominator;
its correction is not a producer gain. [Snapshot scope](snapshot-scope.txt)
confirms that only this case's two rows changed. Markdown came from the existing
rewrite command, preserves decreasing-ratio order and passes the parity verifier.
No Python/PCRE2 rows, complete catalogs or packages were refreshed.

[Both-runtime Release checks](verification.txt) cover terminator choice, empty/
missing prefixes, NUL/CR/LF, 2047/2048/2049-byte admission, invalid outside slices,
strict malformed suffixes, nonzero contiguous origins, Unicode coordinates,
captures/retained results/cursor aliases, finite denial and original synchronous
token/deadline publication. Adjacent Core work/depth/working/inventory/value
routes and PythonRe/PCRE2 guards pass, as do API/snapshot checks. The prior
repeated-segment correctness fix remains; the rejected mixed-Count prototype
was reverted before this change and is absent from the accepted DLL.

Immutable files under `artifacts/experiments/terminated-prefix-value-match-20261029/`:

- `parity-terminated-prefix-538a356f.tar.gz`: exact DLL/PDBs, host, source
  bundle, driver/binaries, before/after snapshots and raw build/measurement logs.
  SHA256 `4D699D4D713E387C6436F441E3EA7D073C836374EE0CC2986CEDC32EDF19FE4C`.
- `correctness-and-refresh.zip`: final fixture, focused checks and refresh logs.
  SHA256 `78E17D43B89B01224D665B89367B6E8CE13B0A0D70DE19CBBB3A39D07F5CA63E`.

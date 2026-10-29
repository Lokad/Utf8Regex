# PythonRe byte-range cursor and split results

**Accepted, source c96846b7.** The existing durable cursor can advance to immutable
capture/name metadata over borrowed unchanged UTF-8 memory. Range projection skips
captured text and whole-subject coordinate maps. SplitRanges uses the existing
scalar discovery/progression and publishes actual segment/capture ranges. No new
matcher, grammar, unchecked access or ownership framework.

Both cursor result shapes share one position, original deadline/token, cumulative
budgets and alias guards. A range advance clears text Current; an owned advance
populates it normally. Prior snapshots survive either advance/disposal. Range
metadata is owned; input bytes and their memory manager remain caller-owned.
Capture numbering/names, unmatched versus empty, LastIndex/LastGroup and strict
whole-subject UTF-8 are preserved. These byte-only types do not promise scalar
coordinates or aggregate caller-owned memory funding; both remain open in PLAN.

[Public A/B](comparison.json), [console](comparison.txt), [driver](driver.cs.txt),
[project](driver.csproj.txt), [preflight](preflight.txt): exact 39a7fff4 and
c96846b7 DLL/PDBs, quiet VM CPU2, .NET11 RC3, symmetric contexts, 500ms warmup,
independent calibration, three alternating samples. **34.60s**, minimum batch
**33.30ms**; no confirmation. Cursor operations include creation/validation,
advances, requested range consumption, EOF and disposal. The large input is
**262,148 UTF-8 bytes**. Constructors are outside timing. Predecoded/decode-then
.NET diagnostics check equal requested outputs on overlapping semantics.

| Byte-range consumer operation | Before us | After us | Before / after B/op |
| --- | ---: | ---: | ---: |
| ASCII iteration | 28.76 | 23.61 | 10560 / 5952 |
| Unicode/capture iteration | 48.04 | 34.64 | 15104 / 9760 |
| Finite limits/live move token | 43.14 | 33.59 | 10672 / 6064 |
| Large capture | 2981.29 | 2019.40 | 807312 / 4360 |
| Same large capture, finite limits | 2896.94 | 1965.17 | 807424 / 4472 |
| No-separator UTF-8 split | 358.51 | 225.56 | 262216 / 40 |

These are end-to-end **output-shaping** improvements: before uses the existing
detailed API, which materializes unrequested text/coordinates; after uses the
new range APIs. They are not matcher parity gains or new benchmark Status.
Checksums consume all requested group ranges; split consumes UTF-8 segment lengths.
No catalog or historical qualification was refreshed for provenance alone.

Existing API controls retain allocations: ASCII cursor **+3.2%**, Unicode cursor
**+1.9%**, creation-token cursor **+3.3%**, Core finite Count **+1.7%**. Existing
split -1.4% and Python finite Count -1.7% variation are not credited as gains.
No blanket equivalence claim. Published/staged packages are unchanged.

[Release checks](verification.txt): full PythonRe **4082 passed per runtime**,
including 47 focused range/token/API checks. Real public APIs enumerate **500,000**
discarded matches under **16 KiB working**, with no cumulative traffic cap; an
explicit result-traffic cap still denies. A **2,097,156-byte** supplementary/BMP
subject supports a large capture and one-part split under 16 KiB working and
4 KiB result limits, allocating <64 KiB combined and no coordinate map. CPython
split fixtures, scalar offsets, empty retries, lookbehind captures, names/last
capture, aliases, mixed owned/range advances, defaults, malformed tails,
pre-cancellation, active cancellation/deadline and retained metadata pass.
Shared-allowance funding and its concurrent retention checks are not delivered
by these per-invocation tests. No package/platform matrix was repeated locally.

Immutable archives under artifacts/experiments/python-ranges-20261029/:

- parity-python-ranges-c96846b7.tar.gz: baseline/candidate DLL/PDBs, source bundle,
  driver/build/preflight/raw measurements. SHA256
  `F33D6874E42389BB3C261CD3F859D2E94D2C8F7968EC0228980755BC6F380A5A`.
- correctness-logs.zip: SHA256
  `C477D3226E84ACDA2ADE83C2D65F7BFD1745DB5E0545A21AF938831639AFEC3D`.

Archives streamed directly to the local workspace and passed a complete tar
inventory read. a40bda03 remains the cumulative release baseline.

# PythonRe cursor tokens scoped to one advance

**Accepted, source 39a7fff4.** MoveNext(token) applies that token to synchronous
discovery and required snapshot projection, preserving the creation token,
absolute deadline and cumulative invocation counters/loans. The exclusive cursor
temporarily exchanges the existing budget token and restores it in finally.
Two distinct cancellable tokens use one temporary BCL linked source; it is
disposed before return. Caller-facing cancellation retains original token identity
and creation-token precedence. Completed tokens have no later authority.

The original MoveNext() uses the same discovery/projection implementation with
no additional per-step token checks. Aliases still share position/disposal and
reject concurrent/reentrant access. Cancellation faults and cleans up the cursor;
earlier owned results remain valid. No new interruption framework, execution mode,
matcher or unchecked access. Aggregate host-owned memory funding remains open.

[Public A/B](comparison.json), [console](comparison.txt), [driver](driver.cs.txt),
[project](driver.csproj.txt), [preflight](preflight.txt): exact 5bb8be6a and
39a7fff4 DLL/PDBs, quiet VM CPU2, .NET11 RC3, symmetric load contexts, 500ms
warmup, independent calibration and three alternating samples. **31.76s** under
the 120s cap, minimum timed batch **25.25ms**. No confirmation.

Each cursor operation includes creation/whole-subject validation, every advance,
consumption of all owned groups' UTF-16 ranges/text lengths, final EOF and Dispose.
ASCII cases contain 96 matches / 97 advances. Equal checksums pass in both producer
lanes and diagnostic predecoded/decode-then-.NET lanes on overlapping semantics.
Construction of regex instances is outside timing. Comparator conclusions are not
catalog qualifications or new parity claims.

| Public operation | Before us | After us | Before / after B/op |
| --- | ---: | ---: | ---: |
| Existing ASCII cursor | 28.44 | 28.47 | 10560 / 10560 |
| Existing Unicode/capture cursor | 47.33 | 47.18 | 15104 / 15104 |
| Existing creation-token cursor | 39.09 | 38.48 | 10672 / 10672 |
| Existing finite cursor | 38.92 | 37.97 | 10672 / 10672 |
| New sole advance token | 39.17 | 39.86 | 10672 / 10672 |
| New finite advance token | 42.68 | 44.12 | 10672 / 10672 |
| New same creation/advance token | 39.15 | 39.65 | 10672 / 10672 |
| New distinct creation/advance tokens | 38.84 | 47.99 | 10672 / 18432 |

The baseline lacks the new overload. Feature rows compare it with the old cursor
using a creation token; distinct-token baseline has only creation authority.
These show **feature cost**, not equal-authority speedups: sole/same/finite move
tokens add ~1.3-3.4%, with unchanged allocation. Distinct tokens add **23.6%** and
**80 B/advance** for linking (7760 B across 97 advances). This is an explicit API
tradeoff; no attempt to cache/retain move tokens or relax original authority.
Existing paths/Count controls cost at most +1.4% here, allocation unchanged;
negative variation is not credited as a gain. No universal equivalence claim.

[Release checks](verification.txt): full PythonRe passes 4045/runtime, followed
by all ten final advance-token cases/runtime (one final test added); Core 308 and
PCRE2 32 focused checks/runtime. Existing deterministic hooks prove actual-work
and projection cancellation, native discovery without initial controls, original
deadline/token precedence, cumulative work denial, aliases, empty progression,
retained snapshots and synchronous scratch refunds. API snapshot/docs are updated.
No catalog/source-package/platform matrix was repeated for this API change.

Immutable local archives under artifacts/experiments/python-advance-token-20261029/:

- parity-python-advance-token-39a7fff4.tar.gz: baseline/candidate DLL/PDBs, source
  bundle, complete driver/build/preflight/comparison. SHA256
  `4EDF9267624AC8F3FB0A07C7155B307F29A2533CF591CF2184D0BAFBC8A345A6`.
- correctness-logs.zip: SHA256
  `61360A0960BDED9D15589512CF5D576D5346DC9982EE24651F7E2DDD5ACB42FF`.

Archives streamed directly to this workspace and passed a complete tar inventory
read. Published/staged packages and historical benchmark snapshots are untouched.

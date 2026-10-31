# Bounded quoted-prefix value matching

Accepted source `36e3015d`; frozen baseline `e44018eb`. The existing metered
zero-origin, <=2048-byte ASCII value-prefix envelope now admits the existing
AnchoredQuotedStringPrefix family. Its complete grammar proof and checked helper
already exist. No new executor, field, mode, API or unchecked access. Full strict
validation, work before/after the bounded helper, original token/deadline and
value projection remain. Mixed/wider/offset/RTL/NB/unproved shapes retain their
scalar paths. Detailed matching keeps all scalar capture histories. The lower
workspace/depth requirements are documented in [SPEC](../../../docs/SPEC.md).

[Release checks](verification.txt) pass on both net10/net11: Core437/runtime,
PythonRe589/runtime, PCRE280/runtime and snapshot3/runtime. Coverage includes
escaped quotes/backslashes, raw/escaped LF, NUL, Singleline, supplementary scalars,
canaries/invalid tails, 2047–2049-byte bounds, offsets, captures/copied aliases,
all work cutoffs and original-token/deadline publication. Initial runs passed;
stdout-only redirection missed test.ps1's information stream. Matching binaries
were replayed once with SkipBuild/all streams to retain the logs, without another
build or broader matrix. See [capture note](log-capture-note.txt).

One [public comparison](comparison.json), **85.166s**, ten cases/four lanes,
minimum sample **49.091ms**, quiet Ubuntu VM/.NET11 RC3/CPU mask4. The actual
workload makes160 independent public Match calls over2807 bytes. Complete UTF-16
range digests agree per subject and per loop with ordinary/compiled Regex;
byte coordinates and scalar captures have separate correctness coverage. The
full operation includes strict validation and value publication. Setup and
compilation are excluded. Warm1.5s/min1000 calls, independent60ms calibration,
five alternating samples, retained batches consumed after timing.

| Public Match loop | Before us | After us | Change | Allocated B before / after |
| --- | ---: | ---: | ---: | ---: |
| Actual ordinary | 508.224 | 51.438 | -89.88% | 968416 / 30536 |
| Actual compiled | 536.751 | 54.294 | -89.88% | 968416 / 30536 |
| Actual finite/live | 663.041 | 81.965 | -87.64% | 987616 / 49736 |
| Actual deadline/live | 661.823 | 97.685 | -85.24% | 995296 / 57416 |
| Dense ASCII | 120.008 | 0.297 | -99.75%, unsettled magnitude | 111304 / 0 |
| Mixed Unicode/ASCII group | 4.357 | 3.506 | -19.53%, unsettled | 17360 / 11616 |
| Empty/escape misses | 9.209 | 1.115 | -87.89% | 17488 / 0 |
| Large scalar control | 223.440 | 224.729 | +0.58% | 211440 / 211440 |
| Number-prefix control | 19.335 | 19.132 | -1.05% | 0 / 0 |
| Literal control | 0.750 | 0.767 | +2.23% | 0 / 0 |

All actual-workload and genuine unchanged-control spreads are <=1.05x. Dense
ASCII is1.25x/1.32x; preserve uncertainty on the exact effect size. The raw
Unicode-scalar-control lane includes one eligible ASCII subject; it is partially
affected, not proof that unchanged Unicode execution improved. Remaining actual
allocations come from subjects deliberately outside the ASCII envelope. Full
[summary](summary.json) retains every sample/spread. **No confirmation** needed;
no blanket cost equivalence or zero-allocation claim.

Only actual quoted-string ordinary/compiled rows were refreshed from clean
`36e3015d` binaries: **49.924/49.638us**,30536B per loop, **0.344/0.721x** against
matching predecoded .NET11, both recorded Win. The previous compiled ratio6.53x
is a separate historical observation; the fixed-baseline A/B establishes the
producer gain. [Selective log](selective-refresh.txt) keeps raw durations
(minimum29.197ms). Generated parity/Markdown and both-runtime snapshot tests pass.
No full catalog or package was requalified; staged/published bytes are unchanged.

[Driver](driver.cs.txt), preflights and [manifest](identities.json) bind timed
precommit DLL/PDBs to their patch/test hash. The committed-source refresh retains
separate [binary hashes](clean-linux-hashes.txt). Immutable archives include raw
measurements, source/binaries, correctness logs and the prior preserved VM snapshot:

- [Raw/source/binaries](../../../artifacts/experiments/quoted-prefix-value-20261031/evidence.tar.gz):
  `771D3C1BB69E9EFA3C9C1C34EC3B8B6B2C6F23ECA617A9264BF2E256B753930A`.
- [Correctness/source](../../../artifacts/experiments/quoted-prefix-value-20261031/correctness-source.zip):
  `CCEB0CF4D7F52C09F7D73022770EF61F261014F8E86644FEBCF08E9802DF54C6`.

Next primary Core gap is helper identifier6.46x compiled. Final-source cumulative
performance, full refresh and fresh immutable package/consumer acceptance remain.

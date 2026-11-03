# Dev.15 fixed-original delivery gate: not accepted

Common `0.5.0-dev.15` source is frozen at
`58b8291cd953210aa0a86139dfe517bfafd6bc5c`, including the accepted
[class-delimiter Count improvement](../MailCandidateWindow-20261103/README.md).
Source CI `41617013780` and freeze CI `41617620116` are green.
No dev.15 package qualification, staging or full refresh was started.
The previously delivered dev.14 bytes and evidence remain unchanged.

One quiet-VM .NET 11 RC3 run completed all **189 public cases in 891.250s**.
Every existing 172-case definition was retained; only frozen source/version
assertions changed. The same accepted 17-case mail driver was added unchanged.
The baseline remains original `a40bda03`, archive SHA256 `333FF4E5…`.
Normal eight-processor startup precedes the existing in-process affinity.
All expected outputs and loaded identities preflighted; independent calibrated
batches, five alternating samples and full result consumption remain unchanged.
The minimum sample was **28.483ms**. There were no allocation increases.

The [complete acceptance result](cumulative-acceptance.json) is **false**:

| Public control | Original us | Dev.15 us | Change |
| --- | ---: | ---: | ---: |
| PythonRe native Search miss | 0.07355 | 0.08534 | +16.03% |
| PythonRe 17-byte FullMatch | 0.09043 | 0.10982 | +21.44% |
| Core class/whitespace/gap Count | 67.802 | 76.633 | +13.02% |

All five pairs are consistently above the 10% allowance. These are stable
failures, with no noise confirmation, replays, dropped controls or baseline reset.
They do not prove that the latest mail change caused the unrelated native costs.
Its parent acceptance does not establish cumulative release acceptance.

Mail Count still improved from 2,553,362us to 3,740us versus original; finite/live
from 2,823,972us to 5,602us and deadline/live from 2,942,530us to 6,232us.
Those gains do not override the failed controls. Package and full-refresh runners
were prepared but deliberately not executed after the failed gate.

The immutable raw archive is
`artifacts/experiments/delivery-dev15-20261103/cumulative.tar.gz`, SHA256
`39C473624637E2CF3B515FF7A2E58832A83F0E4F637291A72F636D2CDB376F80`.
It contains the exact baseline/candidate DLLs and PDBs, complete frozen source,
all copied driver definitions/projects, definition and asset hashes, preflight,
every timing/allocation sample, scripts, exits and failed assessment. RAM-backed
scratch was archived and streamed off the VM before closing the timing session.

At this checkpoint the new cooperative Core Boolean handoff was integrated into
high-priority H5 in the ignored root PLAN.md and removed. Its 1,792B/live-token
claim describes published 0.4.0, not this development source; current-source
assessment precedes a change. H5 preserves the fixed-original delivery failures
and requires original cancellation, strict scalars, funding/refunds and explicit
remaining allocation/retained-capacity costs. No consumer adoption or publication
is claimed.

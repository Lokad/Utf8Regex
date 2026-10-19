# Borrow the bounded literal-search descriptor

Parent99a4a7bf,current112-dev. Actual scalar111's handoff(a)(b) finite Count
selects ExactAsciiLiteral -> validated TryCountNativeLiteralMatches. It does not
execute the simple-plan helper changed by the previous borrow experiment.
PreparedSubstringSearch is readonly,actual SizeOf40B. Windows.NET10.0.13 FullOpts
shows a40B copy before each TryFindLiteralCandidate call;one parameter becomes
`in`,and the call passes the existing descriptor address. Linux10.0.12 untimed
codegen/count preflights also pass. No body,search/SIMD,2048B windows,work charges,
depth/storage/resource admission,cancellation/deadline or allocation changes.
No field,framework,engine,classifier,mode or public API is added;actual SizeOf stays.

The first optional-prefix preflight did not execute this helper. Before editing
or timing,a minimal literal-only untimed driver verified1K/10K ordinary/compiled
counts128times each and captured the actual call-site copy. Full codegen and
source receipt:artifacts/literal-search-borrow-20261019/RESULT.md.

## One public pair and its sole confirmation

Quiet Xeon8573C CPU2/.NET10.0.12,Core-DLL-only swap,same driver,captured two-byte /
exact eight-byte /Unicode10K hit rows and20K fixed-width control. Ordinary/compiled,
unlimited/four finite controls50M work/depth1024/64MiB working/128MiB result.
Five alternating sample orders,120ms warmup/80ms calibration target,50ms settled
floor,60s child/140s batch caps. These are public Count costs;JIT logging is in
separate untimed processes. Constructors/startup excluded.

Initial default-tier pair23.22s including codegen fails duration/control gates:
samples6.8-49ms,spreads up3.97x,unchanged unrestricted rows move1.8-2.4x. Raw
finite changes are not demonstrated causal regressions. The predeclared sole
candidate-first FullOpts/R2R-off confirmation completes24.08s,all160samples
>=74.83ms,maxspread1.058x. No further timing,tuning or rescue.

Confirmation medians,microseconds(ordinary/compiled):

| Public Count | Unrestricted before -> after(control) | Finite before -> after |
| --- | --- | --- |
| captured two-byte,30KB/10K |102.58->102.33 /102.37->102.24|277.11->270.37 /274.54->272.33|
| exact eight-byte,120KB/10K |137.66->137.18 /136.91->137.79|279.42->269.67 /271.94->261.16|
| Unicode,30KB/10K |104.51->103.87 /104.76->103.19|291.18->282.95 /290.90->283.23|
| unchanged fixed-width,140KB/20K |82.06->82.41 /82.69->82.43|205.20->204.33 /205.41->204.05|

Allocations unchanged:0B unrestricted/112B finite. Finite improvements~0.8-4.0%,
controls within1.5%;small differences are not fixed speedup/equivalence promises.
Accept the proved redundant-copy removal with this scoped evidence. It does not
explain the higher finite Count in111's separate package observation or qualify
default-PGO/PythonRe/immutable111/whole-engine/Status performance. No basic
metering is removed or coarsened. Existing scalar/captures/NB and retained-result
contracts remain. The frozen111 packages and all older receipts stay unchanged.

## Validation and exact inputs

Release Corefocused707/full39746,differential1504,Python165,PCRE80 pass,no skips;
all test-output Core DLLs equal measured candidate. Existing boundary/offset /
long/folded/reverse/Unicode/policy/interruption cases cover the shared helper.
No test mirrors parameter spelling. Profiles build/test sequentially after Core.

Baseline actual111CoreEE9C417DD8363E068EF8F036360070B296CF441AF8203EE8E3CDD5AE622F3370;
candidate75779891BD895BAE00F58860E5EB8EE0A2D834516A12B16345167E99A3096068.
HarnessD65B3235E8A712761ECF1CC789C1ED2E64877F359CE9881BCEBD38C31E1A2244.
[Every raw sample/identity and both summaries](measurement.json). Both VM archives
downloaded/SHA256-verified:5FE9B4F2... /1F84D1F8...;full hashes in measurement.json.
Quiet gatesCPU2=0%,other observed dotnet tickdeltas0. All processes terminal0.

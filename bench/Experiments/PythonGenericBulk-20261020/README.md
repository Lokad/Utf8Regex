# Reuse Python's existing generic bulk cursor

Source parent c0b13b46; actual116 is the frozen baseline. Public3072word FindAll
strings allocated5.41MB through optional Core value enumeration versus190KB
through the existing finite Python path. The generic Core cursor rents slots and
starts each attempt separately; Python scalar state retains its admitted scratch.

Decline TryCollectUtf8MatchRanges before validation only when optional Core has
FallbackRegex execution and None direct family. Reuse existing FindAllScalar,
FindAllScalarStrings and FindAllScalarUtf8. No new executor, owner, classifier,
private hook, public API or switch. Generic bulk discovery now has one retained
workspace and direct required shaping instead of Core cursor/range staging.

Native caller proof: simple and structural arms require different execution
kinds; prefix/suffix/folded arms require explicit direct-family kinds. The caret
prefix proof also requires AnchoredAsciiLiteralPrefix. Those real consumers stay
eligible. No Core value-cursor/alias contract is altered. Other entries retain
optional Core preparation and actual candidate-search consumers.

Strict complete-input UTF8, scalar offsets/empty handling, captures, retained
results, original cancellation/absolute deadlines and major admission remain.
Finite generic/token routes already use this Python cursor, so configured numeric
limits and their modeled units do not change. No second validation, new meter,
helper precision, GC lease or result ownership. Actual SizeOf stays.

One quiet VM FullOpts/R2R-off public before->after pair, .NET10.0.12/CPU2,
55.752s. Same frozen Core3EC6DE92 and driver0A51A056; only Python6232CFFC ->
308D4E36 changes.27lanes/270samples, all>=78.723ms,150ms warmup,80ms calibration
target and five alternating lane-order samples. Required outputs stay retained;
values/counts and match-data byte/UTF16 coordinates agree outside timing.

| Generic unlimited public operation | Before us | After us | Time change | Before/after allocation |
| --- | --- | --- | --- | --- |
|3072word strings|2778.323|953.999|−65.66%|5.41MB /~186KB|
|3072word UTF8 values|2851.842|1048.340|−63.24%|5.41MB /~186KB|
|3072word match data|3357.039|1222.408|−63.59%|5.48MB /~444KB|
|1024structural strings|1194.722|574.401|−51.92%|3.71MB /~65KB|
|512mixed-subject words|618.353|180.860|−70.75%|908KB /~26KB|
|Long mixed miss|802.465|8.424|−98.95%|31424B /0B|

Finite generic/token controls shift−0.94..−5.71%; unchanged Count−1.29..−2.40%.
Do not normalize those shifts or claim exact causal percentages/finite gains.
The much larger changed-path improvements and removed allocations support the
guard without another run. Native-family neighbors retain discovery and show
−3.25..+0.14%; prefix neighbors−4.49..−0.23%. They execute the new metadata guard
and are affected neighbors, not untouched controls. No material native regression.
Mixed-before spread1.136 and other modest variance remain in raw results; small
fractional pooled-allocation shifts are not exact allocation-parity claims.

Full affected Python Release3854 passes; final public ownership25 includes the
new whole-subject malformed-tail miss witness and two generous1MiB public string
allocation regressions. Exact strings, UTF8 values and match-data values are
checked; existing offset, capture, finite control, callback, interruption, alias
and retained-result suites remain enabled. No failing assertion disabled, no
route-existence test, new permanent benchmark framework or package qualification.

Raw source/caller proof, complete public preflights/tests/samples, original payload
and decision: artifacts/python-generic-bulk-20261020. Archive SHA256
9499DD28C4D0F6EC651DBA3FF7B7883082059AD5E6D712DBEEF0FDDD1E855992.
This is a source FullOpts comparison, not default-tier actual-package cost,
CPython Status or a new117 package. Immutable116 and exactCore/PCRE94 stay intact;
new source awaits its coherent delivery boundary. Old decoder/GetText/GetBytes/
repeat/checked campaigns remain closed. Full R01-R10 stays open.

Source commit461c52f1155d82328b3b0d8ad288c5db42124e7b is pushed.
[CI39371613397](https://github.com/Lokad/Utf8Regex/actions/runs/39371613397)
is terminal success: Linux04:52:11UTC, Windows04:53:15UTC. The complete source
Release matrix is green; the run is closed and should not be polled again.
No immutable117 package is designated by this source CI.

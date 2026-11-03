# Reject bounded scalar-gap candidates using a short literal prefix

Accepted source `5fefe08a`. The existing greedy Dot-repeat absence check now
searches for up to eight consecutive unfolded ASCII literal instructions rather
than only the first byte. It inspects at most eight instructions and 2052 checked
input bytes, including the complete prefix at the final four-byte scalar endpoint.
Captures, folds and other instructions end the prefix. A hit still requires the
original scalar VM, including greedy order, newline handling and captures.

No new program payload, selector, mode, unchecked access or accounting mechanism.
The original work charges bracket the bounded search and observe cancellation;
strict whole-subject validation, original deadlines, retained results/aliases and
numeric resource admission remain. Successful work requirements can decrease;
no public contract changes. The existing native ASCII routes remain available.

One **95.685s**, 15-case public comparison against frozen `dd492ee0` DLL/PDBs.
Only Core changed; benchmark/PythonRe/PCRE2 assemblies are byte-identical.
Normal eight-CPU startup then CPU2, two-second/min512 managed warmup,
independent60ms calibration and five alternating samples. Results are consumed
after timing; minimum sample31.314ms. Matching-option predecoded/decode .NET
lanes check equal results and provide diagnostic timings, not published ratios.
No confirmation or alternate implementation was needed.

| Public Count | Before us | After us | Change | B/op before/after |
| --- | ---: | ---: | ---: | ---: |
| Holmes corpus, ordinary |243.592|195.363|-19.80%|3552/3552|
| Holmes corpus, compiled |244.035|195.476|-19.90%|3552/3552|
| Finite limits/live token |267.384|215.567|-19.38%|3664/3664|
| Deadline/live token |319.528|265.650|-16.86%|3712/3712|
| River corpus, compiled |5588.264|3475.528|-37.81%|3552/3552|
| Unicode false-first-byte misses |215.106|26.029|-87.90%|3552/1936|
| Unicode hits |210.935|207.360|-1.69%|3552/3552|
| Single-byte tail |13.587|14.982|+10.27%|64/64|

[All observations](summary.json) retain captured gaps, Unicode newline failures,
short repeats, native ASCII and unrelated controls. The single-byte-tail cost
is about1.40us and is accepted for the larger corpus/false-candidate gains under
the user's local-trade-off policy. Ordinary/finite/deadline target spreads are
at most1.013; compiled1.109/1.075 is less stable but agrees with those tight lanes.
Unchanged literal/Python/PCRE2 controls move+0.14/+0.38/+0.20%; nativeASCII-1.02%
is an observation, not a claimed source improvement.

Focused Release checks pass per .NET10/11: Core689, PythonRe271, PCRE278. Tests
cover scalar-width/final-window endpoints, long/truncated/folded/captured tails,
nonzero origins, captures, NonBacktracking, original interruption, every work
cutoff/borrowed loan, malformed tails and retained aliases. Existing profile
resource/cancellation checks pass. Full/package qualification stays at a coherent
delivery boundary; immutable dev.19 packages and the source87 frozen43m08s Full
receipt are unchanged. This is not a new cumulative fixed-original qualification.

Immutable evidence under `artifacts/experiments/bounded-dot-tail-prefix-20261103/`:

- `evidence.tar.gz`: `289D79B54FDB55DD3A3F4D44F20C6A1FFCB31631255F6804FC5F8439D73B2BA9`.
- `correctness-source.zip`: `6D54CD1CE26798811955964F3991D56B2BB059F1A19FD30FD4BEB25B4C5350A1`.
Selective publication on clean `5fefe08a` changes only these two cases' four
ordinary/compiled records. Holmes compiled **4.11x -> 3.32x** predecoded .NET;
River is newly qualified at **2.26x** compiled; its prior short-duration rows
remain historical Unqualified. Ordinary ratios are0.736x/0.103x; all
still3552B. Existing parity verification passes; minimum selected sample45.014ms.
Matching-binary Core snapshot guards3/target pass with -SkipBuild.
Six root tables retain178 ratios in descending order. Other measurements and
identities are unchanged; selective publication is separate from causal A/Bs.

- `selective.tar.gz`: `6EE123E2C050FF8BDF6740D0FD5FBE70ACDAC3B07B06DC5BDC6F9889C9179DF0`.
# Reuse bounded pure-family discovery

**Accepted**, source `cc0bcb40`. Pure LTR exact families of3..8 needles without
boundary/trailing requirements borrow the existing complete-needle search helper.
Its2048-byte spans,1024-byte needle guard/generic fallback, ordered tie selection
and original invocation remain. Callers retain complete-subject validation and any
enclosing regex verification. No new runner, fields, preparation, mode or unchecked
access. The broader native eligibility rule and rejected single-needle16-byte
cutoff remain unchanged; no new Python root retention or cursor dispatch.

Eligibility and per-branch/window/publication checkpoints remain bounded and metered.
Work-unit requirements change with the route: false-candidate replay disappears,
while dense hits can pay extra eligibility/search work. Captures, retained aliases,
working/result admission, no partial count, original cancellation and absolute
deadlines remain. The specification records these version-dependent charges.

ONE **99.7921925s**,17-case public Count comparison uses the frozen dev.24 Full
Core `00a97561`. Only Core DLL/PDB changes; six adapter/benchmark companions remain
byte-identical. Normal eight-CPU startup thenCPU2,1.5s/minimum128 managed and
250ms/minimum64 diagnostic warmup, independent60ms calibration, five alternating
samples. All170 owned samples qualify, minimum **56.4042ms**. Every Count is consumed
after timing. Allocations are unchanged in all cases. No confirmation or variant.

| Public Count | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| ASCII control first | 2.50 | 2.50 | +0.13% |
| Actual five-branch Chinese, live | 6830.40 | 637.42 | -90.67% |
| Actual five-branch Chinese, deadline/live | 14006.84 | 676.27 | -95.17% |
| Five-branch finite/VM control | 48355.65 | 47799.25 | -1.15% |
| Five-branch unlimited/SIMD control | 153.91 | 154.17 | +0.17% |
| Actual three-branch finite/live | 4586.64 | 488.23 | -89.36% |
| Dense first-branch live | 60.12 | 45.46 | -24.39% |
| Dense late-branch live | 73.42 | 222.48 | +203.04% |
| Dense false prefixes | 311.77 | 20.55 | -93.41% |
| No-prefix miss | 326.13 | 37.52 | -88.50% |
| Ordered overlap, long first | 109.53 | 66.45 | -39.33% |
| Ordered overlap, short first | 142.35 | 86.31 | -39.37% |
| Short slice/tail | 0.41 | 0.48 | +15.01% |
| Long-needle fallback | 0.93 | 1.11 | +19.51% |
| Folded family control | 83.26 | 98.21 | +17.96% |
| Reverse family control | 562.84 | 562.23 | -0.11% |
| Python Count control | 9.70 | 9.68 | -0.24% |

The dense late-branch cost is real: about3.03x, adding149us. Short/fallback costs
are about0.06/0.18us; folded control adds14.95us. Preserve all observations without
assigning speculative causes. Promotion follows the user's local-cost/major-gain
policy: actual live/deadline operations save6.19/13.33ms, finite three-branch4.10ms,
and dense false-prefix/miss/overlap cases improve substantially. No universal10%
gate. [All observations](cost-summary.json) retain spreads and qualification flags.

Same-options .NET predecoded/decode-then lanes are separate diagnostics. This is
not a catalog Status, cumulative speedup, all-profile policy comparison or total-
heap claim. Existing dev.24 packages and Full data remain immutable; this source
change is subsequent work. No catalog refresh, package qualification or Full rerun.

Focused Release checks pass on Windows/Linux for .NET10/11:3426 Core including
funded/cursor checks,483 PythonRe and961 PCRE2 per target. Windows Core uses3223
affected checks plus203 matching-binary funded/cursor checks. New existing-fixture
coverage adds three/five-branch ordered live/deadline cases, false prefixes, window
crossings, borrowed slices, malformed suffixes and every three-branch work cutoff.
The source/PDB/DLL/corpus identities, preflight and focused results are archived.

Immutable evidence under artifacts/experiments/pure-family-window-20261104/:

- evidence.tar.gz: `E531B0F9D817BE07459E287E3BA93D7E16F08B6157231831F6DB769C5D188974`.
- windows-correctness.zip: `BA2C46507B9A4CA092C364D8080BA7350191A06FFDA71D1A87B23C8D251ECF07`.

The incremental404744-byte bundle includes committed dev.24 data. VM Full snapshots
were hash-checked against their off-VM archive before restoring only those seven
known paths for alignment. No protected cleanup or historical byte changes.

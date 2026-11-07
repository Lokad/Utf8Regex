# Controlled PCRE2 byte-only literal Count

Extend the existing byte-only literal Count selection to controlled invocations.
Reuse strict bounded validity-only validation and the existing shared bounded
candidate/comparison kernel under the same cumulative invocation. Count needs no
UTF-16 metadata, global cursor or result buffer. Keep the unmetered prepared
counter, existing nonempty/unanchored eligibility, no-match-options rule and
1,024-byte suffix threshold. Other operations and ineligible patterns retain
their paths. No field, owner, subject mode, engine or framework is added.

Work admission remains before bounded search/comparison. The shared kernel uses
at most 2,048-byte search windows and separately charges long-literal comparisons;
this replaces the former 4,096-byte PCRE2 candidate windows for this Count route.
Tight numerical caps may require readmission. Validation, search and terminal
publication share the original token, absolute deadline and sticky work budget.
Working/result/depth limits and borrowed parent loans remain; the integer result
needs no capture/result buffers. [The public contract](../../../docs/SPEC-PCRE2.md)
records this numerical admission change.

## Public costs

ONE separate-Default-process ABBA completed in **101.816 seconds**, under its
120-second cap: 20 public rows, 400 samples, minimum 59.4467 ms. The identical
caller uses one producer per process, 750 ms/minimum 32-call warmup, independent
75 ms calibration and five samples with a 20 ms floor. Startup sees eight CPUs,
then affinity selects CPU2. Results are consumed after timing. All 20 allocation
medians are unchanged.

| Actual Chinese-text PCRE2 Count | Before us | After us | Change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| Finite policy/live token | 2802.16 | 117.90 | -95.79% | 112 |
| Live token | 2781.56 | 116.68 | -95.81% | 112 |
| Deadline | 2792.58 | 122.83 | -95.60% | 160 |

All three gains persist in both processes with disjoint sample ranges. Finite
process medians 2803.68/2795.01 us become 117.88/117.92. The same-run finite
extra cost falls from 2720.54 to 41.16 us; relative overhead falls from 3333.13%
to 53.63%. This remains useful operating cost, rather than zero-cost metering.

Keep overlapping adverse estimates: ordinary dense two/three/supplementary-byte
Count +2.78%/+2.32%/+1.05% and wide miss +0.35%. Ordinary PCRE2 Chinese -5.98%
overlaps; unchanged Core Chinese -6.33% also overlaps with one faster after
process around 72 us versus about 81 us elsewhere. Do not credit the ordinary
route with a new gain. Unchanged ASCII Core -0.37% and predecoded BCL -0.22%
overlap. Excluded 1,023-byte, anchored, empty and match-options cases improve with
disjoint ranges despite bypassing the new route; retain them without a causal
claim. [Summary](cost-summary.json) and [acceptance](acceptance.json) preserve all
20 rows, process medians and ranges. No normalization, confirmation, producer
tuning, composed speedup, universal 10% gate or new catalog Status.

Input and caller are unchanged from the [unmetered pilot](../PcreCountValidation-20261107/README.md):
813,478 LF bytes, SHA256 `F129E81928C58ECBBA0CCBB63B36679355345248DF057D1E9DED670D6E9C964B`,
needle `夏洛克·福尔摩斯`, 30 matches. These separate pilots are not a cumulative
package comparison.

## Validation and preservation

Release Core 477/PCRE2 111 unique checks per target pass on Windows/Linux for
.NET 10/11. PCRE2 consists of the initial 110 checks plus one additional public
denial-precedence fixture; unchanged suites were not repeated. Coverage includes
scalar offsets/window edges, nonoverlap, malformed tails, tight work cutoffs,
sticky denial/no partial count, long needles, retained coordinates and borrowed
parent capacity. Public deterministic witnesses interrupt during validation and
search with the original token/deadline and PCRE2 failure vocabulary.

The initial 8 KiB interruption fixture did not reach the existing 64-step hook.
It was corrected to a 256 KiB public witness with validation/search checkpoints
at steps 64/192. Its original failure is preserved. The supplementary public
work-denial/pre-cancellation fixture was added after producer freeze; matching
producer DLL hashes remain unchanged and only that new fixture was run. No
production change followed freeze. Final tests, both preflights and the sole
timed attempt pass.

Untimed FullOpts CountCore is 1544 to 1658 bytes; the controlled counter is
454 bytes. Validity-only admission inlines to TryValidate with the UTF-16 flag
false, and the counter calls the existing TryFindLiteralCandidate. These are
[code-generation facts](codegen-summary.json), not timing attribution. No
unchecked access, manual size estimate or shipped dependency is introduced.

No package or Full qualification was repeated. All 282 staged assets and frozen
Full52 graphs remain unchanged. Raw evidence is copied off VM and hash-verified:

- `artifacts/experiments/pcre-metered-count-20261107/evidence.tar.gz`:
  `4FE494D2892FC1FFDC570CA0C3D1799FFA0BA5493FDC3D294FF3E9A38CDB6917`.
- `windows-evidence.zip` in that directory, including the supplemental Linux check:
  `3A2AF26CE6544B66F90BD2275BE505A1D50E422AF7B30A5A08F7A96C7C2434A1`.

Parent `328f1577`; Core `8AFEDEC7` to `0B032B40`, PCRE2 `23FA2CBA` to
`7EDAF253`, fixed PythonRe `47870A68`, caller `0E9A9311`. Exact hashes and both
test-patch revisions are retained. No NuGet publication.

Source CI [passed on Windows and Linux](ci.json) for `dae93ecf`.

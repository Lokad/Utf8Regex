# Common 0.5.0-dev.36 delivery checkpoint

Frozen source `d03c31479a5ab6af978cd48acdc02a5af8649353`; Core, PythonRe and PCRE2
target .NET 10/11 with exact adapter-to-Core pins. Packages and symbols are staged
in artifacts/nuget. Publication and downstream adoption remain separate.

The batch removes unused Count coordinate work and empty VM resets, widens the
existing checked packed-nibble scan on AVX512BW, and admits finite Count's four
to eight literal alternatives to its existing metered search. Strict complete-
subject UTF-8/scalars, captures, aliases, synchronous cancellation/deadlines and
useful resource limits remain. Avoided VM scratch intentionally reduces route-
dependent work/depth/working requirements; coordinate-offset admission remains.
See the [resource contract](../../../docs/SPEC.md).

The isolated comparisons have unchanged controls and separate producer processes:

- [Count metadata](../CountValidationMetadata-20261105/README.md): actual
  declaration rows move 1–3% amid variation; explicitly novel Unicode query siblings improve
  71–77%, with unchanged allocations.
- [Empty resets](../EmptyWorkspaceReset-20261105/README.md): actual Unicode Split
  improves 6.44%; measured neighbors retain noisy local costs up to 3.29%.
- [Packed nibble](../PackedNibbleWide-20261105/README.md): actual Chinese-family
  ordinary/compiled Count improves 17.00/16.44%, with unchanged allocations.
- [Finite family](../FiniteFamilyCountEight-20261105/README.md): actual finite/live
  Count falls from 45.1 ms to 0.612 ms, deadline from 46.3 ms to 0.650 ms, saving
  528 B/op. Existing bounded search and interruption admission are reused.

These percentages are not additive. The bounded
[Python checkpoint prototype](../PythonStringPhaseCheckpoints-20261105/README.md)
was fully reverted after its only comparison expired; it has no performance verdict.

Windows qualification takes 288.4158166 seconds: two root builds, one pack per
profile, eight detached fixtures/109420 checks and six target assets pass. Original
copied Awk19/Lython118 per runtime and public native/funded probes pass with final
root loans zero. The 1565 consumer-source files differ only at three exact pins;
464 compiler documents and original restore identities match. All 186 prior
package/symbol outputs remain unchanged. Runtime CI42092240369 and freeze
CI42093572074 pass Windows/Linux.

One **2609.2213558-second (43m29.22s) Full** passes the original 3600-second outer
cap. The [manifest](full-refresh.json) records Full/Complete/terminal zero and all
83 expected steps: one Release build, both-runtime source tests and disabled
dynamic code, complete .NET 11 catalogs, paired comparators, lifecycle/scaling,
resources and final verifiers. Build66.83s/tests273.27s/Core434.96s, Python
raw483.66s/pairs462.87s, PCRE raw305.50s/scaling84.90s, resources101.19s. Original
8 GiB available-RAM/1 GiB repository guards pass with zero readiness wait and no
server shutdown. Normal eight-CPU startup then CPU2, pinned .NET11RC3 and official
CPython3.13.2. Package/consumer clocks are separate.

[Fresh rankings](ratio-summary.json) preserve distinct comparators and uncertainty:

- Core118 wins/4 ties/52 gaps/4 unqualified. Largest qualified ratios are Chinese
  literal4.62x (ordinary), 3.62x (compiled), compiled Chinese family3.33x, email
  2.64x and imports2.61x. River-window and symbol remain unqualified in both modes
  under the existing V3 duration/provenance rules. No floor waiver or favorable rerun.
- Python39 managed wins/38 CPython wins/3 equivalent/5 inconclusive/5 unqualified.
  Largest qualified gaps: early literal2.92x, named-backreference miss2.82x,
  Unicode Split2.62x, verbose2.57x, date miss2.48x and captured FindAll2.17x.
  Alternation miss, ASCII-option hit, multiline hit, offset literal and Unicode-
  category miss fail the existing removable-floor check; retain their exclusions.
- PCRE79 managed wins/11 native wins/1 equivalent/10 inconclusive/25 excluded.
  Date4.39x, subroutine4.34x and date miss4.06x remain. Broader PCRE completion
  stays independent of Core/Python delivery; native PCRE.NET is benchmark-only.

The following are successive Full observations, **not isolated causal estimates**:

| Public Python operation | dev35 / dev36 us | dev35 / dev36 CPython ratio | B/op |
| --- | ---: | ---: | ---: |
| split/unicode | 128.576 / 116.038 | 2.92x / 2.62x | 19512 |
| findall/one-capture-strings | 487.743 / 459.322 | 2.28x / 2.17x | 95000 |
| findall/named-capture-strings | 150.050 / 156.697 | 1.60x / 1.66x | 43800 |
| count/one | 0.254 / 0.287 | 0.127x / 0.145x | 0 |

Retain the adverse named-FindAll4.43%/6.65us and Count12.95%/0.033us movements;
their causes are unproved. The user accepts documented local costs for substantial
gains. The completed checked-access10% allowance is not a universal release gate.
No normalization, selective repeat or compounded gain is used here.

Linux consumes the exact Windows-produced bytes: eight detached fixtures/109420
checks, original Awk19/Lython118 per runtime, public probes with final root loans
zero, and six assets/24 source/PDB/feed/cache/lock audits pass. No producer rebuild
or repack. All eight measured Full DLL/PDB hashes remain unchanged. Seven measured
files are imported once, with only the root introduction's duration/receipt link
updated beyond measured bytes. Four generated blocks, six descending tables and
178 ratio rows verify; six matching snapshot hosts/ten checks pass with SkipBuild.

Canonical artifacts/experiments/delivery-dev36-20261105, verified off the VM:

- full-evidence.tar.gz: `DDD95F033FAC0169A35CEB189791ED6200F5E8A3549E7185DCF110DF1A7A7461`.
- linux-evidence.tar.gz: `069C50D84452CDBC1CF574585998BCC3CA90F9D7AE81BD9A2AAF47283C02C332`.
- windows-evidence.zip: `6C83E865BFE91875F821D8166B49EE0FA0D108CE8530352F877A331A2B32AAE5`.
- windows-restore-inputs.zip: `992AE0C4C1CCB936421EE950812DB0C38CB22180191D76B009CF96DF50CE301E`.
- windows-compilation-inputs.zip: `FF6678632D14958C1C30F22D330A193835FE67F5F9B7C45E1F5B4013A7137C2C`.
- post-refresh-guards.zip: `1744EBF0AA7BB192CB0B6C3C0D09FE9B2D0051961D66D6137772393FFFFF84A2`.

The Linux archive wrapper initially failed on two absent optional receipt files
after qualification succeeded. The completed archive exports that same run,
including its actual preparation-capacity log and recovery record. No tests,
packing or qualification were repeated, and missing historical observations were
not reconstructed. Original sourceCBD35E99, repinnedC4FBEF08 and transfer9A40EF09
remain sealed. This checkpoint does not authorize publication or replace original
downstream adoption/POSIX acceptance.

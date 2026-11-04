# Dev.26 delivery boundary

Common **0.5.0-dev.26**, frozen source `e731a4526f4b2c3071f37876fb8c0285c3be17b4`,
all three packages/.NET10+11 with exact Core pins. Staged in artifacts/nuget,
**not published**. Freeze CI41848475983 is green on Windows/Linux.

This batches [family search reuse](../PureFamilyWindow-20261104/README.md),
[optional lowering](../OptionalRepeatBranch-20261104/README.md),
[literal dispatch](../AsciiLiteralRun-20261104/README.md),
[first-match candidate reuse](../CoreFirstCandidate-20261104/README.md) and
[unobserved value captures](../ValueCaptureElision-20261104/README.md).
Their matched public comparisons retain all local costs, allocations and uncertainty;
these are separate experiments, not a cumulative speedup claim. Local regressions
are accepted for substantial gains. The completed checked-access allowance is not
a universal delivery gate. Scalar/capture/lifetime/resource/interruption contracts
and managed, checked, owned execution are preserved.

Windows qualification **292.4392176s**. Windows/Linux each pass eight detached
fixtures/**108798 checks**, six assets, original Awk19/Lython118 per target and
native/funded public probes with final root loans0. Original1565-file consumer
sources change only three version pins. Linux consumes exact Windows bytes,
without repacking;464 original compiler documents and24 Linux restore records
are retained. Compatibility is distinct from adoption/POSIX acceptance.

Dev.25 failed on seven obsolete expectations that value Match requires unobserved
capture storage. Corrected tests require Boolean/value success and detailed-result
working-byte denial; old+new84 checks pass per target. Production is unchanged.
Attempt73325e348553 and its feed remain preserved; dev.25 was not staged/qualified.

The sole [Full refresh](full-refresh.json) passes in **2611.7671915s =43m31.77s**:
one Release build, both-runtime correctness (**108838 source checks**), disabled
dynamic code, all .NET11 catalogs, paired comparisons, lifecycle/scaling, resources
and verifiers. Full/Complete/terminal0, exactly83 expected steps in order, all Passed,
under3600s. Readiness760s; combined **56m11.77s**. The original8GiB guard admitted
8964216KiB at polling and8963340KiB at Full preflight, without shutdown or weakening.
Fresh2173821304B fixtures/807867543B archive duplicate were retired after canonical
off-VM SHA verification and tar comparison. Historical/protected bytes unchanged.

Seven generated files were hash-imported; four generated blocks/six descending
README tables/178 ratios verify. Snapshot guards pass six hosts/ten checks with
matching binaries and no rebuild. No additional benchmark or package qualification.

[Current rankings](ratio-summary.json): Core115 wins/57 gaps/6 unqualified;
Python36 ManagedFaster/38 CpythonFaster/1 equivalent/12 inconclusive/3 unqualified;
PCRE2 80 ManagedFaster/10 NativeFaster/11 inconclusive/25 excluded. Preserve all
uncertainty. Python's three floor-limited rows remain unqualified, with no favorable
rerun. Independent catalog movement is not isolated causal evidence.

Largest Core gaps remain Chinese literal5.00x, compiled Chinese alternation4.24x,
compiled Chinese literal3.96x, imports3.14x(13.98us/3568B), window3.12x(182.47us/3552B),
email2.70x(1237.17us/0B). Python's closed Unicode Split pilot remains3.55x;
early literal2.89x, limited Split2.84x, date miss2.76x and one-capture FindAll2.35x
remain gaps. PCRE2 date hit/miss/subroutine4.32/4.30/4.29x retain their separate scope.
Closed experiments and post-performance-release POSIX are not reopened by this run.

For the captured nine-byte IsMatch case, unlimited ->finite/live medians are
Core0.633 ->0.772us(3552 ->3664B), Python0.901 ->1.117us(5744 ->5856B),
PCRE2 1.060 ->1.169us(5744 ->5856B). These compare each profile's same operation:
Core.IsMatch, Python.Search(...).Success(value), PCRE2.IsMatch. Core/Python bound
work/depth/working/result; the PCRE2 lane sets MatchLimit. They do not imply identical
Boolean costs, all PCRE2 controls, universal overhead or total-heap limits.
Raw samples for all resource cases remain in the Full archive.

Immutable evidence in artifacts/experiments/delivery-dev26-20261104/:

- windows-evidence.zip: `7A11D6B19059D5E1249B0D86D56CE6CE91830B4AB3D5365B349E05EC0EA1A152`.
- windows-restore-inputs.zip: `59FB8A27285AEB77DFB8D658AACC9EA19A626404C6FD9C7199D81A1EF0E75A63`.
- original compiler inputs: `5B8BC6939E008B1EBE32A0827330FF17F215DF6CE81D37A0D395BD64DDDA2B61`.
- linux-evidence.tar.gz: `B2C39C87199A437CA42DEE51A8195E96EE63BA0DE3DA0B5DA58DB295D5BB4448`.
- full-evidence.tar.gz: `2E55ADE7CA0B93FD58556882B839CAEBED7613295703C01A32999E352F3271FF`.
- post-refresh-guards.zip: `730D38733D6241B9949475ED2AF5B8203BFB1F9A9F5B426B886EACD07FA0C770`.
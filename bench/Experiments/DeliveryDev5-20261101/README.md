# 0.5.0-dev.5 qualification stopped before delivery

Frozen7c117bfbbc1e79f165ecdb43af5ba98871293e89, all3profiles/exactCorepins,
net10/net11RC3. Six package/symbol files were built once under attempt142c13045e76.
No candidate was qualified or delivered to artifacts/nuget/; these attempt bytes
remain preserved and are superseded by a new version.

Windows detached Core net10 passed41128; net11 passed41127/failed1.
DenseFiniteValuesDoNotAcquirePerResultScratch(compiled:false) measured7200B
against4096B. The unchanged packaged test passes both cases in isolation. Its
fixture was missing the existing exclusive Utf8RuntimeAllocationCollection guard;
the repair adds that annotation and preserves the4096B assertion, warmup and
semantic/resource checks. All25fixture checks pass per runtime after repair.
This supports correcting test coordination; it does not identify the source
of every extra allocation or qualify the failed candidate. Production engine
code is unchanged by that repair.

The independent Linux refresh passed build22.56s, both-runtime source tests
147.76s and disabled-dynamic8.09/8.51s, then entered Core sampling. After the
Windows qualification failure, its confirmed-owned process group was stopped
intentionally. Remote exit143, both launcher PIDs absent, no benchmark child
remained. Partial results were sealed before restoring its two generated Core
JSON files. This is not a completed refresh or a60minute proof. Frozen7c source
CI41321863446 is green on Windows/Linux; green source CI did not prevent the
detached allocation-test failure.

Immutable evidence under `artifacts/experiments/delivery-dev5-20261101-superseded/`:

- `partial-evidence.tar.gz`: original source, exact Linux binaries/PDBs, partial
  snapshots, refresh logs/manifest, declaration and explicit stop reason.
  SHA256 `CC084F65C64332BDB4C73BEADAFD71C3602E6B6D35810FFE2413C5D9B83D5175`.
- `windows-failure.zip`: exact six failed-attempt feed files, provenance/steps/
  transcript, original failed/passed TRXs and test binaries/PDBs/source, isolated
  same-byte result and fixed-fixture result, all463 original Windows compilation
  documents including generated inputs. SHA256
  `3DA0332BA379759F2EC9C4D42320019D271BCC531E17AC7BF93E1124A12A882A`.

Use a fresh common version for corrected qualification. Keep the accepted PCRE2
performance batch, fixed-original cumulative allowance and original consumer
checks. No partial snapshot import, publication or repeated token variants.

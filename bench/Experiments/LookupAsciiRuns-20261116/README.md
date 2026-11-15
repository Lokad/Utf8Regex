# Skip proved ASCII runs inside lookup validation

Accepted: after one complete ASCII vector with no incoming UTF8 continuation,
use checked span range search for the next non-ASCII byte. Resume the existing
256/512-bit lookup there; retain scalar tails and exact first-error authority.
Eleven production lines, no fields, selector, density state, allocation or mode.
Governed validation still admits/checks the same <=2048-byte blocks and original
work/cancellation/deadline authority. Metadata validation and all public contracts
remain unchanged. The old [ASCII-run trial](../AsciiRunSearch-20261112/README.md)
affected a superseded validator and stays removed; this changes the current
lookup's repeated ASCII-vector work only.

Windows and SAME-byte Linux .NET10/11 each pass focused Core1011, PythonRe892,
PCRE2396 and POSIX50 Release cases, reusing matching adapter hosts. Existing
exhaustive byte-pair/vector-edge, scalar-width/tail/offset, first-error, profile,
resource and interruption coverage remains. The new test crosses ASCII values,
borrowed offsets, incoming Unicode, arbitrary re-entry positions and malformed
tails. Linux executes both hardware widths; a scoped no-intrinsics public
preflight also passes. Only Core and its changed test host build.

One94.044s public ordinary-process ABBA,180s cap,0 confirmations: twelve operations,
normal8CPU startup then CPU2, five150ms-target samples per process/row, >=20ms
floor. Same bytes/options/complete Count, IsMatch and Match coordinates; results
consumed after timing. Whole-subject Validate is timed unless labelled trusted.
Finite rows keep the same work/depth/working/result policy and live original token.

| Public operation | Before µs | After µs | Change | Sample ranges |
|---|---:|---:|---:|---|
| Actual6.8MB email Count | 934.264 | 935.443 | +0.13% | overlapping |
| Email, finite/live | 1258.380 | 1222.184 | -2.88% | separated |
| ASCII islands after Unicode | 15.343 | 13.217 | -13.85% | separated |
| Same, finite/live | 13.242 | 12.152 | -8.23% | separated |
| Unicode then1MiB ASCII, Count | 57.779 | 46.605 | -19.34% | separated |
| Same, IsMatch | 30.039 | 19.587 | -34.79% | separated |
| Same, value Match | 34.501 | 34.221 | -0.81% | overlapping |
| Chinese literal | 70.967 | 71.107 | +0.20% | overlapping |
| Dense Unicode control | 16.932 | 17.043 | +0.65% | overlapping |
| Whole-ASCII control | 38.899 | 39.516 | +1.59% | overlapping |
| Trusted email control | 278.598 | 274.637 | -1.42% | overlapping |
| Predecoded .NET email control | 460.128 | 454.428 | -1.24% | overlapping |

Keep the small change for the demonstrated long-ASCII input shapes. Ordinary
email does not improve; its main canonical gap remains open. Do not claim a
general validator gain or infer changed-helper engagement/speedup from the value
Match observation. All allocation medians agree:0B ordinarily,112B finite.
Controls/uncertainty and adverse observations remain; no normalization, broad
parity claim, canonical table refresh or further tuning campaign.

Baseline is qualified dev.99 freeze dc1f428c; after is773085c9 plus the patch.
Actual DLL/PDB and all343 compiler documents verify; the changing producer hashes
differ, caller/.NET identities remain identical, managed-member inventory permits
actual Unsafe.SizeOf only. No producer build on the VM. SHA256:

- Before Core `393D19EDC14BBE4A7D91970764F5A9928EE624BAF893A7A6F5B18F82D7AA925C`.
- After Core `0EA0AC17E582CF9110C55E6EDF36744F53FBFCB9052055F952A28214663DF4E2`.
- Raw `artifacts/experiments/lookup-ascii-runs-20261116/evidence.tar.gz`:
  `A8EC883A48560FDEAB93FD7805C87E0B1D081AFBCA26A7066A0DD882B2D84AC0`.
- Validation archive: `09DBA485122527DC9AB3B31FABF02C94409161FA5900851A0DE436A2D91B6CC6`.

Original worker/collector handles terminal0; raw verified offVM before keeper
release. Owned VM task retired after accepted archive hash and zero live process/
cwd/FD/map references. Qualified dev.99 assets rehash unchanged;660 feed files
remain. No package/Full qualification, publication or adoption for this change.

# dev.7: correctness qualified, cumulative performance rejected

All three0.5.0-dev.7 packages/exact Core pins are staged, **not published**.
Frozen source `2ff58324cb10f04e27699eb01df1d13fadd71c68`, .NET10/11RC3. Packages and
symbols in `artifacts/nuget/` and records in `artifacts/package-candidates/0.5.0-dev.7/`
remain immutable, as do prior published/staged bytes. This includes the accepted
ASCII-start union, exact-class fusion and omitted unused repeat journals.

Windows package qualification passed269.650s with two graph builds/one pack per
profile. Linux passed the detached checks against the exact Windows bytes.
Original Awk19/Lython118checks pass per runtime/platform, with the native/funded
producer probes and final root loans0. Six assets and24Linux feed/cache/lock/
PDB/SourceLink audits match. All463original Windows compilation documents,
including generated sources, are captured. Only three copied consumer pins
changed; downstream repositories were untouched.

The [fixed-original gate](cumulative-acceptance.json) completed all79public
workloads in383.380timed seconds with valid output/duration guards. It **fails**
the cumulative10% execution-time allowance against frozen `a40bda03`:

| Public operation | Change |
| --- | ---: |
| Captured FindAll strings, ordinary |+58.01%|
| Captured FindAll, finite/live |+53.31%|
| Captured FindAll, deadline/live |+88.80%|
| Captured FindAll, Unicode/byte offset |+55.41%/+60.69%|
| Core grouped Unicode replacement |+16.28%|
| Python captured Count, finite/live |+15.02%|
| Variable/lazy FullMatch controls |+13.11%/+10.24%|

Spreads on the FindAll rows are about1.01–1.03x. No confirmation ran: these large
stable regressions make rejection unambiguous. Parent-trial gains remain evidence
for their scoped changes; they do not establish cumulative acceptance. Source
attribution and repair are required. **The full catalog refresh is held.**
Existing benchmark JSONs/Status remain unchanged; no60minute claim for this source.

The batch reuses the original26cross-profile/17PCREdate lanes plus14union,
16repeat/control and6actual captured-FindAll cases. Typed public calls in isolated
contexts, warmed independently, five alternating samples and complete result
consumption after timing. Finite/deadline/allocations and unchanged controls
remain. The new drivers retain their predecoded/decode-then .NET lanes.
Unlike comparators and new APIs without an old counterpart keep separate scope.

Raw parts, source/binary/PDB identities and drivers are sealed under
`artifacts/experiments/delivery-dev7-20261101/`:

- `cumulative-2ff58324-rejected.tar.gz`, SHA256 `05C4E099A99C02F868245D7441A666F5B760E5BFE0CF5B8F8147AED178C29D03`.
- `linux-consumers.tar.gz`, SHA256 `FF76756A314F0EB7A30FAD846DB6A3EFDF179D0B39BA48BC72D57C1C94C5A65C`.
- `windows-consumers.zip`, SHA256 `D73979A059F9606E7ADCE9B0C30E0E3E7A6FC13E1520FD51D4911A83E4D38DFB`.
- `windows-compilation-inputs.zip`, SHA256 `B73021790F0DC74C6F85C11A56B147CD332C9B9C1DD04A2B42AB6AC8763BA735`.
- `producer-source.tar.gz`, SHA256 `F351D03071EF565A371839C612C81F768BD8A9B972C75D55554CB20E8742EC5D`.

The first compilation-source capture raced the qualifier moving its completed
attempt directory. Only the capture was corrected from the final receipt; no
package or test reran. [Windows qualification](windows-qualification.json),
[Linux packages](linux-packages.json), [original consumers](linux-consumers.json),
and [identity audit](linux-identity-audit.json). Package correctness, downstream
compatibility, operating cost and publication remain separate acceptance steps.

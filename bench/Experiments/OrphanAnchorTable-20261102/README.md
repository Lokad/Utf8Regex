# Remove the unused emitted anchor-prefix table

`AnchorPrefixesByByte` had no execution reader. Remove its 256-reference array,
property, constructor parameter and preparation loop, plus the test assertion
that preserved its identity. The existing prefix verifier and prepared byte set
remain. No search algorithm, public API, scalar/capture semantics or lifetime
contract changes.

Affected public constructions allocate **2,080 fewer bytes** on the x64 VM.
Construction/program charges decrease with the deleted storage and work; major
buffer admission, selector expansion/depth and interruption checks remain. The
existing remaining-byte and every-work-cutoff rollback tests still exercise the
production lowerer with the actual remaining buffers. No manual size estimate
replaces `Unsafe.SizeOf<T>()`.

One .NET11 RC3 quiet-VM/CPU2 comparison covers eight public constructors and three
warm public Counts. The frozen baseline is source `2c6cb298`, code-equivalent to
the data-only parent `a7a6a005`; the candidate is that parent plus the archived
two-file runtime patch. Public constructor delegates avoid reflection/boxing in
the timed loop. Construction excludes first-use matching/JIT. Complete batches
are checked after timing. Independent calibration, five alternating samples and
thread allocations are retained.

The first batch took **16.143s**. Its first three construction cases were still
warming: baseline compiled construction fell from 315 to 179us, and candidate
compiled construction from 892 to 688us. These are not steady-state regression
evidence. The sole selected confirmation took **26.685s**, changing only warmup to
at least 2s and 2,048 calls for those three cases and the unchanged literal
constructor control. Same frozen producer binaries; no variant or third run.
The original data and drift remain archived. Other cases retain their first-run
evidence. Every selected sample is at least 39.851ms.

| Public operation | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Construct keyword, ordinary |112.37|112.38|+0.00%|
| Construct keyword, compiled |113.67|112.84|-0.72%|
| Construct two-prefix family |100.30|101.26|+0.96%|
| Construct four-prefix family |118.17|116.87|-1.10%|
| Construct zero-offset family |93.59|93.67|+0.09%|
| Construct finite-policy control |64.10|63.92|-0.27%|
| Construct shared-prefix control |52.12|52.54|+0.80%|
| Construct literal control |32.85|32.83|-0.05%|
| Keyword corpus Count |77.48|75.55|-2.50%|
| Shared-prefix Count control |21.27|21.77|+2.32%|
| Literal Count control |6.46|6.45|-0.11%|

**Promote.** Largest adverse selected median is +2.32%; Count allocations and all
unaffected construction allocations stay unchanged. This deletion's benefit is
less storage/preparation, not a claimed matching speedup. The literal control's
confirmation includes a wide outlier; it is preserved, not resampled. This is a
parent comparison, not a cumulative fixed-original gate or a catalog refresh.

Focused Release construction, anchor, semantic, cache/funding, structural cursor
and work-policy checks pass on .NET10/11 (566 + 1,120 per runtime). No unchanged
solution/platform/package matrix was repeated. Immutable dev.9 packages and their
qualification remain at source `2c6cb298`; this cleanup is for the next candidate.

Raw binaries/PDBs, both drivers, inputs, patch, all initial/confirmation timings,
predeclarations and build logs are preserved under
`artifacts/experiments/orphan-anchor-table-20261102/`:

- `evidence.tar.gz`: SHA256 `CEBCA15FE5D0339A9121B0F028D189879B5288B4A5B5B726AF20B3D6CF3228FE`.
- `correctness-source.zip`: SHA256 `3FE9F1F918679CCEBE2D14A1D945C6F82CC4918B5AE133CEB558C49A2C5F3212`.
- Runtime patch: SHA256 `6658A09BD5EFE7221C665C20140E1E52FAAA9658F4DCE27F3A10C611CEBC2FAA`.

See `summary.json` for the selected observations and exact binary identities.
Return to the remaining public parity gaps; batch full delivery qualification
after meaningful runtime progress.

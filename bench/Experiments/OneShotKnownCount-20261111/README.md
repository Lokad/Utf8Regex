# Reuse known counts in one-shot Python text

Accepted. Two read-only properties forward the scalar cursor's existing nullable
input description. One-shot string output reuses ASCII byte lengths or the known
whole-subject UTF-16 length. Unknown trusted ranges still use bounded counting.
No metadata is derived by this lookup; no field, cache, output/VM mode, public
API or ownership/control mechanism was added. Batch paths and Core are unchanged.

This closes the duplicate-count cost found in [one-shot text sizing](../OneShotText-20261111/README.md).
The combined implementation is compared against its **frozen original**
56de0556ff81dffb25d2d6d3b49ff126ce35c9f6 producer graph, not just its immediate
predecessor. No additive composition of earlier percentages.

One 78.939-second quiet-VM .NET 11 ordinary-process ABBA under both policies,
nine public lanes, 360 samples >=51.735 ms, 75 ms calibration target with one
calibration confirmation, 100-second cap. Explicit distinct PythonRe DLL/PDBs;
identical Core/Pcre2/caller assets, with unchanged Core/Pcre2 source verified.
Eight processors visible at startup, then CPU2 affinity. One sequential worker
requires 1 GiB available RAM; results/checksums are consumed outside timing.

| Public operation | Validate before -> after us | AssumeValid before -> after us |
|---|---:|---:|
| small text, 1 MiB ASCII suffix |19.953 ->20.100 (+0.74%)|10.772 ->0.286 (-97.35%)|
| small text, 1 MiB BMP suffix |794.746 ->867.302 (+9.13%)|205.775 ->0.284 (-99.86%)|
| small text, 1 MiB astral suffix |2552.826 ->2560.270 (+0.29%)|1084.772 ->0.279 (-99.97%)|
| finite/live early ASCII text |26.658 ->27.187 (+1.99%)|14.233 ->0.395 (-97.23%)|
| full 64 KiB ASCII scalar text |275.422 ->280.888 (+1.98%)|279.062 ->276.052 (-1.08%)|
| full 64 KiB BMP scalar text |208.607 ->213.087 (+2.15%)|190.552 ->184.680 (-3.08%)|
| full 64 KiB astral scalar text |313.523 ->308.700 (-1.54%)|217.860 ->224.298 (+2.96%)|
| unchanged many-capture strings |726.611 ->735.944 (+1.28%)|742.825 ->714.297 (-3.84%)|

Only the four trusted early-text ranges are disjoint. The prior strict astral
+23.18% observation is not repeated; source also removes the duplicate count.
The full-output differences remain inconclusive, rather than proving a universal
exact restoration. Unchanged Core control -0.18/+8.41% overlaps. All measured
allocations and 368-byte state size are unchanged. Keep every adverse observation
and range; no normalization, extra confirmation, tuning or native-parity claim.
The trusted early 128-work probe still completes; strict and original refuse.

Windows/Linux 557 focused Release checks per .NET 10/11 pass. Existing tests cover
whole/interior scalar ranges, empty strings, exact string admission, both input
policies, original cancellation/deadlines, owned output and program-funded refunds.
The large-range test now explicitly exercises whole-subject cached counts too.
The specification distinguishes reused facts from unknown-range counting.
No package/catalog matrix or mutation of accepted dev.67 artifacts.

Evidence copied off VM and hash verified under
`artifacts/experiments/one-shot-known-count-20261111/`:

- `evidence.tar.gz`: SHA256 42913DFC9849FB7A6D7D789CDCE120FA34468629D307F432B6ADAB3A50B87050.
- `linux-checks.tar.gz`: SHA256 B4641171774F8AA27AA63208C10E3DA71DE09503B502B27E8BF384F80C287B84.

The full summary retains source/DLL/PDB/caller hashes, process medians, ranges,
allocations and checksums. Candidate source: fa1994ac plus the archived three-file
patch. This comparison closes the scoped one-shot correction, while warm Python
capture/findall parity remains a separate roadmap item.

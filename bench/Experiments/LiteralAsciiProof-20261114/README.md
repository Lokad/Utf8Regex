# Reuse literal ASCII validity proof

Accepted Core source `09ce19dd`, before `804ff869`. Strict Count already checks
whether an eligible subject is ASCII. Reuse that complete proof to return zero
for an exact literal starting with a non-ASCII byte. A trusted-text call omits
this validity-only ASCII scan. No new scanner, descriptor, SIMD engine or mode.
Literals starting with ASCII retain their original strict search. Finite limits,
live tokens, deadlines and scalar fallback selectors precede this branch.

One public VM A/B, no confirmation. Separate ordinary producers with distinct
staged Core DLL/PDBs, source checks and identical caller/input/options/full Count
outputs; .NET11GA, internal affinity, 1s warmup/lane,9 samples/250ms target,
237ms minimum observed. Both policies receive valid unchanged UTF8. Strict
whole-subject checking is timed; trusted Count trusts that text. Compare each
policy with its own baseline. ASCII-literal and finite/live cases are unchanged
public-route controls; the other trusted lanes are affected operations.

| Public Count | Strict before → after us | Trusted before → after us |
|---|---:|---:|
| unicode-lead-ascii-miss | 28.231 → 7.732 (-72.61%) | 28.303 → 20.515 (-27.51%) |
| unicode-tail-ascii-miss | 25.925 → 23.990 (-7.47%) | 25.928 → 16.689 (-35.63%) |
| chinese | 67.420 → 67.556 (+0.20%) | 21.677 → 21.608 (-0.32%) |
| dense-mixed | 57.978 → 58.464 (+0.84%) | 49.892 → 50.263 (+0.74%) |
| unaffected-ascii-literal | 28.104 → 27.851 (-0.90%) | 28.095 → 28.035 (-0.21%) |
| unaffected-finite-live | 34.817 → 34.632 (-0.53%) | 24.146 → 23.807 (-1.40%) |

Accept the large ASCII miss improvement. Retain dense +0.84% strict/+0.74%
trusted, Chinese strict +0.20%, all sample ranges and shifted controls. No
normalization, new .NET/CPython parity ratio or claim that the mixed-width corpus
gap is resolved. Measured allocations are unchanged:0B except112B finite/live.
The Chinese corpus strict call remains about67.6us in this context.

Focused Release checks on Windows/Linux, each .NET10/11:494 Core checks plus237
PythonRe checks. Includes selected borrowed boundaries/tails, literal/family
fallbacks, strict malformed-suffix errors, policy equivalence, original tokens,
deadlines/work admission, managed API inventory and Python Count delegation.
Python-only followup `30a36fe7` changes no producer source; Linux checks preserve
both measured Core target DLL/PDB hashes. No contract/default/API changes.

Raw verified offVM before keeper35240 closed terminal0; Python keeper also
terminal0 after verified collection. SHA256 `534A9EA0D57591EB7C26DF0680D1D69554B36204F9DA79E4F707B04A4552166E`; Python
`49392A58FBC2014AD76AD12DFA51A461B7225EF7F8CC91E89C9935FC73EF6B7A`. Local raw lives under
`artifacts/experiments/core-ascii-proof-count-20261114/`.
[Acceptance](acceptance.json) retains medians, ranges and producer identities.

This change is after immutable dev.85; no package, Full run or canonical catalog
is regenerated. All540 existing package/symbol hashes are preserved. The prior
mixed-width SIMD campaign remains closed and removed; this reuses an existing
proof independently.

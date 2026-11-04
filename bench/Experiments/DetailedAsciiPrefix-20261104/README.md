# Detailed capture coordinates in an ASCII prefix

**Promoted with local costs.** After owned scalar matching and complete strict
UTF-8 validation, detailed projection checks every capture-history endpoint,
including captures beyond the whole match. If the entire required prefix is
ASCII, it reuses the existing identity boundary map. Unicode prefixes keep the
existing full map; callers with an already admitted UTF-16 offset map bypass the
proof. The returned context still borrows the full input. No new representation,
owner, matcher, mode, cached state or unchecked API was added.

The proof checks interruption/work every256 groups/captures and at most2048
prefix bytes. Capture/result/working admission remains before allocation. Actual
map-array charges disappear for the proved case, so finite policies can admit
previously denied calls. Aliases and all capture histories retain their original
absolute byte/UTF-16 coordinates. Failed proofs can add work and time.

Read-only public inspection on exact source `835ba6a2`, both runtimes/platforms,
established the live cost. Linux's actual imports subject is46429B/SHA507F5A33,
first Unicode5831, whole match65..91 and **all** capture endpoints<=91. Detailed
allocation was196360B and an8KiB result cap denied it. Windows preserves CRLF:
47858B/SHA32957950, Unicode5991/end93,202248B. These untimed observations establish
coverage and allocation, not speedup; their archive is `B878F3E2`.

One **79.2063704s**,15-case public comparison against accepted `835ba6a2` Core
plus six byte-identical frozen dev.27 Full companion DLL/PDBs; only Core changes.
The source overlay is `EC8E56E4` (two production files and one new public test).
Normal8CPU startup thenCPU2, managed1.5s/min128 and diagnostic250ms/min64 warmup,
independent60ms calibration and five alternating samples. **150 owned samples,
minimum47.1334ms**. Typed outputs are consumed after timing. Detailed operations
include record construction and coordinate consumption, without extra group-string
extraction. All histories are checked separately by correctness tests. BCL
same-options lanes are equal-result coordinate diagnostics on these overlapping
subjects. [Observations](cost-summary.json) retain identities, allocations and spreads.

| Public MatchDetailed | Before us | After us | Change | B/op before -> after |
| --- | ---: | ---: | ---: | ---: |
| Actual imports |158.73|3.09|-98.05%|196360 ->4848|
| Actual imports compiled |172.91|2.65|-98.47%|196360 ->4848|
| Actual imports live token |179.12|3.78|-97.89%|196472 ->4960|
| Actual imports finite/live |184.43|3.91|-97.88%|196472 ->4960|
| Actual imports deadline/live |171.24|4.32|-97.48%|196520 ->5008|
| Tiny all-ASCII |0.86|0.86|+0.23%|4592 ->4592|
| Early ASCII before Unicode |207.71|2.77|-98.67%|275016 ->4592|
| Unicode before capture |281.20|397.08|+41.21%|275016 ->275016|
| Late capture after Unicode prefix |233.95|378.12|+61.63%|275008 ->275008|
| Far earlier Unicode capture history |1379.01|1427.66|+3.53%|1495408 ->1495408|
| Empty capture before Unicode |139.50|1.50|-98.92%|139768 ->4528|
| RTL full map |142.13|200.43|+41.02%|141456 ->141456|

The Unicode fallback penalties are measured public costs, not isolated-kernel
estimates. Their spreads are1.02–1.04; no specific cause is established beyond
this source change. Successful-prefix after samples spread1.22–1.62, so the
small absolute estimates are uncertain, but the order-of-magnitude benefit and
eliminated arrays are clear. Local costs are accepted for these substantial gains
under the user's policy; the completed checked-access10% allowance is not a
universal delivery gate. No confirmation, tuned variant or repeat campaign.
Unchanged native Count -0.39%, Python Count +1.38%, PCRE Count -1.18%; no causal
credit or catalog/parity claim is assigned to these controls.

Windows/Linux .NET10/11 each pass1197 focused Core checks, Python176 and PCRE165.
Coverage includes all capture histories, repeated/empty/lookaround captures,
Unicode before or beyond the whole match, slices/offsets, RTL/NonBacktracking,
retained aliases, finite/funded admission, malformed full tails and synchronous
original-token/deadline interruption after matching. Existing assertion, atomic,
balancing and capture-dependency regressions are included. Public timeout checks
expect RegexMatchTimeoutException; cancellation checks preserve token identity.

Immutable `artifacts/experiments/detailed-ascii-prefix-20261104/`:

- evidence.tar.gz: `8A63FD702173C46965ED9522B2FC7AABCA14CC661AB3B4C9FE6B8D325D14A683`.
- windows-correctness.zip: `CFA55F8726D5F16615C3FEC1ED57F4810E6BC04C74BBA4D0F7470EA6F77D303C`.
- Earlier inspection: `B878F3E2362C964EEDA56B7F620A0E35FDBFFC91F21F9C162FA888823EC8A799`.

Immutable staged dev.27 packages and44m28.60s Full tables still describe
`11a26e26`. This later public pilot is separate evidence; full package/platform
matrices remain at the next coherent delivery boundary. No package publication.

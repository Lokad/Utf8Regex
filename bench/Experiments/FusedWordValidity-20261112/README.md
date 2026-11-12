# Word-delimited Count with fused validity — rejected

Baseline: accepted source20174326, restored ec77c25b compilation, metadata5cce9347.
The trial kept the existing Unicode word-delimited matching algorithm and used
one local validated-prefix offset: combined ASCII/delimiter discovery, existing
validation on wider runs, and already-decoded body bytes. No owner, buffer,
public token, alternate engine or unchecked API. The trial and its tests are
fully removed; **no contract or shipped implementation changes**.

## Bounded public decision

ONE98.714s ordinary-process ABBA comparison,14 rows/both input policies/560samples,
minimum71.304ms, maximum.032MiB per timed batch;140s outer cap. .NET11.0.0 on
Linux, normal8-CPU startup thenCPU2, default tiering/PGO. Distinct loaded Core
DLL/PDBs, identical caller/Python/.NET controls, complete results and loan refunds
verify. These are public Count costs, not kernel timings or catalog updates.

| Public Count | Validate before -> after us | AssumeValid before -> after us |
|---|---:|---:|
| Actual email corpus |994.421 ->7567.860|268.698 ->276.355|
| Finite/live email |1380.157 ->7775.285|288.103 ->296.447|
| Dense email |41.426 ->56.124|41.254 ->49.019|
| Unicode email |141.547 ->187.907|110.376 ->127.730|
| Funded/deadline email control |1474.239 ->1525.563|362.908 ->368.083|
| Unchanged predecoded .NET email |444.817 ->441.785|444.155 ->444.166|

The first four strict rows regress661.03%,463.36%,35.48%,32.75%, with disjoint
sample ranges. Trusted dense/Unicode regress18.82/15.72% disjoint; trusted actual
and finite email regress2.85/2.90% disjoint. Reject without a confirmation or a
tuned follow-up. Funded calls validate before this helper, so that row is an
unchanged-route control, not evidence for fused validation. It moves+3.48% strict
disjoint/+1.43% trusted overlapping. Other unchanged controls retain movement:
Russian+9.45/+8.11% disjoint, URI+6.20% strict disjoint, Chinese+4.61% strict
disjoint, tiny literal-10.69% strict disjoint; others overlap. No normalization or
causal attribution to unchanged methods. Full28-row observations remain in raw
summary.json.

Allocations remain0B/ordinary,112B/finite-live,160B/funded-deadline. Modeled peak
stays17B held by the caller; final loans0. These are admission observations,
not heap/RSS guarantees. No package/Full refresh or performance Status change.

## Validation and restoration

Candidate Windows/Linux Release checks pass per.NET10/.NET11: Core3583,
PythonRe681,Pcre2874. Independent NonBacktracking results, malformed first-error
offsets at ASCII/wide/block fences, retry floors, scalar words/astral letters,
policy authority, every work cutoff, zero-byte admission, sticky denial and
original cancellation/deadlines pass. Existing head/body interruption fixtures
were corrected during the trial; those trial edits are also removed.
Both342-document Core PDB/PE inventories differ only at the two intended
production files and generated revision. Reuse unchanged adapter/test binaries;
build only affected Core/Core tests. Initial test-only null/default build error
and the first passing checks precede the final boundary additions; preserve them.

Restore the accepted source and remove all trial-only tests. One restored affected
build and focused checks perOS/TFM pass: Core1563,PythonRe371,Pcre2209. VM source is
clean. All476 immutable package/symbol assets stay unchanged. No source CI rerun
is needed for this receipt-only decision; implementation20174326 remains green.

Canonical artifacts/experiments/fused-word-validity-20261112/:

- Gate4FFBC599881ACF721B4341098AFFF51E8A27D175160D028CC17EDA104ABDE76C.
- PublicE98816F887E85FAACF98824ABEC892AC147AAC6E85C05A7AB2A3D4847599339A.
- Windows98E1F892D32EE316291AFCD88D3367A9BCB3F1BA4124EA6ABA7C479AB1C2CF41.
- Restored Linux9024E265C125356DF12372DAFD167386FB7361B103945BFDA8259A858F151273.
- Restored WindowsDC8B98DCD23381E1343733E7FE07D8BF78FAD3130D8183606775EE6B9178C960.

Archives verify offVM before keeper release. Current Core/Python gaps and general
POSIX histories remain active; this closes this fusion proposal only.

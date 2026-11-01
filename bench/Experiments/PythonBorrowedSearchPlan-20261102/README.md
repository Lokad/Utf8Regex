# Borrow the admitted search plan during Python discovery

One production line now borrows the prepared object's existing readonly search
plan. The old inspection/property chain returned the entire plan by value on
each candidate search. The local reference cannot escape; the cursor retains
the hint/program use and the underlying field is immutable. No getter, API,
field, policy, accounting, algorithm, unchecked access or allocation was added.

The [actual SizeOf opcode](size.json) reports2720bytes. Controlled public warmup
and [Release Tier1 inspection](codegen-summary.json) confirm the copy was live:
SeekCandidate shrinks4186→578code bytes and stack reservation3464→136bytes;
its bulk write-barrier copy disappears. Core DLL/PDB bytes are identical between
lanes. Codegen was inspected before the public comparison; metadata alone was
not treated as performance evidence.

One102.064-second comparison,16existing workloads, dedicated.NET11RC3 VM/CPU2.
Frozen `ca9b7769` versus its archived one-line patch, both BCL equal-result lanes,
and fixed `a40bda03` as a fifth lane on the original six FindAll cases in this same
batch. Typed public calls, warm1s/min1000, independent60ms calibration,
five alternating samples≥39.978ms; complete outputs consumed after timing.

| Public FindAllToStrings | Parent us | Candidate us | Change |
| --- | ---: | ---: | ---: |
| Dense captured values |596.204|444.701|−25.41%|
| Finite limits/live token |766.918|627.947|−18.12%|
| Deadline/live token |928.271|819.404|−11.73%|
| Mixed Unicode |325.166|247.518|−23.88%|
| Nonzero byte offset |628.260|464.763|−26.02%|

[All parent costs, allocations, spreads and pairs](summary.json). Allocations are
unchanged in every case. Largest adverse control median3.70%, pair3.89%; maximum
candidate spread1.042x. The old large prologue also affected the method's owned
leading-byte branch, explaining why an ineligible long-prefix hint can benefit.
Native-result/literal routes remain controls. No confirmation or variants.

The [direct fixed-original six](original-summary.json) now meet the10% gate:
ordinary−6.20%, finite−0.83%, deadline+0.39%, Unicode−6.44%, miss−2.10%, offset−6.96%.
That does **not** qualify the other original rows or the coherent79-workload
release gate. No new package, catalog or Status qualification is claimed.

Full PythonRe4530 Release checks pass per runtime on.NET10/11, including existing
captures, retained aliases, funded admission, cancellation/deadline and limit
cutoffs. No mirrored test or Core/global/platform/package matrix was added.
Metering and resource/lifetime contracts are unchanged. The unrelated closed20B
VM-instruction borrow and token-metadata experiments were not reopened.

Immutable artifacts under `artifacts/experiments/python-borrowed-search-plan-20261102/`:

- `evidence.tar.gz`: SHA256 `92832183C63981DA832EA80401AE0554DE93D87E0624610AFC8953D4E862CE6D`.
- `correctness-codegen.zip`: SHA256 `E83B0AD9361D018E4BDA9F6880B99ADDC07EC0FCFCEAFF1E3860182F6E660EC5`.

They retain all three producers' DLLs/PDBs, exact loaded identities, source/applied
patch, driver, preflight, predeclaration, raw samples, codegen and correctness logs.

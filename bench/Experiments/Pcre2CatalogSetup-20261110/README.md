# Select PCRE2 benchmark metadata without compiling the full catalog

`de6ecc95` replaces eager catalog compilation with deterministic case metadata.
One-case lookup returns its curated/selected imported definition directly; the
full listing is lazy. The existing context compiles that selected engine once.
The 32 imported public-operation cases all compiled before this change; the
discarded compilation pass excluded **zero** cases. All66 complete identities
(patterns, inputs, options, operations, replacements/backends) remain identical.
Imported Core workloads remain explicitly managed-compatible adapter controls,
where PCRE2 dispatch/owned execution and adapter costs differ from a Core call;
they are not additional PCRE2 syntax coverage. No selection-based pruning here.

An untimed probe installs the existing construction checkpoint failure while
building/listing metadata and looking up curated/imported cases: no compilation
occurs. Unsupported public operations fail explicitly. Creating the selected
context still propagates the original injected engine failure and a real bounded
construction refusal. Literal and imported-email public inspection smokes pass.
Release benchmark build passes. Shipped libraries/APIs and warm measurements
are unchanged; no package or profile matrix is replayed.

One local Windows cold CLI observation, before `582c229e` versus its exact
catalog-source patch (then committed asde6ecc95), followed by the single allowed
noise confirmation. Ordinary .NET11 processes; includes startup/inspection/output
and is **not matching throughput, a VM refresh result, or a parity Status**.

| Observation | Before PCRE2 ms | After PCRE2 ms | Before unchanged Core ms | After unchanged Core ms |
|---|---:|---:|---:|---:|
| Initial |541.010|126.694|251.011|878.155|
| Confirmation, process1 |427.060|136.393|310.990|130.071|
| Confirmation, process2 |413.257|135.161|128.298|128.731|

PCRE2 command is`--inspect-pcre2-case literal/early`; control is
`--inspect-pattern needle`. Keep the startup variation, especially the initial
unchanged control; do not normalize or claim a general startup percentage.
Confirmation uses ABBA producers and15s caps on each owned child. Timing check
is closed. The direct invariant is stronger: a single-case process no longer
constructs32 unrelated engines or loads unrelated imported input definitions.

Exact binary/PDB hashes, identity digest, case IDs and individual observations:
[summary.json](summary.json). Raw source patch, frozen changed DLL/PDBs, identities,
probe and command outputs:`artifacts/experiments/pcre-catalog-20261110/evidence.tar.gz`,
SHA256`BC2A791F108277D2FDD8BF0FB9936955909788804E69CC323C723876BDB995CF`.

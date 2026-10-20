# Sixth review: profile boundaries and unnecessary retained work

Development source **0.4.0-rc.2-dev**, final source `1609aa03`. PLAN.md was
rewritten before implementation around six bounded deliverables. Earlier plans
and the former checked-access ledger are archived in ignored PLAN.HISTORY.md;
versioned historical evidence and immutable artifacts remain available.

- PCRE2 construction requires an owned profile parse. The unrestricted Core
  grammar fallback, direct executor and its execution/projection branches are
  deleted. Undocumented .NET escape/property/balancing forms now refuse; valid
  escaped punctuation is handled by the owned parser. Resource tests use valid
  PCRE2 syntax and physical capture numbering. Shared Core fast paths retain
  their existing parse and eligibility proofs.
- Character compilation uses the existing admitted token/class/property buffers,
  token instruction capacity, growth/final-copy overlap and bounded interruption.
  Single-token parsing borrows the original backtracking construction owner.
  Character programs omit unused Core preparation. The reviewed 1,000-token
  instruction-cap bypass is closed; deleting storage also means the text form
  can legitimately fit 32 KiB, while an independent 16 KiB byte cap refuses.
  Accounting covers modeled major capacity, not each helper, all heap or RSS.
- Character syntax-node vectors and token offset mirrors are gone. Literal Rune
  staging is temporary rather than retained with duplicate anchor metadata.
  Runtime tokens/UTF-8 bytes and live backtracking Probe trees remain. Core name
  copies are omitted where native PCRE2 metadata is authoritative.
- [Benchmark preflight](loaded-sources.json) relates loaded runner/Core/PythonRe
  producer revisions to relevant source/build files. Matching code and later
  documentation-only differences pass; actual stale product DLLs and stale,
  missing, malformed or unresolvable producers refuse. The five original handoffs
  still pass the source gate; unknown untracked source is detected. No additional
  fingerprint/protocol framework or qualification axes were introduced.
- Test-only capture/writer wrappers and the arbitrary-discriminator experiment
  are deleted. Tests exercise production APIs. Candidate census replay moved to
  benchmark tooling; empty and live two-candidate replay smoke checks pass.
  Five duplicate PCRE2 fixture rows are removed. Current schema-10 tooling
  remains; retired schema migration/reclassification machinery is deleted and
  historical-schema input refuses clearly.
- Public/package docs distinguish development from blocked frozen RC1, explain
  current construction coverage, and describe in-flight synchronous cancellation.
  [Parity synchronization](derived-parity.json) adds only four empty historical
  identity fields per generated row: every prior field, timing, Status, snapshot
  hash and summary is unchanged. Original measurement snapshots are untouched.

[Focused Release checks](focused-checks.json) cover affected public text/byte
construction, grammar/capture metadata, class/property/quote/grapheme/Probe paths,
finite admission, original cancellation/deadlines, ownership and writer/search
behavior. Initial failures exposed obsolete Core-extension/alias fixtures; their
valid PCRE2 replacements and final results are identified separately. Filters
overlap. Current benchmark pages and the generated parity report verify.

[One bounded comparison](measurement.json) used frozen before DLLs, existing
Release CLI lanes, five samples and 45-second child caps. One backreference
confirmation was allowed; no further timing, snapshot refresh or Status change.

| Selected observation | Before -> after | Interpretation |
| --- | --- | --- |
| Character warm public Count | 19.254 -> 19.377 us | +0.64%; selected execution allowance met |
| Character construction | 86.631 -> 17.156 us; 32,360 -> 2,912 B | -80.20%; unused preparation/storage removed |
| Character first Count | 103.669 -> 91.694 us; 0 B unchanged | Distinct from warm throughput |
| Backreference warm public IsMatch | 0.670 -> 0.773 us; 3,872 B unchanged | Initial +15.37%; noisy |
| Backreference single confirmation | 1.580 -> 1.129 us | -28.54%, severe unchanged-control variation; inconclusive |
| Backreference construction | 262.394 -> 192.875 us; 57,960 -> 57,384 B | Small-batch observation; no equivalence claim |
| Literal construction control | 3.737 -> 4.125 us; 2,240 -> 2,040 B | +10.38%; wide tails; first IsMatch 0.894 us unchanged |

Backreference timing does **not** establish either compliance with the 10% public
allowance or a demonstrated regression above it. Stop this bounded experiment;
retain that uncertainty with the earlier first-use concern for future release
qualification. Construction/first-use/private costs are separate from warm
matching. No universal performance or fresh package-readiness claim is made.

[Complete Windows/Linux source CI39468662223](source-ci.json) is terminal green
at exact `1609aa039e848e87aee4ca324b3ba85e0ee6f8ee`. All four suites pass on
both platforms, with no failed/skipped tests or duplicate PCRE2 discovery-ID
warnings. Existing analyzer warnings remain. Closeout changes are documentation,
receipts and generated parity metadata only; no second full source matrix is due.

[Nine original RC1 hashes](rc1-unchanged.json) are unchanged. No package, symbol,
zip, manifest, historical measurement snapshot or consumer handoff was rebuilt
or rewritten. Both planning files remain ignored. A corrected immutable candidate,
final package/downstream qualification, unresolved delivery costs and publication
remain deferred; frozen RC1 is unpublished and its PCRE2 bytes must not be published.

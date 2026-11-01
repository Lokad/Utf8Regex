# Reuse decoded scalar in reverse viability rows

Baseline: clean source `1a18b14eee814b2e00ffae9856dfd724cd47efd6`, quiet Linux
VM/.NET11RC3. Carry the preceding Rune into the next reverse row, removing its
repeated decode. Preserve the algorithm, table, allocations, work counters and
all invocation checks. No new execution API, mode, field or cache.

One 120s-capped public Count comparison, CPU mask4, isolated before/after assemblies
and unchanged predecoded/decode-then-.NET comparator lanes. Thirteen cases: actual
canonical region (46429B, SHA507F5A33C406D155D02A9633551C5231E9CF9890D250C1A5153045F224C688D2),
ordinary/compiled/live/finite-live/deadline-live; Unicode boundaries ordinary and
deadline/live; wide, nullable, dense ASCII, dense supplementary NB; unchanged
non-NB scalar and literal controls. Setup/compilation excluded. Full producer
validation included. Results agree before warmup and after each complete batch.

Warm1s/min64 calls per lane, independent60ms calibration, five alternating samples
>=20ms. At most one selected noise confirmation, then stop. Require useful public
improvement with no demonstrated >10% regression and unchanged results,
allocations and resource behavior. Record variation; no universal parity claim.

Focused Release net10/net11 NB/capture/resource tests and adapter controls.
Production state probe must retain exact viability bits, numeric work costs,
storage and synchronous enclosing-loan refunds, including empty, adjacent mixed
scalars, packed-word boundaries and a long empty row. No complete catalog/package
qualification for this source slice; preserve immutable published/staged bytes.

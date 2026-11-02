# Python immutable-policy snapshot: insufficient primary gain

One 77.269-second quiet-VM comparison moved the existing four nullable execution-
limit checks into the shared constructor and stored their result in a private
readonly property. Native Python `Search("needle", "xxneedlezz")` improved
**3.84%**, below the predeclared **5% primary-gain criterion**. All five paired
changes were between -3.69% and -4.17%, with tight producer spreads. No noise
confirmation or implementation variant was justified. **The change was reverted.**

This is an insufficient-gain rejection, not a demonstrated public regression.
The 18 matching operations and two constructor controls had unchanged allocations;
the largest adverse median was 2.16%. All results matched and every timed sample
was at least 54.791 ms. Constructor footprints stayed 50,336/60,960 bytes for the
literal/general controls. Finite limits, live tokens, deadlines, captures, offsets,
Unicode and an unchanged Core Count control were included.

Before assets are the exact frozen b8099e59/dev.13 cost-gate DLL/PDB pair. After
assets are parent 52701a13 plus the archived Python-only patch; Core DLL/PDB bytes
are identical. Normal eight-CPU runtime startup precedes in-process CPU2 affinity.
Each producer/comparator lane calibrates independently and runs five alternating
samples. Complete outputs are consumed after timing. Two construction controls
retain their outputs with a conservative 64 MiB batch cap. CPython is not part of
this focused parent comparison; the .NET lanes are diagnostic comparators.

Existing focused Release checks passed **887 per runtime** on .NET 10/11 before
measurement, including resource policies, funding and native/lifetime controls.
After reverting, the selected fixed-policy/native one-shot/literal checks passed
**72 per runtime**, and the VM's affected project rebuilt successfully.
No public contract changed. No full suite, cumulative gate, package qualification,
catalog refresh or publication was run for this rejected change.

The fixed-original a40bda03 acceptance contract is unchanged. Dev.13 remains
rejected for its stable +10.895% native Search cost; this parent result is not
cumulative acceptance. Preserve the qualified dev.12 and all staged bytes.

[Assessment](assessment.json) records every producer median, allocation and spread;
[verification](verification.json) records correctness and restoration checks.
Raw driver/source, declaration, patches, preflights, all samples and exact DLL/PDBs:
`artifacts/experiments/python-policy-flag-20261102/raw.tar.gz`, SHA-256
`D061A5D1599767CC865DDE11125B63443FBC218BC4E1623EA758CB71D4D3D1CE`.
Local preparation/assessment sources are preserved alongside it as
`local-preparation.zip`, SHA-256
`119CC75F26E37C346122D5FD4675183171D1E219991313E17CDF1595E685CE4E`.

At this safe checkpoint no active TODO handoff Markdown exists outside archived
evidence or scratch. Existing H1-H4 requirements remain in the ignored root plan.

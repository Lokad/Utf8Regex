# Cursor factory inlining: stopped before timing

**Rejected and restored.** One private AggressiveInlining attribute inlined
CreateScalarCursor into MatchValueScalar, but both360B/376B bulk copies remained.
The predicted returned-cursor transfer removal did not occur. Matching Linux
Release FullOpts changes caller410B plus factory356B into caller658B; code size
alone is not a public performance result.

Following the declared code-generation stop, no timing, test matrix, tuned
variant or package qualification followed. Windows/Linux net11Release builds and
the untimed public Search smoke pass. Restored Core/Python DLLs match their
baselines on both OS. Source39d3bcb3 is restored; accepted improvements and all
264staged assets remain. Frozen Full49 graph hashes are unchanged.

[Decision and identities](decision.json). Immutable off-VM archive:
`artifacts/experiments/python-cursor-inlining-20261107/evidence.tar.gz`,
`19E0F79B51067596C01249F8823E26D2E7A4E2B69C54EE07109437ADEE473F59`,3738898B.
It contains before/trial graphs/PDBs, patch, public probe, listings and build/
restoration logs. Further copy reduction needs a distinct design and justified
public benefit; another inlining hint is not a demonstrated solution.

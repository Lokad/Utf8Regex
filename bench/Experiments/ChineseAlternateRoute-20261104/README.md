# Chinese alternation route census

Read-only, untimed inspection of the accepted wide Core `3a8cf7e2`, using the
actual813478-byte Chinese corpus and207 public Count matches. Both ordinary and
compiled modes select the same **ExactPacked** search. Full strict validation
precedes it; fused-family eligibility is false. The generic prepared-search branch
precedes the existing leading-UTF8-segment hybrid branch.

The production packed predicate census consumes40,540 discriminator candidates
at byte offset1;829 survive its second anchor,344 its third, and207 match fully.
The five actual six-byte leading prefixes occur254 times. This demonstrates a
specific source of candidate work; it is not a timing estimate or speedup claim.

The runtime already retains a LeadingUtf8Segment prefilter and its automaton.
Investigate letting that existing Count path handle this qualified family before
the generic packed branch. This needs a bounded public comparison and dense-prefix,
cross-window, priority, malformed-input and controlled-route validation. Eligibility
does not establish that the alternate path is faster or suitable for every family.
No new engine, preparation, mode or permanent instrumentation has been added.

[Frozen route and counts](route.json) identify the exact DLL and corpus. The initial
restored Windows build gave identical route/census results; the authoritative
frozen probe uses the measured accepted Linux DLL. Immutable inspection.zip under
artifacts/experiments/chinese-alternate-route-20261104/ has SHA256
`0829B7F806EA21F40F0AB6BD378922D5002281A7A7A07431CFBB4937F9B442C6`, retaining
the probe, outputs and frozen DLL/PDB. No performance experiment or catalog refresh.

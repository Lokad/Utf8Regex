# Remaining single-capture FindAll gap

The next qualified case is `findall/one-capture-strings`, ratio
**4.172 [4.142,4.179]**, source0e24cece, pattern `item-([0-9]+)`.
Its17408-byte subject produces2048 strings/5120 UTF-8 output bytes; the existing
snapshot records114560B. [Identity](identity.json). The similarly named
many-capture case is2.75x; do not substitute it for this queue item.

One existing60s-capped diagnostic command completed:
`--measure-pythonre-findall-phases findall/one-capture-strings 200 5`.
[Raw phases](phases.txt) show public805.540us/114560B, prepared projection47.492us
(variable22.467–82.042), final shape1.048us, BCL discovery223.541us and
benchmark-only trailing-capture replay186.825us. These cumulative controls are
not additive, a new qualified ratio or a production speedup. The loaded engine
is the same5c source used by the completed literal-lookahead evidence.

Production already looks through CaptureEnd for its no-rewind greedy proof,
uses terminal scalar-class runs and reuses validated ASCII character counts.
Capture trails also preserve rollback and LastIndex for retained/detailed
results; removing them from the shared cursor has no demonstrated safe proof.
The replay suggests a native captured-range opportunity, but it does not supply
a complete production capture/budget/lifetime handoff. No case-specific matcher,
new mode or accounting machinery was added, and no source A/B was claimed.

Keep this qualified gap visible as follow-up work. Prioritize the coherent
fixed-baseline/current-refresh/package/actual-consumer boundary for the accepted
batch; further capture optimization needs a general byte-range proof and focused
public validation. This assessment does not declare the performance goal complete.

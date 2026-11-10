# Common dev64 delivery — qualification in progress

Freeze common0.5.0-dev.64 for Core, PythonRe, PCRE2 and Posix, .NET10/11 with
exact adapter-to-Core pins. This is an internal package candidate, not publication
or downstream adoption. Existing staged package/symbol bytes remain immutable.

Accepted source batch since dev63:

- [Committed LF scanning](../DotLfScan-20261110/README.md),78cea4c9:
  existing bounded dot scanning, preserving scalar/capture/interruption authority.
- [Complete Singleline EOF](../DotSinglelineEof-20261110/README.md),81d1066d:
  proved final greedy dot repeats avoid unused rewind storage.
- [Controlled PCRE2 Count](../PcreClassRunControlled-20261110/README.md),90d18c2f:
  existing ASCII class-run counter retains enforceable finite work and deadlines,
  avoiding unnecessary per-match scratch.

[Actual Python CPU attribution](../PythonCaptureProfile-20261110/README.md)
identifies remaining VM/repeat costs. The subsequent
[argument-flow trial](../ClassRepeatArguments-20261110/README.md) is rejected and
fully restored; none of it enters this candidate. Strict validation remains
required; [email attribution](../CoreEmailAttribution-20261110/README.md) explains
its substantial cost without claiming that the matching gap is closed.

Acceptance pending: one existing four-profile Windows qualifier and original
Awk/Lython handoffs, one complete VM refresh under3600s, identical-package Linux
acceptance, imported/verified benchmark tables and exact source/final-metadata CI.
No package, consumer, performance or platform acceptance is claimed yet. Raw and
terminal evidence will be recorded here when each check completes.

# Deferred Python search-hint preparation

Fixed baseline `5ee5e366`, .NET 11 RC3, quiet VM. The trial caches the existing
bounded direct-delimiter eligibility analysis once per search cursor, including
ineligibility. Match/FullMatch still skip search preparation. No matcher, shaping,
validation, resource calls or cursor ownership changed; two primitive fields were
added to existing state. The production change is **reverted**.

The first ordinary-process ABBA took **48.098 seconds** under 180 seconds, with
identical Core/worker, five calibrated samples per row/process after warmup.
Many-capture strings improve 4.9% (5.9% finite/live), captured Count 4.3–5.1%,
range cursor 6.0–8.1% and Unicode strings 5.0%. Range cursors allocate 8 B more;
other measured allocations stay unchanged. FullMatch is 6.2% slower.

One-capture unlimited is bimodal on both producers: before process medians are
467.9/1230.2 µs; after1498.0/455.3 µs. The pooled +9.0% is not a demonstrated
regression. This uncertainty invoked the one predeclared confirmation.

The sole confirmation took **48.180 seconds**, again under 180 seconds with
unchanged binaries/worker. One-capture medians are452.2/1517.5 µs before and
475.2/1216.0 µs after. Many-capture unlimited changes from−4.9% to+9.2%, with one
after process moving to945.8 µs versus780.7 µs in the other. Finite one-capture
also moves+14.3%. FullMatch now moves−0.7%; unchanged Core Count moves+3.1%.
These pooled changes do not establish stable regressions or speedups under the
observed process variation. Count/Unicode improvements persist, but do not settle
the principal captured-output trade-off.

Stop without a third measurement or further tuning. No permanent mode, new
framework, catalog refresh or package qualification. The earlier rejected ASCII
decoder trial was not reopened. Captured-output parity remains unresolved.

Existing complete Release PythonRe tests passed both targets on Windows/Linux,
including delimiter/range candidates, captures, scalar/empty progression, original
authority, finite result/working limits and cursor aliases. Reversion restores the
exact already-tested baseline file; no repeated source matrix is needed for it.

[First summary](summary.json) and [confirmation](confirmation-summary.json) retain
the separate process observations. Full captured-string results are checked outside
timing; the range-cursor row consumes count and final participation/slot shape.
Raw evidence and matching binaries are copied off VM and hash-verified locally:

- `artifacts/experiments/python-search-preparation-20261109/evidence.tar.gz`,
  SHA-256 `8183DB55E02C8264FB8DB40BC181D0B186A68F216713B4D1C3171A0932CDD3FF`.
- `artifacts/experiments/python-search-preparation-20261109/confirmation-evidence.tar.gz`,
  SHA-256 `0E2DC42F5CE4A5E69D214B828D325C5B7637164AD299D1B6DB601B176B8E7DAA`.

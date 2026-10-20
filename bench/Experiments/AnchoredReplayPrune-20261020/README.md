# Delete anchored-validator partial replays

Before25369fcc library source; dependency-inventory correctionb404e4da is separate.
No matching, resource-admission, metering or lifetime body changes.

The fixed-prefix, first-bounded-segment and suffix-after-first-bounded entry points
in Utf8AsciiAnchoredValidatorExecutor had only Core Debug wrapper callers. Those
wrappers and the segment-summary formatter had only benchmark consumers. The
three private partial Core implementations likewise had only partial-replay callers.
None was a public production or test entry. Caller search covers all src/tests/
benchmark C#; removed declarations are retained locally in removed-members.json.

Delete those seven executor/formatter implementations, their outer/Inspection
forwarders, benchmark-context forwarding methods and obsolete console/timing rows.
Delete the separate benchmark-only native-whole forwarder/row; retain the actual
whole native validator with production consumers. No new replay, accessor, mode,
visibility expansion or abstraction. Shared literal/class predicates and bounded
run-length selection remain because full matching uses them.

All retained executor and facade source text matches the baseline after excluding
only the reviewed deleted declarations. The source-body proof is saved locally.
Public API, strict whole-input UTF-8, scalar/capture semantics, NonBacktracking,
original token/deadline and resource/cursor/result contracts stay unchanged.
This deletion changes diagnostic output, not the engine's full validation rules.

Focused Release Core anchored/simple-pattern/compiled-route/finite checks:6670
pass,no failures/skips. Incremental benchmark closure and bounded postcode CLI
smoke exercise retained full matching and direct fixed-alternation routing. Tiny
smoke timings do not qualify performance or Status. No changed live path to time;
no package/platform matrix for removed benchmark-only code.

After the preceding batch's stale dependency-inventory expectation failed CI,
retain a stricter two-file PCRE2 route allowlist. All10 inventory checks pass on
Windows and on the same built assemblies with Linux/.NET10.0.12. Failed logs,
explicit Linux result and preflight limits are preserved. This correction does
not weaken dependency boundaries or restore any deleted runtime.

Logs, caller/declaration manifest and source-body proof:
artifacts/anchored-replay-prune-20261020. Existing immutable Core/Python115,
Core/PCRE94 and older staged/published artifacts remain untouched. Broader Core
composite diagnostic dispatch and PCRE2 diagnostic wrappers remain for caller
audit/direct-production testing; full R01-R10 is not complete.

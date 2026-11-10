# Unique POSIX workspace ownership

Accepted after dev62 metadata producer `bccf2489`. Remove the separately allocated
workspace object: synchronous entry points own one mutable local; the existing
public reference cursor owns one field. Projection borrows `in` state and its
capture reader is readonly. No live owning copies are passed between consumers.
Release clears buffers, their loans and the program reference, resetting to a
default value that can be released again safely. Constructor failure already
refunds its partial state acquisition; pre-construction validation failure can
release the default local without invoking the host allowance.

The public cursor remains a reference owner: aliases share progress/disposal,
retained coordinate values remain independent. No public API/lifetime change,
initialization flag, finalizer, pool, executor or execution mode. Major buffers
retain admission before allocation; modeled working peaks and all work/depth/
result/cancellation/deadline controls stay. Removing an unmodeled helper object
does not turn the resource policy into an exact CLR/process memory guarantee.

One **82.878s** separate-process ABBA on the quiet .NET11 RC3 VM, within180s;
21 public lanes and420 samples, all at least56ms. Same Core/caller, one producer
per process, full results and zero loans checked outside timing. Add only actual
public literal IsMatch and cursor controls to the existing19-lane worker. These
generic controls are not new POSIX catalog headlines. No confirmation or package/
platform/Full qualification; dev62 bytes and snapshots remain immutable.

| Public operation | Before (µs) | After (µs) | Change | CLR B/op before→after |
|---|---:|---:|---:|---:|
| Empty captures |0.747|0.724|−3.1%|208→64|
| Nullable captures |1.035|1.013|−2.2%|200→56|
| Absent captures |0.574|0.556|−3.0%|192→48|
| Empty captures, finite |1.048|1.016|−3.1%|1184→1040|
| Empty captures, funded |1.205|1.169|−3.0%|1184→1040|
| Empty captures, deadline |1.295|1.270|−1.9%|1232→1088|
| History12 |97.915|98.001|+0.1%|192→48|
| Ambiguous Count |0.318|0.317|−0.3%|144→0|
| Literal IsMatch |0.055|0.057|+3.8%|144→0|
| Ambiguous cursor |0.924|0.956|+3.5%|352→328|
| SKU consumer,4096 calls |5209.963|5213.933|+0.1%|983040→393216|
| Unchanged Core Count |0.836|0.849|+1.6%|0→0|

Accept the144B synchronous allocation reduction and smaller short-capture costs,
retaining the measured Boolean/cursor timing penalties. The cursor embeds more
state but avoids a second object, saving24B total. Cold construction allocations
and funded empty/nullable peaks976/960B are unchanged. Small movements and
uncertainty remain unnormalized; no refreshed native ratio or composed gain.
[Summary](summary.json) contains every lane/process/range.

All198 POSIX Release checks pass per target on Windows/Linux, .NET10/11. Existing
independent scalar/longest/empty/absent/nested fixtures and public capture windows,
63/64/65-scalar alias advances, retained values, malformed input/floors, constructor
refusal/cancellation, history-phase original-token/deadline interruption, finite
refusal and synchronous refunds exercise the changed ownership. No tests were
added merely to assert a representation. The friend refusal test now also owns
one default-safe local. Source review covers all three synchronous owners, the
factory return, readonly projection and durable cursor field.

Raw patch, protocol, before/after graphs, Linux checks and samples are verified
off VM at `artifacts/experiments/posix-workspace-value-20261110/evidence.tar.gz`,
SHA-256 `58A524D813B924ADE068A3EC220E4012C182ECF42EA7D75F6917CC0300CB4F61`.
Windows checks are alongside it. Source CI follows the scoped commit.

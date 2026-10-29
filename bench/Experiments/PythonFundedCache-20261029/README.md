# Funded PythonRe replacement preparation and active cache lifetime

**Accepted, source 59638981.** The account supplied to PythonRe Compile now also
admits covered replacement preparation and retained keys/entries/plans. Existing
construction reservations transfer a conservative completed charge to the entry;
failure refunds discarded storage and keeps valid cache values. Warm reuse borrows
the existing charge. There is no maximum-cache precharge or historical accumulator.

The program lifetime owner is now the shared Core Utf8FundedStorage utility.
Cache removal closes an entry; uses acquired under the cache lock keep it funded
until their final disposal. Preparers and pending waiters hold uses before waiting,
so clearing a pending entry before charge transfer is safe. One independently
controlled uncached retry after a foreign pending interruption is funded too.
Funded expression disposal stops retention before clearing, allowing existing
operations to finish without repopulating it. Ordinary Core/PCRE2 caches keep their
existing admission scopes; no second host cache, graph registry or finalizer.

This extends program/cache admission. Shared working admission on other operation
families and caller-retained output transfer remain open hosting requirements.
Incidental owners are outside major-capacity coverage; this is not exact heap/RSS.
Native kernels, scalar/capture semantics and original interruption/limits stay.

[Public comparison](comparison.json), [console](comparison.txt),
[preflight](preflight.txt), [driver](driver.cs.txt), [project](driver.csproj.txt):
exact 7deb8230 versus 59638981 producer DLL/PDBs, quiet VM CPU2, .NET11 RC3.
**36.62s**, 500ms per-lane warmup, independent calibration, three alternating
samples, minimum batch **23.60ms**, 120s outer cap. No confirmation or catalog
refresh. Public ReplaceToString includes validation, discovery, substitution and
owned text publication. All returned results remain live in a preallocated batch
during timing; equality/checksum consumption occurs after timing. Batch arrays
are outside measured producer allocations. Every result agrees with equivalent
predecoded/decode-then-.NET operations on overlapping semantics.

| Public operation | Before us | After us | Before / after B/op |
| --- | ---: | ---: | ---: |
| Existing unfunded warm ASCII substitution | 12.48 | 12.51 | 5352 / 5352 |
| Existing unfunded warm Unicode substitution | 16.64 | 16.73 | 6400 / 6400 |
| Existing unfunded finite/live-token substitution | 15.57 | 15.55 | 5472 / 5472 |
| Funded warm ASCII substitution | 12.25 | 12.48 | 5376 / 5696 |
| Funded warm Unicode substitution | 16.28 | 16.50 | 6424 / 6744 |
| Funded finite/live-token substitution | 15.72 | 16.06 | 5496 / 5824 |
| Existing unfunded 64-template churn | 1.921 | 2.000 | 3720 / 3728 |
| Funded 64-template churn | 2.019 | 2.444 | 3744 / 4016 |

Existing warm calls changed <=+0.6%; unchanged Core replacement -0.3% and funded
Python Count +0.05%, with unchanged allocations. Negative variation, including
Core Count -7.7%, is not a gain. Existing cold-template churn +4.1%/+8 B reflects
the extra optional entry field. Funded warm calls add **1.3-2.1% / 320-328 B** for
the current funding scope/use, while funded churn adds **21.1% / 0.43us / 272 B**.
Those funded rows now enforce replacement/active-plan admission that the old
account did not cover. They are additional authority costs, not equal-authority
regressions or a global <=10% claim. Further accounting precision was not added.

Shared retained balance stabilizes across warm/churn batches and every program's
final disposal returns all funding. For the template cycle, the candidate retains
73376 modeled bytes versus the old program-only 52384; this adds actual plan
coverage rather than charging every historical template. Cold scope allocation
on a funded warm hit is a known cost, not hidden as matching work or equivalence.

[Release checks](verification.txt): complete PythonRe 4114/runtime, Core affected
construction/cache/inventory 1376/runtime and PCRE2 affected construction/resource/
replacement 510/runtime, .NET10/11. Real public substitutions cover warm reuse,
large invalid templates on empty/warm caches, partial refusal/cancellation, stable
many-template retention, concurrent reuse, active eviction/disposal and foreign
cancellation. A direct production-cache test detaches pending preparation before
transfer while preparer/waiter uses remain, then proves refund after the last use.
The first full run found eight obsolete private-method reflection signatures;
tests now call the changed implementation directly, without a forwarding executor.
Both the failure and passing logs are preserved.
CI 40961696611 on 59638981 passed Windows/Linux, both runtimes and disabled
dynamic code.

Follow-up 93184584 fixes a demonstrated cold failure-path gap: timed-out funded
template preparation previously decoded a 1 MiB subject into Exception.Input
outside admission. The facade now omits that diagnostic subject on funded
expressions. The public test failed on both runtimes before the change; all 65
funded/timeout checks per runtime then passed. A second public test proves that
partial preparation denial with a full cache preserves its already funded plans.
Success-path code is unchanged from the measured producer; no timing was repeated.

Immutable archives in artifacts/experiments/python-funded-cache-20261029/:

- python-funded-cache-59638981.tar.gz: exact producer DLL/PDBs, source bundle,
  driver/build/preflight/raw results. SHA256
  `5D39584D74E94BBF94024560371E0F67502A5AB520E1942FAC1014CF3E4B0D3E`.
- correctness-logs.zip: SHA256
  `57BB53E9D54F7EE1E9C81C708376E6491B759DB21036BB8436ED48B24C4BF67C`.
- timeout-followup-logs.zip: failing and passing diagnostic witness plus partial
  denial checks. SHA256
  `08071E5E48AC29D80ED1FCF471A93D4B68808A487DEC72A916299DE7DB3563C5`.

The archive was streamed to the workspace and fully inventoried. No unchanged
package/platform/catalog qualification was repeated. Final cumulative performance,
current sub-hour refresh and release/consumer acceptance still require final bytes.

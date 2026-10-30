# PCRE2 caller-funded compilation and Boolean/count operations

Producer **84f21325da5a8141733003dba1f0a135c12b8dce**, baseline
**cb61b569f3d588594a519ed5c1fca41b487e6c2f**. First H2 PCRE2 ownership slice;
persistent cursors, retained results, funded replacement and H3 native pattern/
template parsing remain unfinished.

Funded text/byte Compile returns a disposable `Utf8Pcre2RegexProgramOwner`, keeping
the underlying expression private. Close rejects new calls and refunds compilation
after actual active users finish. Boolean/count calls accept byte starts, PCRE2
match flags and independent cancellation; temporary storage uses program funding
or a separate working allowance. Raw expressions also accept shared working
admission. Existing construction/storage leases and scalar/backtracking drivers
are reused. Major buffers are admitted before acquisition; loans refund
synchronously. Numeric work/depth/heap/result controls and profile errors remain.
Borrowed subjects and incidental invocation metadata are separate. There is no
finalizer, replacement matcher, new resource framework or relaxed admission.
Byte compilation still decodes under construction admission; it is not direct
native parsing. [Contract](../../../docs/SPEC-PCRE2.md).

Release checks pass on both .NET 10 and .NET 11: full PCRE2 **6229/runtime**,
then final test-only concurrent/root-ceiling additions pass **76 focused/runtime**,
including all **23 owner cases**, API and managed-access guards. Checks cover
different children of one finite root, separate allowances, original compilation/
call tokens, actual working cancellation/deadline, strict malformed tails/scalar
starts, byte-buffer reuse, compile failure, terminal refusal, flags, global empty
progression, close during active work and deterministic refunds. Existing copied
span-cursor lifetime is unchanged. [Verification](verification.txt).

The one [public A/B](comparison.json) took **42.00s**, under a 120s cap;
minimum actual sample **43.36ms**, matching samples at least **81.60ms**.
Ubuntu 24.04.4/Xeon Platinum 8573C, .NET 11 RC3, CPU mask 4. Each lane warms
600ms, calibrates independently toward 80ms and records three alternating samples.
Results/checksums are checked after timing; working/program balances are checked
between samples and final close. No confirmation, catalog refresh or package matrix.

| Equal-result public operation | Before / after unfunded (us) | Shared working (us) | Program default / separate working (us) |
| --- | --- | --- | --- |
| Literal Count, 8198 B | 0.321 / 0.321 | 0.882 | 0.926 / 0.938 |
| Scalar Count, 1472 B | 88.950 / 87.846 | 88.429 | 88.680 / 88.678 |
| UCP Count, 1280 B | 117.899 / 126.561 | 137.228 | 137.273 / 137.383 |
| Finite/live scalar Count | 95.926 / 91.792 | 91.786 | 91.871 / 92.025 |
| Literal IsMatch, 8198 B | 0.312 / 0.313 | 0.477 | 0.534 / 0.530 |
| Scalar IsMatch, 1472 B | 1.204 / 1.213 | 1.450 | 1.485 / 1.468 |

Unfunded matching controls vary **-4.31% to +7.35%**. The new feature is not
timing-equivalent: owner literal Count is about **2.9x** current unfunded cost,
while scalar/finite Count stays close. Existing major-buffer-free literal calls
allocate 120 B/op for shared working state and 144 B/op through a program lease.
Scalar unfunded operations allocate 3872 B/op, shared working 3992 and owner 4016.
Finite/live scalar calls already allocate 3992; owner calls add 24 B.

UCP is different: before control allocates zero; after control records one 1480 B
pool refill in two 1024-call samples (1.445 B/op), zero in the third. Shared-working
samples allocate 1598.55–1600 B/op and owners 1624. The existing exact-state pool
allows finite/shared borrowers to take retained leaves, then drop rather than
re-retain them when their admission ends. Alternating funded lanes can therefore
drain the after control's pool; the baseline has an isolated assembly context.
This is source-supported attribution, not another measurement or a claim that
allocations are unchanged. Working peaks are 1504 B for UCP and 3968 B for scalar
cases; literal operations need no modeled buffer loan. Fixed metadata allocations
are distinct from modeled major capacities. No retention/accounting mode was added.

Eager compilation before/after/funded+close costs **0.772 / 0.798 / 0.976 us**
for literal and **248.655 / 182.821 / 205.865 us** for scalar. Funded compilation
adds 112 allocated bytes in both cases (1880 -> 1992 and 66704 -> 66816), with
feature costs +22.25% / +12.61% over current unfunded compilation. The large scalar
construction control difference is retained without crediting this API addition
as a compiler optimization. No general parity/comparator benchmark Status claim.

Immutable evidence under artifacts/experiments/pcre2-funded-owner-20261030/:

- pcre2-funded-owner-84f21325.tar.gz: exact Core/PCRE2 DLL/PDB pairs before/after,
  source bundle, affected-project builds, driver/preflight/raw results. SHA256
  `36BD1E1C5EDDE6C961F44B7DCFBADFF0B84A23A067CED72AC3B69A468AC69AF5`.
- correctness-logs.zip SHA256
  `30760E42CDA493B19283E7452994CF050CAA187EEEFF1276B17CA66BC47C9EC3`.

Continue the explicit PCRE2 cursor/result lifetime flow and native replacements.
Final cumulative fixed-baseline cost, current sub-hour refresh and immutable
package/actual consumer acceptance remain release gates.

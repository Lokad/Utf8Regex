# Terminal ACCEPT guard proof

Accepted against the exact [N13-after graph](../PcreConditionalProof-20261109/README.md),
PCRE2 source `fc5ce5fb` with frozen dev59 Core. Extend the existing cold guard
unwrapper through an exact two-child prefix/unnamed-ACCEPT sequence, then require
one eligible ASCII token. The existing finite compiler filters the branch classes;
no runtime executor, guard state or mode is added. Existing64/128 construction
caps, major admission and interruption checks remain.

PCRE2 makes an ACCEPT assertion condition true/false for positive/negative
lookahead, retaining captures in both cases. The owned conditional runner already
implements that flow. Controlled calls and result/capture APIs keep the VM;
named ACCEPT, other verbs, extra suffixes, empty branches and unproved guards
decline. See [PCRE2 assertion rules](https://www.pcre.org/current/doc/html/pcre2pattern.html#backtrack).

One **88.079-second** ordinary-process ABBA on the quiet .NET11 RC3 VM under the
declared180s cap: same Core/worker, changed adapter, five samples/process/lane
after500ms warmup and independent75ms calibration. All440 samples exceed67ms;
full values and zero working loans are checked after timing. No confirmation or
source tuning.

| Public operation | Before (µs) | After (µs) | Change |
|---|---:|---:|---:|
| Negative ACCEPT hit, unlimited | 0.375 | 0.013 | −96.5% |
| Negative ACCEPT miss, unlimited | 0.367 | 0.015 | −95.9% |
| Positive ACCEPT hit, unlimited | 0.407 | 0.013 | −96.8% |
| ACCEPT class intersection | 0.398 | 0.011 | −97.2% |
| Dense candidate search | 446.85 | 7.70 | −98.3% |
| Negative hit, finite/live | 0.50 | 0.49 | −2.8% |
| Negative hit, funded/live | 0.51 | 0.53 | +3.4% |
| Excluded named ACCEPT, unlimited | 0.374 | 0.424 | +13.4% |
| Captured Count control | 11.97 | 12.72 | +6.3% |

Allocations/working peaks are unchanged in all22 lanes. Target and dense gains
occur in both processes, as do the adverse named-ACCEPT and captured Count costs;
accept those trade-offs explicitly. Named finite/funded controls improve11.1/6.4%,
deadline calls~3%, normal guard46% and existing bounded32%, despite unchanged
fallback/normal program instructions. Do not attribute those movements solely
to the cold proof, normalize by controls or compose native ratios. Core Count~0%.
[Summary](summary.json) retains process medians, ranges and all resource costs.

Focused Release78 checks/target pass, followed by whole PCRE2 **6610/target on
Windows/Linux**, .NET10/11, reusing matching Windows binaries. New independent
checks pin ACCEPT branch/class outcomes, nested wrappers, retained captures from
negative conditions, scalar floors, malformed tails, original token/work refusal
and conservative exclusions. Existing deadline/depth/working/result/recursion
checks pass. Old conditional tests no longer assert native/executor routing;
capture-set cases compare public captures on overlapping ASCII semantics.
The initial strengthened test requested text for absent groups, which correctly
throws; it now checks participation before text. No producer change was required.

Raw protocol, driver, patch, before/after graphs, Linux checks and observations
are verified off VM at
`artifacts/experiments/pcre-accept-guard-20261109/evidence.tar.gz`, SHA-256
`71B957C16B045A5CB4B079E39F9AB34B0FE47498744E75094F67494F8CB81412`.
Windows logs are alongside it. Historical packages/snapshots stay immutable.
Common dev60 will qualify N12/N13/N15 together once at the delivery boundary.

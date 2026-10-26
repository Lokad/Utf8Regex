# Managed PCRE2 qualification

## Current common RC2 candidate

**0.4.0-rc.2**, producer d1a14f407ae4e34230430260fc58111c960c1e63, is
qualified and unpublished. Both adapters require exactly Core [0.4.0-rc.2].
The [fresh release receipt](../../bench/Experiments/ReleaseCandidate2-20261026/README.md)
records complete detached Windows/Linux suites, original public consumers,
API/member/PDB/source/dependency identities, green source CI and bounded actual-
package/cumulative costs. PCRE2 has 6,189 passing cases per platform, with no
final failures/skips. The initial staging correction and cost uncertainty remain
disclosed. Historical RC1 qualification below does not establish this result.

The selected managed profile and its documented exclusions remain the contract.
Generic partial decisions outside supported drivers, full grammar completion,
NativeAOT/trimming and complete native PCRE2 equivalence remain unqualified.
Defaults are unlimited; modeled controls do not contain an entire host process.
No package publication, external consumer pin changes or old snapshot refresh.

## Historical RC1: superseded, publication blocked

The superseded frozen release is **0.4.0-rc.1** for Core, PythonRe and PCRE2,
producer `2e97ac974e6e8c60aac95db1e4808cbda2ff9736`. Both adapters depend on
exact Core `[0.4.0-rc.1]`; all three restore into one dependency graph. This
supersedes the separate Core/PCRE2 scalar.94 designation and **has not been
published to NuGet**. Later review found PCRE2 defects in this producer: its
native backtracking compiler can bypass construction depth/instruction limits;
fixture-specific validation misses neighboring invalid syntax; and compilation
can publish a program with no executable backend. Lexical guards can also reject
valid classes when an escape operand resembles a class/group delimiter.
**Do not publish these PCRE2 bytes.** Keep their original evidence and identities;
a corrected release requires a fresh immutable candidate and final qualification.

The [tracked release identities](../../bench/Experiments/ReleaseCandidate-20261020/release.json)
identify the packages, symbols, DLLs and qualification scope. Local-only handoff,
restore instructions and integrity manifest remain under
`artifacts/utf8regex-0.4.0-rc.1-20261020/`; they are not in a source checkout.
The canonical local feed remains
`artifacts/release-candidates/0.4.0-rc.1/feed/`; preserve its bytes and version.

The same final DLLs passed complete detached Release suites on Windows and Linux:
Core 39,870, differential 1,504, PythonRe 3,881 and PCRE2 5,911 per platform,
without failed/skipped results. Five existing duplicate PCRE2 discovery-ID
warnings remain disclosed. These counts describe this frozen evidence, not a
coverage percentage or a guarantee for later source.

The release also passed:

- unchanged producer Awk/Lython assertions and the PCRE2 public consumer,
  including 676 recorded native-property observations;
- one combined three-package consumer with exact Core dependency resolution;
- CPython capture/semantic fixtures on their overlapping profile;
- private-feed source mapping, fresh caches, lock SHA512 and actual loaded DLL
  identity checks; and
- API/member, managed-only dependency, PDB, SourceLink and symbol checks.

Core/PCRE2 retain only permitted `Unsafe.SizeOf` queries; PythonRe has none.
There are no unchecked reads, generated staging helpers, native imports or
shipped BCL matcher/result implementations. BCL timeout/options metadata and
PCRE2 `Regex.Escape` are not matcher execution. Exact producer
[Windows/Linux CI](https://github.com/Lokad/Utf8Regex/actions/runs/39401075919)
is green; later source must earn its own validation.

## Contract and limits of the evidence

[README.md](README.md), [SPEC-PCRE2.md](../../SPEC-PCRE2.md),
[ARCHITECTURE.md](ARCHITECTURE.md) and
[HOST-INTEGRATION.md](../../HOST-INTEGRATION.md) define the supported managed
profile, operation admission and ownership. Normal matching uses complete scalars
over strictly validated UTF-8 with primary byte coordinates. Explicit opted-in
`\C` and non-slice `\K` ranges have their documented special semantics.

Synchronous cancellation, the original absolute deadline, bounded state/work/depth,
major working admission and cumulative result/staging admission remain required.
Defaults are unlimited; modeled capacities do not bound all heap/RSS, incidental
helpers, callbacks/getters, separate preparation or arbitrary host retention.
Native PCRE2 units and timings are not interchangeable with managed limits.

`Probe` has a curated partial profile. DFA matching, invalid-UTF subject mode,
callouts, split, full capture history and unsupported substitution/option forms
are excluded. Managed uppercase replacement maps final sigma U+03C2 to U+03A3;
it does not reproduce the observed native 10.47 lowercase-sigma anomaly.
No complete .NET/Python/PCRE2 equivalence is promised.

The release introduces no new throughput or benchmark Status claim.
[Existing package-cost evidence](../../bench/Experiments/Scalar119Delivery-20261020/README.md)
retains its actual package/runtime scope and uncertainty. In particular, finite
email Count can exhaust a 50-million-work policy although unlimited Count succeeds;
the unqualified search experiment was excluded. Native comparison belongs only
to private benchmark tooling. Producer consumer checks do not establish external
consumer adoption.

## Corrections since RC1

Source has advanced to **0.4.0-rc.2**. Superseded helpers, shipped benchmark
replay, test-only cache/collector machinery, construction field-transfer mirrors
and duplicate package orchestration have been removed. Subsequent corrections
add the following construction behavior:

- Backtracking parsing/lowering and reference-following analysis use the shared
  construction owner. Recursive frames have a fixed 256-frame backstop, tightened
  by smaller caller limits. Instruction/auxiliary buffers admit capacity and
  growth overlap before allocation. AST/maps/metadata use a conservative 256-byte
  charge per UTF-16 pattern character, plus 256 bytes per name entry for each
  lowerer's own lookup. Shared storage can be charged again; earlier refusal is
  intentional. Whole-heap exclusions remain separate.
- Character tokens and class/property storage now share major-buffer admission,
  instruction capacity and bounded interruption, including single-token parsing
  from the backtracking compiler. Character programs omit unused Core preparation
  and syntax-node/offset vectors; literal Rune staging is temporary.
- Final compilation requires an owned PCRE2 semantic parse. Unrestricted Core
  grammar acceptance and its direct executor are removed; undocumented .NET
  escape/property/balancing extensions now refuse construction. Shared Core fast
  paths remain behind profile parse and eligibility proofs.
- General contextual bounds/flags/escape checks replace fixture-spelling checks.
  A final all-declined program throws `NotSupportedException` during construction.
  Empty intermediate foundations still support actual profile compilers.
- Lexical policy/name/structure guards share complete escape-operand skipping,
  preserving class and quoted-text boundaries while retaining real policy errors.
- Lexical scope buffers now admit major capacity/depth through the original
  construction owner and poll during long scans, before semantic parsing.
  Completed operation slots discard unused original Core preparation and retain
  the actual derived Core or immutable slice payload where needed. Public null
  text patterns report the named argument error after established policy checks.

Strict UTF-8/scalars, captures, synchronous original tokens/deadlines, owned
results/cursors and useful work/depth/working/result controls remain required.
[Sixth-review source evidence](../../bench/Experiments/SixthReview-20261020/README.md)
records the grammar boundary, character admission and retained-state cleanup.
[Seventh-review evidence](../../bench/Experiments/SeventhReview-20261021/README.md)
adds lexical admission, live-owner cleanup, a bounded construction/public cost
comparison and green Windows/Linux source CI at `884b156b`. Source checks **do
not** replace or relabel RC1's package evidence.
The [eighth-review source receipt](../../bench/Experiments/EighthReview-20261021/README.md)
adds pre-acquisition major state/buffer bounds for optional finite/ASCII expansion,
original interruption and complete program publication without dummy foundations.
Its bounded local public/construction comparison has its own controls and scope;
it does not qualify new package bytes or finite-policy throughput.
No corrected development package has been qualified. The earlier public first-use
latency concern and inconclusive backreference timing remain open for a future
delivery; no universal performance equivalence or fresh benchmark Status is claimed.

For a future real package boundary, use the existing consolidated flow:

```powershell
./qualify-package-candidate.ps1 -Profiles Core,PythonRe,Pcre2
```

It requires clean source/new coherent version, builds once, packs each selected
package once and checks detached consumers plus exact provenance. `-SkipBuild`
requires matching configuration/revision binaries. The default includes
Core/PythonRe; PCRE2 can qualify separately by selecting `-Profiles Pcre2` (Core
is always included). Platform/public-downstream/performance acceptance is separate.
Do not rebuild RC1, repeat unchanged package qualification, or infer wider profile
completion from this command.

## Historical evidence

Earlier immutable candidates and failures keep their original identities:
scalar.94, scalar.92 property repairs and scalar.76 construction input. Their
local-only handoffs are under `artifacts/pcre2-scalar94-delivery-20261018/`,
`artifacts/pcre2-scalar92-delivery-20261018/` and
`artifacts/pcre2-scalar76-delivery-20261017/`; these are not source-checkout files.
They are historical inputs, not the current dependency graph.
`verify-pcre2-qualification.ps1 -QualificationId <original-producer-sha>` remains
available to verify the original archive-manifest format; it does not produce
new candidates. The superseded per-package scripts are removed.

The full former execution ledger, including earlier receipts, failures and
cost caveats, is preserved in
[repository history](https://github.com/Lokad/Utf8Regex/blob/0e8238e42ae79d7a8b3332bd9ab6d82aff0ca3f8/src/Lokad.Utf8Regex.Pcre2/QUALIFICATION.md)
and the local gitignored PLAN.HISTORY.md. It is not an additional current
qualification claim. Broader profile, host/default-policy, performance and
publication work is deferred by the focused cleanup plan.

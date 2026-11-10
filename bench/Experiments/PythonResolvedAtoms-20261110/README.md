# Lower Python parser-resolved dots and anchors directly

Before `5de0fe829b9fc526655176135047ef612073454d`; after
`099976cd531790a675f6e29fdee5c054ef5ab89f`. Three existing semantic atom kinds
now create existing scalar nodes under the admitted factory. DotAll uses the
parser's canonical atom text; anchor Multiline uses the established conversion
scope. This avoids rendering a fragment, reparsing a Core tree and lowering it
again. Existing factory callers retain their defaults. The live class/escape/
impossible-surrogate leaf parser remains. No parser/profile framework or mode.

One **33.527s** separate-process public constructor/constructor-plus-first-Count
ABBA on the quiet Linux VM, .NET11 RC3. Startup8CPUs, then CPU2 affinity; no
tiering overrides. Exact Core/Python DLL/PDB identities and source revisions,
both graph manifests, equal inputs and independent results checked before timing.
Each row warms500ms, independently calibrates to75ms, retains five samples/process:
160samples, minimum58.377ms. Outer180s/worker45s caps; no confirmation needed.
Both producers use default **Validate** with valid immutable UTF-8 subjects.

Constructor rows time construction only; their engine results are checked by
Count after the clock. First-Count rows include construction and that first call.
Result batches are preallocated and consumed after timing. These are construction/
first-call costs, **not warm matching throughput or native parity Status**.

| Public workload | Before µs | After µs | Change | Allocated B before / after | Sample ranges overlap |
|---|---:|---:|---:|---:|:---:|
| Dot/anchors, construct |219.173|175.192|-20.07%|60528 / 50120|Yes|
| Dot/anchors, construct + Count |102.432|92.725|-9.48%|60528 / 50120|Yes|
| Scoped flags, construct |91.552|75.651|-17.37%|85808 / 70048|No|
| Scoped flags, construct + Count |87.605|71.800|-18.04%|85808 / 70048|No|
| Multiline, construct |62.449|54.675|-12.45%|58976 / 48568|No|
| Multiline, construct + Count |61.130|50.489|-17.41%|58976 / 48568|No|
| Unchanged literal, construct |53.346|53.518|+0.32%|50352 / 50352|Yes|
| Unchanged literal, construct + Count |44.845|44.133|-1.59%|50352 / 50352|Yes|

Patterns/subjects/expected counts are `^.$` / `😀\n` /1,
`(?s:^.(?-s:.)$)` / `\n😀` /1, `(?m)^.$` / `😀\nx\n` /2,
and control `needle` / `needle` /1. The first two dot/anchor rows vary materially
between processes and differ in absolute cost; do not infer a negative Count
cost by subtracting them or promote their pooled percentage as certain. Scoped
and multiline observations plus saved allocations support the small change.
All process medians, ranges and identities: [summary.json](summary.json).

All5024 PythonRe Release tests pass on each of .NET10/11. Eight added public
cases independently check lexical DotAll/Multiline addition/removal, newline,
astral, escaped literals and surrogate-impossible atoms under both input policies
and finite limits. Existing construction, structured-error, token/deadline,
capture/result/cursor-lifetime and admission/refund tests remain.
Major-buffer/instruction bounds, per-frame/map work and admitted node factories
remain; construction work/capacity requirements can decrease with removed work.

Raw `artifacts/experiments/python-atoms-20261110/evidence.tar.gz`, SHA256
`7BC57FBA713E05A0A71993DDB6B1565A7441D0C281C2FD72E87728E0451B06AF`.
The adjacent `driver-validation.tar.gz` retains the exact worker/project, runner,
summarizer, build/smoke and test logs. Timing is CLOSED. Existing immutable
packages and broader profile benchmark tables are unchanged.

# Python ASCII word predicate: no material date benefit

**Rejected; production and candidate-only tests reverted.** Parent source
`2c8200f94135caf449478ffae6f03fb426fccd2d`. The candidate combined the two ASCII
letter ranges with a case-bit/range check, inside the existing ASCII guard.
Pinned Unicode lookup, allocations, work admission, interruption, captures and
lifetimes were unchanged. No new table, state, execution route or unchecked API.

Untimed Release FullOpts inspection found the original predicate remained an
81-byte branch tree; candidate code was 73 bytes. Smaller code did not produce
a useful improvement on the actual date workloads. Keep the readable original
range expression. No confirmation, reordered variant or follow-up tuning.

One **50.496676394-second** public ABBA compared ordinary Default-load-context
processes, one producer per process. Core, PCRE2, benchmark and worker DLL/PDB
bytes were identical; only PythonRe changed. Normal eight-CPU startup, then CPU2;
default tiering/PGO, one-second/min1024 warmup, independent 60ms calibration and
five samples per row/process. All **180 samples exceed 56.381ms**. Complete
Boolean/Count results and captured string arrays are checked after timing.
Disassembly overrides were confined to separate untimed preflights.

| Public operation | Before us | Candidate us | Change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| Unchanged literal IsMatch control |0.11693|0.11649|-0.38%|0|
| Actual Python date miss |1.57555|1.57657|+0.06%|80|
| Date hit |0.51441|0.51520|+0.15%|80|
| Actual date miss, finite/live |1.96016|1.98652|+1.35%|192|
| Dense date misses |81.78945|81.94236|+0.19%|80|
| ASCII word Count |498.10|496.39|-0.34%|1056|
| Unicode word Count |350.19|346.08|-1.17%|1056|
| Unicode boundary Count |244.52|241.46|-1.25%|0|
| Unicode captured FindAll strings |359.04|352.53|-1.81%|41496|

[All observations](cost-summary.json) retain per-process medians, sample ranges,
allocations and identities. Date process ranges overlap. Small secondary changes
and unchanged allocations do not justify retaining this rewrite; no guessed JIT
cause or universal regression gate. These compare producer versions only; the
qualified dev40 CPython/.NET catalogs remain unchanged. No new benchmark Status,
catalog refresh, package qualification or published/staged byte changes.

Candidate Release validation passed 4817 Python tests per target on Windows and
Linux, including every ASCII value against the pinned mask and public word and
boundary operations. Existing Unicode/pinned-category, capture/alias, finite
resource, cancellation/deadline and malformed-input coverage passes. Core and
PCRE2 implementation were unchanged. After reverting both edited files, affected
projects were rebuilt and 36 focused boundary/repeat/resource checks per target
passed on each operating system. Production and test trees match the parent;
all eight measured Full DLL/PDB identities still match.

Both nine-row preflights passed before the original command stopped at its
8 GiB RAM admission, without taking samples. The same prepared timed phase
admitted at 8707992 KiB after **760 seconds** of bounded readiness; build hosts
released memory naturally. Threshold, source, worker, controls and timing protocol
were unchanged; no shutdown or repeated preflight. Copying Full's reserve into
this small experiment imposed disproportionate preparation cost. AGENTS now asks
for conservative admission appropriate to focused workers and retained results;
the Full/package checks retain their capacity requirements.

Exact unchanged Core:
`633DA3DDC5E9FDC471ED45C90A2055802F9603BB6E1260E5E016CAB367D4EDAF`.
Python before:
`2E76F402929F563DB7984A8A6690D051EC50B447CD86880283173BB957A1A623`;
candidate:
`596E8F00E9383EA1359C7E3ECC14DAC64C0DD078C9761B325BE0427BB7740B8F`.

Immutable `artifacts/experiments/python-word-predicate-20261106/`:

- `evidence.tar.gz`: `ACB15C7982224A41D0EBD01D63593DBB5B7E9CD17D2B3D9F25696B205D2A9664`.
- `windows-evidence.zip`: `A2F0000123F7FE69B1CB8A51F0CEEE622D8D8DEB68D622C0F9B1877F069EE10B`.
- `restoration.zip`: `2A7F70A311A3F8A529AED4E45DDB2581B9F04B440AD15657C609EE3C218B1BA6`.

Archives retain both graphs, exact source patch, driver/declaration, every public
sample, preflights, disassembly, readiness and terminal-zero evidence. Dev40
packages, tables, completed 43m31s Full and all eight measured DLL/PDB bytes remain
unchanged. Resume the performance queue with a different demonstrated cause.

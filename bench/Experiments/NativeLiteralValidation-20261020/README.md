# Native Unicode literal validation experiment — rejected

Frozen119 attribution confirms that its837-byte Cyrillic miss spends0.815us on
whole-input metadata validation,0.598us on existing validity-only validation and
0.051us on prevalidated search; public Search0.869us. All these miss-lane samples
exceed51.566ms. The23.527s diagnostic also contains weak8.602ms kernel samples and
1.735x spreads elsewhere: those are attribution observations, not public gates.
Isolated BCL validity0.355us does not prove metadata/authority substitutability.
No validation kernel or checked-access campaign is reopened.

A small prototype deferred validation only for unmetered/no-hook pure exact Unicode
literals, keeping whole-input metadata on hits and strict validity on misses.
Controlled, diagnostic, ASCII, boundary and trailing routes kept their original
order. Same existing native search, no helper/owner/framework/API/mode. It passed
Python839 and Core598focused Release checks.26new cases cover scalar coordinates,
all six native value/text consumers, malformed prefix/tail/overlong/surrogate/
truncated input, original cancellation/deadlines, diagnostic validation ordering,
work denial, parent loans and retained views/snapshots. One new test initially
miscounted 中é😀尾 as6UTF16units; corrected to5, original failure retained. Existing
tests were unchanged.

**Do not promote the prototype.** One62.218s default-tier public before/after pair,
Core-DLL-only swap and fixed Python/driver/subjects,28lanes/280samples>=48.368ms:

-837-byte Unicode Search/Match/FullMatch/text misses observe36–38% lower costs;
  Search0.832→0.525us. Long Unicode miss95.958→10.996us. These are candidate
  observations, not qualified source gains.
- Short public miss0.0573→0.0673us,+17.40%, exceeds the predeclared10%ceiling with
  stable1.005x/1.012x per-phase spreads. Short hits/full matches rise6.8–9.5%.
- Unchanged native Core Count moves−34.65%/−14.47%; unchanged .NET ASCII Match
  moves−37.45% predecoded/+10.71% decode. Several spreads exceed1.5x. Cause unproved;
  neither allocations (all unchanged) nor isolated kernel costs repair the gate.

No confirmation, retuning, extra eligibility mode, package qualification or CPython
Status refresh. Production file and both DLLs restored byte-identical to the
baseline; restored Python839pass. Core CD6BD90A292A1B23F7AAFC4E78C8C72E7739A6352CA0E2B522FE56A6BC3569E6;
Python7818965E903D8566BE0D36C8A324058DC9C5A3B7B66ED879AA825D3159053A73.
Keep the26production-behavior tests and closed evidence. No public/numeric policy
or lifetime revision. Frozen119 qualification, published comparisons and all older/
failed bytes remain unchanged. The upcoming release excludes this rejected code.

[Complete evidence](measurement.json), [rejected patch](rejected.patch),
[attribution fixture](Attribution.cs), [public fixture](PublicPair.cs).
Local predeclarations/build/failure/restoration/raw transport:
artifacts/literal-validation-attribution119-20261020.
Attribution archive28CC24F3BCE574F51087553B66025D2F5D84DC9416D125095DCCE4AF835B14F7;
public pair archiveA221A2C84DBD7D6C7424FDF77B4CC60DB2BEB92BB47518469EA30CB7CC8F86CA.
Broader R01–R10 remains active; release preparation now takes priority, with PLAN
work to pause after its final gates as requested. No native dependency, consumer
source/pin/default change or publication accompanies this experiment.

# Python coordinate comparison correction

Benchmark-only source `cc59d1fbe55448bd22ae87eccf50d9c90f0670e2`; all 16 producer DLL/PDB files remain
frozen `d7e28bd3` (dev.87). No API/engine/package change. PythonRe and CPython
consume preexisting valid unchanged text, with identical options and checked
outputs. No validation-policy matrix.

Simple value rows now project byte/UTF-16 ranges within timing, retaining the
native Match for lazy value access. Misses need no coordinate work. Detailed
and structural rows build only requested boundaries within the operation;
bulk conversion shares disjoint text segments, including lookaround captures
outside group zero. ASCII uses CPython's existing str representation flag and
identity coordinates, not a subject scan. Setup maps serve only untimed offset/
callback/byte-control verification. Boolean/count/string/callback/split timed
work is unchanged. These are equivalent requested public-output costs, not a
claim about bare native re.Match discovery.

Windows and Linux each verify all91 existing catalog digests.104 focused checks
over26 cases poison setup maps and preserve prior checksums/digests/consumption
tokens across scalar widths, offsets, flags, lookaround, absent/empty groups,
bulk output and misses. Both-runtime source snapshot guards and the existing
offline README writer pass, without builds or package qualification.

ONE selective refresh:39 affected cases and one unchanged string-output control,
9 alternating pairs/case,0 confirmations;186.852s within900s. Retain every status,
sample and uncertainty. Unselected cases and lifecycle/scaling records remain
unchanged. Candidate-position, capture-count and Unicode-coordinate-density
scaling retains the old preparation protocol and is explicitly historical.

Current PythonRe/CPython ratios include Unicode FullMatch0.1024, detailed search
0.3804, structural bulk0.3103 and detailed iteration0.2193. They are corrected
public-output observations, not producer speedups. The unchanged capture-string
control moved managed -23.74% and CPython
-11.93% between contexts; its current ratio is
1.7577. Do not attribute other old/new movements solely to
projection, subtract this control, or compare these as a controlled producer A/B.
Many-capture strings retain their original1.997 ratio/670.652us versus337.848us;
that is an actual remaining capture/search/output target.

Errata: first preflight stopped before any timing on Windows CRLF versus Linux
LF hashing; normalize text identity. After all40 comparisons completed, a receipt
assertion expected a40-character source rather than the reporter's12-character
commit. Full source/worker/PDB identities are verified offline; samples and
failure archive preserved, no retiming. Offline finalization renders/validates
the same raw data. [Acceptance and all observations](acceptance.json).

Raw verified offVM before keepers closed under
`artifacts/experiments/python-coordinate-fairness-20261114/`: main `61F69741E026F8CC1CCF6D243358AEFC8B93E58F43D68BC96A16C220287CA2B9`,
offline `F82C475C757E94EF163A4A0BF474BB86B042C4C6B378CA279FD72B6A60716D93`. All hosts/keepers are terminal.
The completed45m47 Full remains scoped to its original source/protocol, not a
new Full claim for this correction.

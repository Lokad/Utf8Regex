# Live consuming cells and ordered epsilon rows

One candidate, one <=120s comparison. Supersedes the unmeasured reverse-edge
worklist design before performance testing: retain the existing epsilon order,
filter its consuming/Accept/Fail cells at construction, and use next-row bits to
visit only eligible consuming cells. Two readonly masks fit the existing512B
carrier allowance. No predecessor array, extra forward pass, framework, density
switch or matching mode. Compact order uses existing before-allocation admission;
cache cost already consumes constructor reservations. Cold allocation/work can
change and must be measured. Invocation arrays/loans and exact numeric work stay.
All atom InputStep charges and one direct quantum per<=64cell row remain.

Baseline: frozenff018 runtime DLL/PDB, source code identical to currentee59
(which only adds tests/receipt), SHA CEF93A88D00525AC72C3012BAA9A6972B595684158AE1BF5672A74E3E77975B0.
Candidate: ee59 plus the archived two-file patch. Reuse matching baseline binaries;
do not rebuild merely to refresh Git metadata. Both datasets use canonical LF
region input46429B/SHA507F5A33C406D155D02A9633551C5231E9CF9890D250C1A5153045F224C688D2.

Fifteen public cases, quiet VM/.NET11RC3, CPUmask4, four equal-result lanes.
Count: actual region ordinary/compiled/live/finite/deadline; Unicode,wide,nullable,
dense ASCII/supplementary,category deadline; category IsMatch deadline control;
unchanged non-NB/literal Count. One typed public UTF8 Compile+first-empty-Count
case includes fresh construction/preparation every operation, with both before/
after non-NB cold-construction controls. That row labels its four producer lanes
explicitly; they are not predecoded/decode-then-.NET. Other rows retain those
.NET comparator lanes. No reflection in the timed cold calls.

Warm1s/min64, independent60ms calibration, five alternating samples>=20ms;
complete outputs consumed after timing. Setup excluded except the explicitly
cold row. At most one selected noise confirmation, then stop. Require material
primary gain (target>=15%) without demonstrated>10% adverse public-control/cold-
time regression; report actual cold bytes and shifted construction budget costs.
Revert a poor tradeoff, no variant campaign. No full package/catalog qualification.

Correctness: focused Release net10/net11 NB/capture/resource/construction/cache
and adapter checks; public shared-dependency cases;371 complete production table/
single-search probes (24pattern x15subject matrix plus11 prior cases), comparing
all bits, work, captures, peaks and final13B loan. Preserve width63/64/65+,
nullable scopes, original-token cancellation/deadlines and strict scalar input.

# Conservative parser hints and Unicode coverage

Linux CI exposed a stale requirement that every host char case variant have a
parser hint, with22 hardcoded exceptions. Hints are conservative; absent hints
retain owned scalar matching. Readonly.NET10/11 probes show five Unicode16 pairs
already match both ways without hints. Three newer Unicode17 pairs and the
asymmetric micro-sign example correctly remain distinct. Host NLS/ICU conversion
is not the owned matching contract. No public matcher defect was demonstrated.

Expanding the hints by10 entries passed focused Core683/Python131/PCRE41 checks
per runtime, but the one bounded public constructor/Count comparison rejects it:
34.505s, seven cases, five alternating samples, minimum48.620ms. Variable-width
Unicode Count regresses13.70% with tight spreads; affected constructors allocate
22272->23608B and22272->23568B. The noisy ASCII Count does not erase the stable
failure. See `rejected-summary.json`. No confirmation or variants; all producer
data changes are reverted.

The repair removes the obsolete completeness assertion and exception list.
The valid hint-subset compatibility check remains. New tests verify public
Unicode16 pairs, ordinary/compiled preparation, byte/scalar lengths and Count;
newer host casing cannot add Unicode17 pairs. PythonRe retains its Unicode15.1
rules. SPEC states the existing BMP/supplementary Unicode16 scope explicitly.
No matching algorithm, resource/ownership rule, switch or unchecked API changes.
Canonical Core685 checks pass per runtime; the Python131/PCRE41 checks also pass.
Final source CI is separate from the still-open dev.10 cumulative cost failure.

Raw records under `artifacts/experiments/case-coverage-20261102/`:

- `evidence.tar.gz`: SHA256 `934624ACE61232D7420F8A38D2B78E3FFE693B2DA88B00B6DF2CD6B79D32D0C6`.
- `rejected-source-correctness.zip`: SHA256 `AAA81AF246602FA242FE85DDBA55153476BA4EB5336C345C51D2F5EECAC94CFB`.

These preserve both DLL/PDB sets, driver/binaries, protocol, samples and setup
failures. The probe initially inherited the default TFM; preflight then passed
a DLL path where the driver expects a directory. Both were repaired before the
sole timing run. Touched whitespace was finalized before timing. None changed
the timing protocol. No package/platform matrix or full refresh was repeated.

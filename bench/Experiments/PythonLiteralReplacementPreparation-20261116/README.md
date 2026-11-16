# Literal replacement omits unused subject descriptions

Accepted: Core's Python replacement bridge checks subject validity under the
borrowed invocation's policy and checks replacement validity independently,
without deriving either UTF16 description. Delete the output character-count
parameter and its arithmetic; Python text output uses the existing result ledger
on completed bytes. Byte output needs no UTF16 count or ASCII classification.
ASCII needles still select complete scalars; discovery/copy algorithms, eligibility,
owned/retained results, basic metering and original interruption remain.

Short Unicode text uses the ledger's existing conservative byte-derived capacity;
it can deny earlier than exact sizing. Larger text keeps bounded exact sizing.
SPEC/profile README document this revision. Existing loan/projection tests now
use the real unknown-length path, dropping assertions for the removed metadata.

Windows and SAME-byte Linux per.NET10/11: focused Core2054 and full PythonRe5226
pass. Coverage includes strict malformed subjects, independent replacement errors
on misses, exact bytes/counts, owned aliases, capacity overlap, retained refunds,
finite work/depth/bytes and cancellation/deadlines. An initial test-host compile
found two direct callers of the removed parameter; only that host was corrected/
rebuilt. No producer rebuild or timing was repeated for the correction.

One105.356s ordinary-process ABBA/180s cap,0 confirmations. Twelve complete public
operations, normal8CPU startup thenCPU2,1s warmup,75ms calibration plus one
calibration check, five150ms-target samples/process/row, all>=20ms. Caller-valid
unchanged UTF8, equal options/complete bytes or strings plus replacement counts.
Native eligibility is asserted; finite/token and captured routes do not use the
changed bridge. Funded retained timing includes disposal and verifies parent loans.

| Public operation | Before us | After us | Change | Sample ranges |
|---|---:|---:|---:|---|
| Unicode byte miss,655360 bytes | 1238.570 | 99.558 | −91.96% | separated |
| Unicode byte hit | 1185.699 | 106.285 | −91.04% | separated |
| Unicode text miss | 1579.285 | 1587.173 | +.50% | overlapping |
| Unicode text hit | 1678.412 | 1696.294 | +1.07% | overlapping |
| Dense byte output | 62.620 | 53.997 | −13.77% | separated |
| Dense text output | 72.028 | 80.129 | +11.25% | separated |
| Funded retained bytes | 1298.266 | 108.372 | −91.65% | separated |
| Unchanged finite/token route | 165.690 | 169.350 | +2.21% | overlapping |
| Unchanged captured route | 145.375 | 147.382 | +1.38% | overlapping |
| Small Unicode text | .303 | .303 | −.16% | overlapping |
| Unchanged Core Count path | 12.258 | 12.164 | −.77% | overlapping |
| Identical predecoded.NET text | 104.532 | 97.392 | −6.83% | overlapping |

Intentionally retain the dense-text11.25% slowdown for the substantial byte-output
gains. No universal10% gate applies. Ordinary allocation medians agree; retained/
finite/.NET rows keep small amortized differences in raw samples. Do not normalize
controls, infer exact kernel shares, claim broad text gains or refresh native parity.
The byte work is removed directly; string output still requires sizing/decoding.

CoreFBBC8B3D→94F3F6E3, PythonAD728320→B2D9D946; actual393 compiler documents,
DLL/PDB/member/input and common caller/.NET identities verify. All676 older/dev.101
package files remain unchanged; no package/Full or canonical refresh. Original
worker/collector exit0, raw verified offVM, owned VM scratch retired after accepted
hash/zero references. Artifacts:
artifacts/experiments/python-literal-replacement-preparation-20261116/.
SHA256 raw C8C75FFAC696906F4AED7DC678374B6C087AE1D25C13BD0C9566D7726780BF88;
validation1523D412BE6FFAB7BC3BF454C645BDD2D9EEE1E964B38CA4724F017F45F25FA6.

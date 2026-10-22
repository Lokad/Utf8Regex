# Recovery ownership and PythonRe constructor cleanup

2026-10-22. Bounded execution of the twenty-fourth review at e8b8a22f. Development
stays 0.4.0-rc.2-dev. Commits 27230901 (recovery) and 4627fda4 (constructors);
final producer 4627fda41ca741e32a403614e1ffc0b08bfd5ce3. This is source evidence,
not a freshly qualified package. Six library DLL/PDB hashes are recorded.

## Scope recovery to executable ownership

test.ps1 previously selected testhost.exe processes by a command-line substring.
Paths such as utf8regex-copy passed the predicate, and an incidental reference to
this checkout could select an outside host. The existing recovery function now
normalizes the executable path and requires this checkout's directory prefix
including its separator. The existing test output contains testhost.exe under
tests/Lokad.Utf8Regex.PythonRe.Tests/bin/Release/net10.0, so the ordinary host is
eligible. No new process-management layer was introduced.

The intentional operational restriction is fail-closed ownership: unknown or
outside executable paths are left running even when that prevents automatic lock
recovery. Command-line mentions do not establish ownership. AGENTS.md documents
this. Recovery still runs only after a matching lock failure, with the existing
bounded retries and testhost.exe-only discovery; shared compiler servers stay
excluded. It does not claim to identify the actual lock owner or process age.

check-recovery.ps1.txt parses and invokes the production Stop-StaleProcesses AST
from the before and after files with mocked CIM, process lookup and stopping.
Eleven cases cover own paths, absent command line, casing, separators, normalized
traversal, neighboring copies, unrelated paths, incidental text and unknown
executable ownership. Before: six mismatches. After: zero. No real process was
discovered or terminated. before-test.ps1.txt and raw recovery-check.json retain
the inputs and result; the witness requires PowerShell on Windows.
To replay, copy the witness and before text into artifacts/<new-directory>/,
rename the witness to check-recovery.ps1, and invoke it there. The defaults resolve
the current repository test.ps1 two directories above that scratch directory.

## Delete constructor forwarding layers

Removed three short PythonReScalarMatchCursor constructors and the short
Utf8PythonMatchCursor constructor: 24 lines removed from shipped source. Existing
production constructors, all three scalar production call sites, the durable
factory and shared PythonReScalarMatchState remain unchanged. No algorithm,
storage layout, public API or resource/cancellation/deadline contract changed.
Thirty-four test constructions now pass the same explicit invocation and null
candidate to the actual production constructor. Existing guided candidates remain.
No test factory or replacement forwarding layer was added.

Review correction: the durable overload was test-only, not completely unused.
The initial text review missed three durable test callers and a target-typed
scalar construction. Compilation exposed them; all four were migrated rather
than retaining the overloads. The historical read-only assessment is not rewritten
to hide that correction. The resulting cleanup scope and acceptance are unchanged.

## Focused validation and delivery status

- Windows recovery AST witness: all 11 final cases pass; no real process actions.
- Focused Release PythonRe cursor/projection/authority/publication/output checks:
  460 pass. Full affected PythonRe suite: 3,910 pass in 18 seconds.
- Core public API/shared shipped-member dependency inventory: 13 pass. PythonRe
  public API and dependency checks also run in its full affected suite.
- One final matching benchmark/library closure compiles friend tooling. Existing
  README parity/PythonRe/PCRE2 generated-page checks and full producer/PE/PDB/source
  preflight pass. See compiled-documents.json and validation.json.
- Previous source CI 39814095402 is now confirmed completed/success; its earlier
  pending observation remains historical. Current source CI 39819948662 has both
  configured Windows/Linux jobs completed/success; see ci.json.
- immutable-files.json verifies four snapshots, nine frozen RC1 files and five
  original TODOs unchanged. PLAN/history remain ignored and preserve earlier work.

No timing campaign: only constructors without production callers were deleted;
production matching bodies/callers are unchanged. The script predicate executes
only during observed Windows lock recovery. No full local solution/configuration/
package matrix, normal server shutdown, benchmark measurement or VM campaign.
Existing benchmark evidence retains its original source scope; this cleanup does
not establish a new performance or package-wide claim.

Fresh immutable common-version packages, isolated consumers and actual-package
finite/cumulative operating-cost qualification remain the next release boundary.
These small findings do not justify another framework or broad cleanup gate.

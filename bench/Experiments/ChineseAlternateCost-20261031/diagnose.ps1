$ErrorActionPreference = 'Stop'
Set-Location /home/vermorel/tmp/utf8regex-net11-memory/pilot
./bench.ps1 -Configuration Release -SkipBuild -CommandArgs @('--inspect-case','literal-alternate/sherlock-zh')
if ($LASTEXITCODE) { exit $LASTEXITCODE }
./bench.ps1 -Configuration Release -SkipBuild -CommandArgs @('--inspect-exact-literal-family-case','literal-alternate/sherlock-zh')
if ($LASTEXITCODE) { exit $LASTEXITCODE }
./bench.ps1 -Configuration Release -SkipBuild -CommandArgs @('--inspect-utf8-validation-route','literal-alternate/sherlock-zh')
if ($LASTEXITCODE) { exit $LASTEXITCODE }
./bench.ps1 -Configuration Release -SkipBuild -CommandArgs @('--measure-count-attribution-case','literal-alternate/sherlock-zh','1000','5')
if ($LASTEXITCODE) { exit $LASTEXITCODE }

$ErrorActionPreference = 'Stop'
Set-Location /home/vermorel/tmp/utf8regex-net11-memory/pilot
./bench.ps1 -Configuration Release -SkipBuild -CommandArgs @('--inspect-utf8-validation-route','literal/sherlock-zh')
if ($LASTEXITCODE) { exit $LASTEXITCODE }
./bench.ps1 -Configuration Release -SkipBuild -CommandArgs @('--measure-unicode-literal-case','literal/sherlock-zh','2000')
if ($LASTEXITCODE) { exit $LASTEXITCODE }

<#
.SYNOPSIS
Summarize the archived single-group capture-trail comparison.
.DESCRIPTION
Requires terminal raw runs and the declaration beside this script; validates identities/results then writes cost-summary.json. Does not time execution.
#>
$ErrorActionPreference='Stop'
$p=$PSScriptRoot
$d=Get-Content (Join-Path $p 'comparison-declaration.json') -Raw|ConvertFrom-Json
if((Get-Content (Join-Path $p 'comparison-exit.txt') -Raw).Trim() -ne '0'){throw 'Comparison not terminal0'}
$runs=@(foreach($name in $d.Order){
 $r=Get-Content (Join-Path $p "$name.json") -Raw|ConvertFrom-Json
 $hash=if($name.StartsWith('before')){$d.CoreBefore}else{$d.CoreAfter}
 if($r.Worker -ne $name -or $r.CoreSha256 -ne $hash -or $r.PythonSha256 -ne $(if($name.StartsWith('before')){$d.PythonBefore}else{$d.PythonAfter}) -or $r.StartupProcessors -ne 8 -or $r.LoadedCoreAssemblies -ne 1 -or $r.Rows.Count -ne 12 -or $r.LoadedPythonAssemblies -ne 1){throw 'Wrong producer graph/inventory'}
 $r
})
function Median([double[]]$values){$v=@($values|Sort-Object);$n=$v.Count;if(!$n){throw 'Empty observations'};if($n%2){return $v[[int][Math]::Floor($n/2)]};return ($v[$n/2-1]+$v[$n/2])/2}
$rows=@(for($i=0;$i -lt 12;$i++){
 $e=@($runs|ForEach-Object{$_.Rows[$i]});$first=$e[0]
 $expected=$first.Expected|ConvertTo-Json -Depth 10 -Compress
 foreach($r in $e){
  if($r.Name -ne $first.Name -or $r.InputSha256 -ne $first.InputSha256 -or ($r.Expected|ConvertTo-Json -Depth 10 -Compress) -cne $expected -or $r.Microseconds.Count -ne 5 -or $r.ElapsedMilliseconds.Count -ne 5 -or $r.AllocatedBytes.Count -ne 5 -or @($r.ElapsedMilliseconds|Where-Object{$_ -lt 20}).Count){throw 'Wrong row/result/duration'}
 }
 $b=@($e[0].Microseconds)+@($e[3].Microseconds);$a=@($e[1].Microseconds)+@($e[2].Microseconds);$bm=Median $b;$am=Median $a
 [pscustomobject][ordered]@{Name=$first.Name;BeforeUs=$bm;AfterUs=$am;DeltaUs=$am-$bm;Percent=100*($am/$bm-1);
  BeforeBytes=Median (@($e[0].AllocatedBytes)+@($e[3].AllocatedBytes));AfterBytes=Median (@($e[1].AllocatedBytes)+@($e[2].AllocatedBytes));
  BeforeProcessMedians=@((Median $e[0].Microseconds),(Median $e[3].Microseconds));AfterProcessMedians=@((Median $e[1].Microseconds),(Median $e[2].Microseconds));
  BeforeMinimum=($b|Measure-Object -Minimum).Minimum;BeforeMaximum=($b|Measure-Object -Maximum).Maximum;
  AfterMinimum=($a|Measure-Object -Minimum).Minimum;AfterMaximum=($a|Measure-Object -Maximum).Maximum}
})
$ref=Get-Content (Join-Path $p 'comparators.json') -Raw|ConvertFrom-Json
if($ref.CoreSha256 -ne $d.CoreBefore -or $ref.Rows.Count -ne 2){throw 'Wrong reference graph'}
$references=@(foreach($r in $ref.Rows){
 $index=0
 if($r.InputSha256 -ne $runs[0].Rows[$index].InputSha256 -or ($r.Expected|ConvertTo-Json -Depth 10 -Compress) -cne ($runs[0].Rows[$index].Expected|ConvertTo-Json -Depth 10 -Compress) -or @($r.ElapsedMilliseconds|Where-Object{$_ -lt 20}).Count){throw 'Wrong reference result/duration'}
 [pscustomobject]@{Name=$r.Name;Microseconds=Median $r.Microseconds;AllocatedBytes=Median $r.AllocatedBytes;MinimumMilliseconds=($r.ElapsedMilliseconds|Measure-Object -Minimum).Minimum}
})
[ordered]@{Scope=$d.Scope;SourceParent=$d.SourceParent;ProducerVersions=@($runs.CoreVersion|Select-Object -Unique);CoreBefore=$d.CoreBefore;CoreAfter=$d.CoreAfter;PythonBefore=$d.PythonBefore;PythonAfter=$d.PythonAfter;
 Seconds=[double](Get-Content (Join-Path $p 'comparison-elapsed-nanoseconds.txt') -Raw)/1e9;Samples=240;
 MinimumSampleMilliseconds=($runs|ForEach-Object{$_.Rows}|ForEach-Object{$_.ElapsedMilliseconds}|Measure-Object -Minimum).Minimum;
 Confirmation='None';Rows=$rows;ReferenceRows=$references}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $p 'summary.json')
$rows|Format-Table Name,BeforeUs,AfterUs,Percent,BeforeBytes,AfterBytes -AutoSize

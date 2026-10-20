using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.PythonRe;
var policy=new Utf8ResourcePolicy{MaxWorkSteps=50000000,MaxDepth=1024,MaxWorkingBytes=64*1048576,MaxResultBytes=128*1048576};
using var tokens=new CancellationTokenSource();
var specs=new[]{
 (Name:"words-strings",Pattern:"[a-z]+",Text:string.Concat(Enumerable.Repeat("alpha beta gamma 123 ",1024)),Expected:3072,Operation:"strings",Values:new[]{"alpha","beta","gamma"}),
 (Name:"words-utf8",Pattern:"[a-z]+",Text:string.Concat(Enumerable.Repeat("alpha beta gamma 123 ",1024)),Expected:3072,Operation:"bytes",Values:new[]{"alpha","beta","gamma"}),
 (Name:"native-family-control",Pattern:"cat|dog",Text:string.Concat(Enumerable.Repeat("cat dog ",512)),Expected:1024,Operation:"strings",Values:new[]{"cat","dog"}),
};
var lanes=new List<(string Name,Func<int> Action,int Expected,Action Verify)>();
foreach(var spec in specs)foreach(var mode in new[]{"unlimited","finite","token"})
{
 var regex=new Utf8PythonRegex(spec.Pattern,PythonReCompileOptions.None,Regex.InfiniteMatchTimeout,mode=="unlimited"?default:policy);
 var input=Encoding.UTF8.GetBytes(spec.Text);var token=mode=="token"?tokens.Token:default;
 Utf8PythonFindAllResult kept=default;Utf8PythonFindAllUtf8Result bytes=default;Utf8PythonMatchData[] values=[];
 Func<int> action=spec.Operation switch{
 "bytes"=>()=>{bytes=regex.FindAllToUtf8(input,0,token);return bytes.Count;},
 "values"=>()=>{values=regex.FindAll(input,0,token);return values.Length;},
 _=>()=>{kept=regex.FindAllToStrings(input,0,token);return kept.Count;}};
 Action verify=()=>{
  if(action()!=spec.Expected)throw new Exception("Wrong count");
  var actual=spec.Operation switch{"bytes"=>bytes.ScalarValues.Select(Encoding.UTF8.GetString).ToArray(),"values"=>values.Select(x=>x.ValueText).ToArray(),_=>kept.ScalarValues};
  for(var i=0;i<actual.Length;i++)if(actual[i]!=spec.Values[i%spec.Values.Length])throw new Exception("Wrong public value");
  if(spec.Operation=="values")for(var i=0;i<values.Length;i++){
   var start=i/3*21+new[]{0,6,11}[i%3];
   if(values[i].StartOffsetInBytes!=start||values[i].EndOffsetInBytes!=start+actual[i].Length||values[i].StartOffsetInUtf16!=start||values[i].EndOffsetInUtf16!=start+actual[i].Length)throw new Exception("Wrong coordinates");
  }
 };
 verify();lanes.Add((spec.Name+"/"+mode,action,spec.Expected,verify));
}
foreach(var mode in new[]{"unlimited","finite","token"})
{
 var regex=new Utf8PythonRegex("(a)(b)",PythonReCompileOptions.None,Regex.InfiniteMatchTimeout,mode=="unlimited"?default:policy);
 var input=Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("ab ",1024)));var token=mode=="token"?tokens.Token:default;
 lanes.Add(("count-control/"+mode,()=>regex.Count(input,0,token),1024,()=>{if(regex.Count(input,0,token)!=1024)throw new Exception("Wrong Count");}));
}
// Comparator outputs have the same eager public shapes on these ASCII subjects.
foreach(var spec in specs.Where(x=>x.Name.StartsWith("words-")))foreach(var decode in new[]{false,true})
{
 var regex=new Regex(spec.Pattern,RegexOptions.CultureInvariant);var input=Encoding.UTF8.GetBytes(spec.Text);
 string[] strings=[];byte[][] bytes=[];Utf8PythonMatchData[] values=[];
 Func<int> action=spec.Operation switch{
 "bytes"=>()=>{var subject=decode?Encoding.UTF8.GetString(input):spec.Text;bytes=regex.Matches(subject).Select(x=>Encoding.UTF8.GetBytes(x.Value)).ToArray();return bytes.Length;},
 "values"=>()=>{var subject=decode?Encoding.UTF8.GetString(input):spec.Text;values=regex.Matches(subject).Select(x=>new Utf8PythonMatchData{Success=true,StartOffsetInBytes=x.Index,EndOffsetInBytes=x.Index+x.Length,StartOffsetInUtf16=x.Index,EndOffsetInUtf16=x.Index+x.Length,ValueText=x.Value}).ToArray();return values.Length;},
 _=>()=>{var subject=decode?Encoding.UTF8.GetString(input):spec.Text;strings=regex.Matches(subject).Select(x=>x.Value).ToArray();return strings.Length;}};
 Action verify=()=>{if(action()!=spec.Expected)throw new Exception("Wrong comparator count");var actual=spec.Operation switch{"bytes"=>bytes.Select(Encoding.UTF8.GetString).ToArray(),"values"=>values.Select(x=>x.ValueText).ToArray(),_=>strings};for(var i=0;i<actual.Length;i++)if(actual[i]!=spec.Values[i%spec.Values.Length])throw new Exception("Wrong comparator value");if(spec.Operation=="values")for(var i=0;i<values.Length;i++){var start=i/3*21+new[]{0,6,11}[i%3];if(!values[i].Success||values[i].StartOffsetInBytes!=start||values[i].StartOffsetInUtf16!=start||values[i].EndOffsetInBytes!=start+actual[i].Length||values[i].EndOffsetInUtf16!=start+actual[i].Length)throw new Exception("Wrong comparator coordinates");}};
 verify();lanes.Add((spec.Name+"/dotnet-"+(decode?"decode":"predecoded"),action,spec.Expected,verify));
}
foreach(var decode in new[]{false,true})
{
 var regex=new Regex("(a)(b)",RegexOptions.CultureInvariant);var text=string.Concat(Enumerable.Repeat("ab ",1024));var input=Encoding.UTF8.GetBytes(text);
 Func<int> action=()=>regex.Count(decode?Encoding.UTF8.GetString(input):text);
 lanes.Add(("count-control/dotnet-"+(decode?"decode":"predecoded"),action,1024,()=>{if(action()!=1024)throw new Exception("Wrong comparator Count");}));
}

// Existing finite-enumeration fixture, reduced to one changed representative subject.
var branchText=string.Concat(Enumerable.Repeat(" xx b12 ab34 ",1000));
var branchInput=Encoding.UTF8.GetBytes(branchText);var branchOracle=new Regex("a?b[0-9]{2}",RegexOptions.CultureInvariant);
var branchExpected=ConsumeOracle(branchOracle,branchText);
foreach(var mode in new[]{"unlimited","finite","token","deadline"})
{
 var regex=new Utf8Regex("a?b[0-9]{2}",RegexOptions.CultureInvariant,mode=="deadline"?TimeSpan.FromSeconds(10):Regex.InfiniteMatchTimeout,mode=="unlimited"?default:policy);
 var token=mode=="token"?tokens.Token:default;
 foreach(var countOnly in new[]{true,false})
 {
  Func<int> action=countOnly?()=>regex.Count(branchInput,token):()=>Consume(regex,branchInput,token);
  var expected=countOnly?2000:branchExpected;
  lanes.Add(("branch-"+(countOnly?"count":"enumerate")+"/"+mode,action,expected,()=>{if(action()!=expected)throw new Exception("Wrong branch output");}));
 }
}
foreach(var decode in new[]{false,true})foreach(var countOnly in new[]{true,false})
{
 Func<int> action=countOnly?()=>branchOracle.Count(decode?Encoding.UTF8.GetString(branchInput):branchText):()=>ConsumeOracle(branchOracle,decode?Encoding.UTF8.GetString(branchInput):branchText);
 var expected=countOnly?2000:branchExpected;
 lanes.Add(("branch-"+(countOnly?"count":"enumerate")+"/dotnet-"+(decode?"decode":"predecoded"),action,expected,()=>{if(action()!=expected)throw new Exception("Wrong branch comparator");}));
}
// Captured eager results retain both returned group arrays.
var tupleText=string.Concat(Enumerable.Repeat("ab ",1024));var tupleInput=Encoding.UTF8.GetBytes(tupleText);
foreach(var mode in new[]{"unlimited","finite","token"})
{
 var regex=new Utf8PythonRegex("(a)(b)",PythonReCompileOptions.None,Regex.InfiniteMatchTimeout,mode=="unlimited"?default:policy);
 var token=mode=="token"?tokens.Token:default;Utf8PythonFindAllUtf8Result kept=default;
 Func<int> action=()=>{kept=regex.FindAllToUtf8(tupleInput,0,token);return kept.Count;};
 Action verify=()=>{if(action()!=1024||kept.TupleValues.Length!=1024)throw new Exception("Wrong captured count");foreach(var row in kept.TupleValues)if(row.Length!=2||!row[0].AsSpan().SequenceEqual("a"u8)||!row[1].AsSpan().SequenceEqual("b"u8))throw new Exception("Wrong captures");};
 lanes.Add(("captured-utf8/"+mode,action,1024,verify));
}
foreach(var decode in new[]{false,true})
{
 var regex=new Regex("(a)(b)",RegexOptions.CultureInvariant);byte[][][] kept=[];
 Func<int> action=()=>{kept=regex.Matches(decode?Encoding.UTF8.GetString(tupleInput):tupleText).Select(x=>new[]{Encoding.UTF8.GetBytes(x.Groups[1].Value),Encoding.UTF8.GetBytes(x.Groups[2].Value)}).ToArray();return kept.Length;};
 Action verify=()=>{if(action()!=1024)throw new Exception("Wrong captured comparator count");foreach(var row in kept)if(row.Length!=2||!row[0].AsSpan().SequenceEqual("a"u8)||!row[1].AsSpan().SequenceEqual("b"u8))throw new Exception("Wrong comparator captures");};
 lanes.Add(("captured-utf8/dotnet-"+(decode?"decode":"predecoded"),action,1024,verify));
}

// This boundary retains twelve existing consumer lanes; other setup is untimed.
lanes.RemoveAll(x=>!(x.Name.StartsWith("words-utf8/")||x.Name.StartsWith("captured-utf8/")||x.Name.StartsWith("count-control/"))||x.Name.EndsWith("/token"));
var longPart=new string('a',8193);
var currentCases=new[]{
 (Id:"pair-count",Pattern:"a?b[0-9]{2}",Text:string.Concat(Enumerable.Repeat(" xx b12 ab34 ",1000)),Rtl:false,Start:-1,Expected:2000),
 (Id:"rtl-strict-miss",Pattern:@"\A\k<g>(?<g>.{8193})\z",Text:"b"+longPart[1..]+longPart,Rtl:true,Start:-1,Expected:0),
 (Id:"rtl-empty",Pattern:@"\z",Text:new string('a',65536),Rtl:true,Start:-1,Expected:1),
 (Id:"rtl-offset",Pattern:@"\A\k<g>(?<g>.{8193})\z",Text:longPart+longPart,Rtl:true,Start:8193,Expected:0),
 (Id:"literal-control",Pattern:"needle",Text:new string('x',65536)+"needle",Rtl:false,Start:-1,Expected:1)
};
foreach(var item in currentCases){
 var flags=RegexOptions.CultureInvariant|(item.Rtl?RegexOptions.RightToLeft:0);
 var input=Encoding.UTF8.GetBytes(item.Text);var oracle=new Regex(item.Pattern,flags);
 foreach(var mode in item.Id=="literal-control"?new[]{"unlimited","finite"}:new[]{"unlimited","finite","token","deadline"}){
  var regex=new Utf8Regex(item.Pattern,flags,mode=="deadline"?TimeSpan.FromSeconds(10):Regex.InfiniteMatchTimeout,mode=="unlimited"?default:policy);
  var token=mode=="token"?tokens.Token:default;
  Func<int> action=item.Start<0?()=>regex.Count(input,token):()=>regex.CountFromUtf16Offset(input,item.Start,token);
  Action verify=()=>{if(action()!=item.Expected)throw new Exception("Current public Count mismatch: "+item.Id);};
  verify();lanes.Add((item.Id+"/"+mode,action,item.Expected,verify));
 }
 foreach(var decode in new[]{false,true}){
  Func<int> action=item.Start<0?()=>oracle.Count(decode?Encoding.UTF8.GetString(input):item.Text):()=>CountOracleFrom(oracle,decode?Encoding.UTF8.GetString(input):item.Text,item.Start);
  Action verify=()=>{if(action()!=item.Expected)throw new Exception("Current oracle mismatch: "+item.Id);};
  verify();lanes.Add((item.Id+"/dotnet-"+(decode?"decode":"predecoded"),action,item.Expected,verify));
 }
}

foreach(var lane in lanes)lane.Verify();

void Emit(object x)=>Console.WriteLine(JsonSerializer.Serialize(x));
Emit(new{Kind="identity",Runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,Core=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Utf8Regex).Assembly.Location))),Python=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Utf8PythonRegex).Assembly.Location)))});
if(args.Contains("--preflight")){foreach(var lane in lanes){lane.Verify();Emit(new{Kind="preflight",lane.Name,lane.Expected});}return;}
(long Elapsed,long Allocated) Measure(Func<int> action,int iterations,int expected)
{
 var allocated=GC.GetAllocatedBytesForCurrentThread();var start=Stopwatch.GetTimestamp();var sink=0;
 for(var i=0;i<iterations;i++){var value=action();if(value!=expected)throw new Exception("Unexpected timed count");sink^=value;}
 var elapsed=Stopwatch.GetTimestamp()-start;allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;GC.KeepAlive(sink);return(elapsed,allocated);
}
var iterations=new int[lanes.Count];
for(var i=0;i<lanes.Count;i++)
{
 var lane=lanes[i];var warm=Stopwatch.StartNew();var calls=0;do{lane.Action();calls++;}while(calls<10005||warm.Elapsed.TotalMilliseconds<500);Emit(new{Kind="warmup",lane.Name,Calls=calls,ElapsedMs=warm.Elapsed.TotalMilliseconds});
 var count=1;long elapsed;
 do{var m=Measure(lane.Action,count,lane.Expected);elapsed=m.Elapsed;if(elapsed*1000.0/Stopwatch.Frequency<50)count=(int)Math.Min(10000000L,count*2L);else break;}while(count<10000000);
 iterations[i]=count;
}
// Existing finite-cost diagnostic confirms settled duration after all lanes warmed.
for(var i=0;i<lanes.Count;i++)
{
 var fastest=double.PositiveInfinity;for(var confirmation=0;confirmation<3;confirmation++){GC.Collect();GC.WaitForPendingFinalizers();var measured=Measure(lanes[i].Action,iterations[i],lanes[i].Expected);fastest=Math.Min(fastest,measured.Elapsed*1000.0/Stopwatch.Frequency);}
 iterations[i]=(int)Math.Clamp(Math.Ceiling(iterations[i]*50/Math.Max(.001,fastest)),iterations[i],10000000);
}
for(var sample=0;sample<5;sample++)for(var position=0;position<lanes.Count;position++)
{
 var index=(sample&1)==0?position:lanes.Count-1-position;var lane=lanes[index];GC.Collect();GC.WaitForPendingFinalizers();
 var measured=Measure(lane.Action,iterations[index],lane.Expected);
 Emit(new{Kind="sample",lane.Name,Sample=sample,Iterations=iterations[index],ElapsedMs=measured.Elapsed*1000.0/Stopwatch.Frequency,Nanoseconds=measured.Elapsed*1000000000.0/Stopwatch.Frequency/iterations[index],Allocated=measured.Allocated/(double)iterations[index]});
}
foreach(var lane in lanes)lane.Verify();

static int Consume(Utf8Regex regex,byte[] input,CancellationToken token){var sum=0;foreach(var match in regex.EnumerateMatches(input,token))sum=unchecked(sum+1+match.IndexInBytes+match.LengthInBytes);return sum;}
static int ConsumeOracle(Regex regex,string text){var sum=0;foreach(var match in regex.EnumerateMatches(text))sum=unchecked(sum+1+match.Index+match.Length);return sum;}

static int CountOracleFrom(Regex regex,string text,int start){var count=0;foreach(var match in regex.EnumerateMatches(text,start))count++;return count;}

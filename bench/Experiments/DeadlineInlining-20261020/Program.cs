using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.PythonRe;
using Lokad.Utf8Regex.Pcre2;
using OwnedRegex = Lokad.Utf8Regex.Utf8Regex;

var verifyOnly=args[0]=="--verify";
using var output = new StreamWriter(File.Open(verifyOnly?args[1]:args[0], FileMode.CreateNew)) { AutoFlush = true };
void Emit(object value) => output.WriteLine(JsonSerializer.Serialize(value));
var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(OwnedRegex).Assembly.Location)));
Emit(new { Kind="identity", CoreSha256=hash, Runtime=Environment.Version.ToString(),
    HarnessSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Program).Assembly.Location))),
    Tiering=Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") });
var policy = new Utf8ResourcePolicy { MaxWorkSteps=50_000_000, MaxDepth=1024,
    MaxWorkingBytes=64UL*1048576, MaxResultBytes=128UL*1048576 };
var lanes = new List<Lane>();
using var live = new CancellationTokenSource();
foreach (var item in new[] {
    (Id:"optional-hit",Pattern:"a?b[0-9]{2}",Unit:" xx b12 ab34 ",Expected:2000),
    (Id:"optional-miss",Pattern:"a?b[0-9]{2}",Unit:" xx bxx abxx ",Expected:0),
    (Id:"fixed-control",Pattern:"ab[0-9]{2}",Unit:" xx ab12 ab34 ",Expected:2000),
    (Id:"family-exact",Pattern:@"\b(?:target|needle)\b",Unit:" xx target needle notarget xx ",Expected:2000),
    (Id:"family-folded",Pattern:@"(?i:\b(?:target|needle)\b)",Unit:" xx TaRgEt NEEDLE notarget xx ",Expected:2000),
    (Id:"family-unicode",Pattern:@"\b(?:café|niño)\b",Unit:" xx café niño uncafé xx ",Expected:2000) })
foreach (var compiled in new[] {false,true})
foreach (var mode in new[] {"unlimited","finite","live","deadline"})
{
    var text=string.Concat(Enumerable.Repeat(item.Unit,1000)); var input=Encoding.UTF8.GetBytes(text);
    var options=RegexOptions.CultureInvariant | (compiled?RegexOptions.Compiled:RegexOptions.None);
    var finite=mode!="unlimited";
    var token=mode=="live"?live.Token:CancellationToken.None;
    var timeout=mode=="deadline"?TimeSpan.FromSeconds(10):Regex.InfiniteMatchTimeout;
    var regex=new OwnedRegex(item.Pattern,options,timeout,finite?policy:Utf8ResourcePolicy.Unlimited);
    var oracle=new Regex(item.Pattern,RegexOptions.CultureInvariant);
    var expected=oracle.Match(text); var match=regex.Match(input);
    if(oracle.Count(text)!=item.Expected || regex.Count(input)!=item.Expected || regex.IsMatch(input)!=(item.Expected>0) ||
        match.Success!=expected.Success || match.Success&&(match.IndexInBytes!=Encoding.UTF8.GetByteCount(text.AsSpan(0,expected.Index)) || match.LengthInBytes!=Encoding.UTF8.GetByteCount(expected.Value))) throw new Exception("Public preflight mismatch");
    var name=$"{item.Id}/{(compiled?"compiled":"ordinary")}/{mode}";
    lanes.Add(new(name,()=>regex.Count(input,token),item.Expected));
    Emit(new {Kind="preflight",Lane=name,Bytes=input.Length,Count=item.Expected,First=match.Success?match.IndexInBytes:-1,Length=match.Success?match.LengthInBytes:0,
        SubjectSha256=Convert.ToHexString(SHA256.HashData(input)),
        UnitMinimumWork=MinimumWork(item.Pattern,item.Unit,options,policy)});
}
foreach(var hit in new[]{true,false}) {
 var pattern="a?b[0-9]{2}";
 var text=string.Concat(Enumerable.Repeat(hit?" xx b12 ab34 ":" xx bxx abxx ",1000));
 var input=Encoding.UTF8.GetBytes(text);var expected=hit?2000:0;var id=hit?"optional-hit":"optional-miss";
 foreach(var mode in new[]{"unlimited","finite","live","deadline"}) {
  var token=mode=="live"?live.Token:CancellationToken.None;
  var timeout=mode=="deadline"?TimeSpan.FromSeconds(10):Regex.InfiniteMatchTimeout;
  var limits=mode=="unlimited"?Utf8ResourcePolicy.Unlimited:policy;
  var python=new Utf8PythonRegex(pattern,PythonReCompileOptions.None,timeout,limits);
  var pcre=Utf8Pcre2Regex.Create(pattern,Pcre2CompileOptions.None,default,limits,timeout);
  lanes.Add(new($"python/{id}/{mode}",()=>python.Count(input,0,token),expected));
  lanes.Add(new($"pcre/{id}/{mode}",()=>pcre.Count(input,0,Pcre2MatchOptions.None,token),expected));
 }
 var oracle=new Regex(pattern,RegexOptions.CultureInvariant);
 lanes.Add(new($"dotnet/{id}/predecoded",()=>oracle.Count(text),expected));
 lanes.Add(new($"dotnet/{id}/decode",()=>oracle.Count(Encoding.UTF8.GetString(input)),expected));
}
foreach(var lane in lanes) {
 if(lane.Call()!=lane.Expected)throw new Exception("Lane preflight mismatch: "+lane.Name);
 Emit(new {Kind="lane-preflight",Lane=lane.Name,Expected=lane.Expected});
}
if(verifyOnly) { Emit(new {Kind="complete",VerifyOnly=true}); return; }
var counts=new Dictionary<string,int>();
foreach(var lane in lanes)
{
    var warm=Stopwatch.StartNew(); do { Sink.Value=lane.Call(); } while(warm.ElapsedMilliseconds<120);
    var n=32;
    for(var attempt=0;attempt<6;attempt++) {
        var elapsed=Measure(lane,n).Ms;
        if(elapsed>=65 && elapsed<=150)break;
        n=(int)Math.Clamp(Math.Ceiling(n*80/Math.Max(.001,elapsed)),1,2_000_000);
    }
    counts[lane.Name]=n;
}
for(var ordinal=0;ordinal<5;ordinal++)
foreach(var lane in ordinal%2==0?lanes:lanes.AsEnumerable().Reverse()) {
    var sample=Measure(lane,counts[lane.Name]);
    Emit(new {Kind="sample",Lane=lane.Name,Ordinal=ordinal,sample.Ns,sample.Bytes,sample.Ms,Iterations=counts[lane.Name]});
}
Emit(new {Kind="complete"});
static (double Ns,double Bytes,double Ms) Measure(Lane lane,int count) {
    var bytes=GC.GetAllocatedBytesForCurrentThread(); var start=Stopwatch.GetTimestamp(); var sum=0;
    for(var index=0;index<count;index++)sum=unchecked(sum+lane.Call());
    var ms=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    var allocated=GC.GetAllocatedBytesForCurrentThread()-bytes;
    if(sum!=unchecked(count*lane.Expected))throw new Exception("Count mismatch");
    Sink.Value=sum;return(ms*1e6/count,(double)allocated/count,ms);
}

static ulong MinimumWork(string pattern,string text,RegexOptions options,Utf8ResourcePolicy policy) {
    var input=Encoding.UTF8.GetBytes(text);
    ulong low=1, high=1000000;
    while(low<high) {
        var middle=low+(high-low)/2;
        var regex=new OwnedRegex(pattern,options,Regex.InfiniteMatchTimeout,policy with {MaxWorkSteps=middle});
        try { regex.Count(input); high=middle; }
        catch(Utf8ResourceLimitExceededException error) when(error.ResourceKind==Utf8ResourceLimitKind.ExecutionWorkSteps) { low=middle+1; }
    }
    return low;
}

record Lane(string Name,Func<int> Call,int Expected);
static class Sink {internal static int Value;}

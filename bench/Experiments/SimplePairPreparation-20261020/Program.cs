using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.PythonRe;
using Lokad.Utf8Regex.Pcre2;
using OwnedRegex=Lokad.Utf8Regex.Utf8Regex;
var verifyOnly=args[0]=="--verify";
using var output=new StreamWriter(File.Open(verifyOnly?args[1]:args[0],FileMode.CreateNew)){AutoFlush=true};
void Emit(object x)=>output.WriteLine(JsonSerializer.Serialize(x));
Emit(new{Kind="identity",CoreSha256=Hash(typeof(OwnedRegex).Assembly.Location),DriverSha256=Hash(typeof(Program).Assembly.Location),Runtime=Environment.Version.ToString(),Tiering=Environment.GetEnvironmentVariable("DOTNET_TieredCompilation")});
var policy=new Utf8ResourcePolicy{MaxWorkSteps=50_000_000,MaxDepth=1024,MaxWorkingBytes=64UL*1048576,MaxResultBytes=128UL*1048576};
using var live=new CancellationTokenSource();var lanes=new List<Lane>();
var repeated=string.Concat(Enumerable.Repeat(" xx b12 ab34 ",1000));
foreach(var item in new[]{
 (Id:"hit",Pattern:"a?b[0-9]{2}",Text:repeated),
 (Id:"miss",Pattern:"a?b[0-9]{2}",Text:string.Concat(Enumerable.Repeat(" xx bxx abxx ",1000))),
 (Id:"dense-false-hit",Pattern:"a?b[0-9]{2}",Text:new string('a',4096)+"b12"),
 (Id:"short-hit",Pattern:"a?b[0-9]{2}",Text:"b12 ab34"),
 (Id:"same-head-hit",Pattern:"a{1,2}b[0-9]{2}",Text:string.Concat(Enumerable.Repeat(" xx ab12 aab34 ",1000)))})
foreach(var compiled in new[]{false,true})
foreach(var mode in compiled?new[]{"unlimited","finite"}:new[]{"unlimited","finite","live"}) {
 var options=RegexOptions.CultureInvariant|(compiled?RegexOptions.Compiled:RegexOptions.None);
 var regex=new OwnedRegex(item.Pattern,options,Regex.InfiniteMatchTimeout,mode=="unlimited"?Utf8ResourcePolicy.Unlimited:policy);
 var oracle=new Regex(item.Pattern,RegexOptions.CultureInvariant);var input=Encoding.UTF8.GetBytes(item.Text);
 var token=mode=="live"?live.Token:CancellationToken.None;var name=$"{item.Id}/{(compiled?"compiled":"ordinary")}/{mode}";
 var count=oracle.Count(item.Text);var ranges=ConsumeOracle(oracle,item.Text);var expected=oracle.Match(item.Text);var actual=regex.Match(input);
 if(regex.Count(input,token)!=count||Consume(regex,input,token)!=ranges||actual.Success!=expected.Success||actual.Success&&(actual.IndexInBytes!=expected.Index||actual.LengthInBytes!=expected.Length))throw new Exception("Preflight mismatch: "+name);
 Emit(new{Kind="preflight",Lane=name,Bytes=input.Length,Count=count,RangeChecksum=ranges,First=actual.Success?actual.IndexInBytes:-1,Length=actual.Success?actual.LengthInBytes:0,SubjectSha256=Convert.ToHexString(SHA256.HashData(input))});
 lanes.Add(new(name+"/count",()=>regex.Count(input,token),count));
 lanes.Add(new(name+"/enumerate",()=>Consume(regex,input,token),ranges));
}
foreach(var compiled in new[]{false,true}) {
 var options=RegexOptions.CultureInvariant|(compiled?RegexOptions.Compiled:RegexOptions.None);
 var regex=new OwnedRegex("(?:a|c)?b[0-9]{2}",options);var input=Encoding.UTF8.GetBytes(repeated);
 lanes.Add(new("three-neighbor/"+(compiled?"compiled":"ordinary"),()=>regex.Count(input),2000));
}
var adapterInput=Encoding.UTF8.GetBytes(repeated);
foreach(var finite in new[]{false,true}) {
 var limits=finite?policy:Utf8ResourcePolicy.Unlimited;
 var python=new Utf8PythonRegex("a?b[0-9]{2}",PythonReCompileOptions.None,Regex.InfiniteMatchTimeout,limits);
 var pcre=Utf8Pcre2Regex.Create("a?b[0-9]{2}",Pcre2CompileOptions.None,default,limits,Regex.InfiniteMatchTimeout);
 lanes.Add(new("python/"+(finite?"finite":"unlimited"),()=>python.Count(adapterInput),2000));
 lanes.Add(new("pcre/"+(finite?"finite":"unlimited"),()=>pcre.Count(adapterInput),2000));
}
var control=new Regex("a?b[0-9]{2}",RegexOptions.CultureInvariant);
lanes.Add(new("dotnet/predecoded/count",()=>control.Count(repeated),2000));
lanes.Add(new("dotnet/decode/count",()=>control.Count(Encoding.UTF8.GetString(adapterInput)),2000));
var controlRanges=ConsumeOracle(control,repeated);
lanes.Add(new("dotnet/predecoded/enumerate",()=>ConsumeOracle(control,repeated),controlRanges));
lanes.Add(new("dotnet/decode/enumerate",()=>ConsumeOracle(control,Encoding.UTF8.GetString(adapterInput)),controlRanges));
foreach(var lane in lanes){if(lane.Call()!=lane.Expected)throw new Exception("Lane mismatch: "+lane.Name);Emit(new{Kind="lane-preflight",Lane=lane.Name,Expected=lane.Expected});}
if(verifyOnly){Emit(new{Kind="complete",VerifyOnly=true});return;}
var counts=new Dictionary<string,int>();
foreach(var lane in lanes) {
 var warm=Stopwatch.StartNew();do{Sink.Value=lane.Call();}while(warm.ElapsedMilliseconds<120);
 var n=32;
 for(var attempt=0;attempt<6;attempt++) {
  var elapsed=Measure(lane,n).Ms;if(elapsed>=65&&elapsed<=150)break;
  n=(int)Math.Clamp(Math.Ceiling(n*80/Math.Max(.001,elapsed)),1,2_000_000);
 }
 counts[lane.Name]=n;
}
for(var ordinal=0;ordinal<5;ordinal++)
foreach(var lane in ordinal%2==0?lanes:lanes.AsEnumerable().Reverse()) {
 var sample=Measure(lane,counts[lane.Name]);Emit(new{Kind="sample",Lane=lane.Name,Ordinal=ordinal,sample.Ns,sample.Bytes,sample.Ms,Iterations=counts[lane.Name]});
}
Emit(new{Kind="complete"});
static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
static int Consume(OwnedRegex regex,byte[] input,CancellationToken token) {
 var sum=0;foreach(var match in regex.EnumerateMatches(input,token))sum=unchecked(sum+1+match.IndexInBytes+match.LengthInBytes);return sum;
}
static int ConsumeOracle(Regex regex,string input) {
 var sum=0;foreach(var match in regex.EnumerateMatches(input))sum=unchecked(sum+1+match.Index+match.Length);return sum;
}
static(double Ns,double Bytes,double Ms)Measure(Lane lane,int count) {
 var bytes=GC.GetAllocatedBytesForCurrentThread();var start=Stopwatch.GetTimestamp();var sum=0;
 for(var i=0;i<count;i++)sum=unchecked(sum+lane.Call());
 var ms=Stopwatch.GetElapsedTime(start).TotalMilliseconds;var allocated=GC.GetAllocatedBytesForCurrentThread()-bytes;
 if(sum!=unchecked(count*lane.Expected))throw new Exception("Timed mismatch: "+lane.Name);
 Sink.Value=sum;return(ms*1e6/count,(double)allocated/count,ms);
}
record Lane(string Name,Func<int> Call,int Expected);
static class Sink{internal static int Value;}

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex;
using OwnedRegex=Lokad.Utf8Regex.Utf8Regex;
var verifyOnly=args[0]=="--verify";
using var output=new StreamWriter(File.Open(verifyOnly?args[1]:args[0],FileMode.CreateNew)){AutoFlush=true};
void Emit(object x)=>output.WriteLine(JsonSerializer.Serialize(x));
Emit(new{Kind="identity",CoreSha256=Hash(typeof(OwnedRegex).Assembly.Location),DriverSha256=Hash(typeof(Program).Assembly.Location),Runtime=Environment.Version.ToString(),Tiering=Environment.GetEnvironmentVariable("DOTNET_TieredCompilation")});
var policy=new Utf8ResourcePolicy{MaxWorkSteps=50_000_000,MaxDepth=1024,MaxWorkingBytes=64UL*1048576,MaxResultBytes=128UL*1048576};
var strict=new UTF8Encoding(false,true);using var live=new CancellationTokenSource();var lanes=new List<Lane>();
var longPart=new string('a',8193);
var specs=new[]{new Case("short-hit","a",8,true,false),new Case("long-hit","a",8193,true,false),new Case("first-difference","a",8193,false,false),new Case("last-difference","a",8193,false,false),new Case("scalar-hit","😀é",1025,true,true),new Case("empty","a",0,true,false)};
foreach(var spec in specs)foreach(var rtl in new[]{false,true}) {
 var part=string.Concat(Enumerable.Repeat(spec.Part,spec.Repeats));
 var reference=spec.Name=="first-difference"?"b"+part[1..]:spec.Name=="last-difference"?part[..^1]+"b":part;
 var text=rtl?reference+part:part+reference;var input=strict.GetBytes(text);
 var body=spec.Scalar?$"(?<g>(?:{spec.Part}){{{spec.Repeats}}})":$"(?<g>.{{{spec.Repeats}}})";
 var pattern=rtl?$@"\A\k<g>{body}\z":$@"\A{body}\k<g>\z";
 var flags=RegexOptions.CultureInvariant|(rtl?RegexOptions.RightToLeft:0);var oracle=new Regex(pattern,flags);
 if(oracle.Count(text)!=(spec.Hit?1:0))throw new Exception("Wrong oracle case");
 foreach(var mode in spec.Scalar||spec.Name=="empty"?new[]{"finite"}:new[]{"unlimited","finite","live"}) {
  var regex=new OwnedRegex(pattern,flags,Regex.InfiniteMatchTimeout,mode=="unlimited"?default:policy);
  var token=mode=="live"?live.Token:default;var name=$"{spec.Name}/{(rtl?"rtl":"ltr")}/{mode}";
  Verify(regex,input,part,rtl,spec.Hit,token);
  Emit(new{Kind="preflight",Lane=name,Bytes=input.Length,Count=spec.Hit?1:0,SubjectSha256=Convert.ToHexString(SHA256.HashData(input)),Pattern=pattern});
  lanes.Add(new(name+"/count",()=>regex.Count(input,token),spec.Hit?1:0));
  if(spec.Name=="long-hit"&&mode!="live")lanes.Add(new(name+"/detailed",()=>Detailed(regex,input,token),1+input.Length+strict.GetByteCount(part)+(rtl?strict.GetByteCount(part):0)));
  if(spec.Name=="long-hit"&&mode=="finite")lanes.Add(new(name+"/enumerate",()=>Consume(regex,input,token),1+input.Length));
 }
 if(spec.Name=="long-hit") {
  var compiled=new OwnedRegex(pattern,flags|RegexOptions.Compiled,Regex.InfiniteMatchTimeout,policy);
  var deadline=new OwnedRegex(pattern,flags,TimeSpan.FromSeconds(10),policy);
  lanes.Add(new($"long-hit/{(rtl?"rtl":"ltr")}/compiled-finite/count",()=>compiled.Count(input),1));
  lanes.Add(new($"long-hit/{(rtl?"rtl":"ltr")}/deadline/count",()=>deadline.Count(input),1));
 }
 if(!spec.Scalar&&spec.Name!="empty"||!rtl)foreach(var decode in new[]{false,true}) {
  lanes.Add(new($"{spec.Name}/{(rtl?"rtl":"ltr")}/dotnet-{(decode?"decode":"predecoded")}",()=>oracle.Count(decode?strict.GetString(input):text),spec.Hit?1:0));
 }
}
foreach(var rtl in new[]{false,true})foreach(var finite in new[]{false,true}) {
 var body=$"(?<g>.{{8193}})";var pattern=rtl?$@"\A\k<g>{body}\z":$@"\A{body}\k<g>\z";
 var flags=RegexOptions.CultureInvariant|RegexOptions.IgnoreCase|(rtl?RegexOptions.RightToLeft:0);
 var text=rtl?new string('A',8193)+longPart:longPart+new string('A',8193);var input=strict.GetBytes(text);
 var regex=new OwnedRegex(pattern,flags,Regex.InfiniteMatchTimeout,finite?policy:default);
 Verify(regex,input,longPart,rtl,true,default);
 lanes.Add(new($"folded-control/{(rtl?"rtl":"ltr")}/{(finite?"finite":"unlimited")}",()=>regex.Count(input),1));
}
foreach(var finite in new[]{false,true})foreach(var kind in new[]{"generic-vm","literal","optional-pair"}) {
 var pattern=kind=="generic-vm"?@"\A(?<g>.{8193})(?=.{8193}\z).*\z":kind=="literal"?"needle":"a?b[0-9]{2}";
 var text=kind=="generic-vm"?longPart+longPart:kind=="literal"?new string('x',65536)+"needle":string.Concat(Enumerable.Repeat(" xx b12 ab34 ",1000));
 var input=strict.GetBytes(text);var regex=new OwnedRegex(pattern,RegexOptions.CultureInvariant,Regex.InfiniteMatchTimeout,finite?policy:default);
 lanes.Add(new($"{kind}-control/{(finite?"finite":"unlimited")}",()=>regex.Count(input),kind=="optional-pair"?2000:1));
}
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
static void Verify(OwnedRegex regex,byte[] input,string captured,bool rtl,bool success,CancellationToken token) {
 if(regex.Count(input,token)!=(success?1:0))throw new Exception("Count mismatch");
 var match=regex.MatchDetailed(input,token);if(match.Success!=success)throw new Exception("Success mismatch");
 if(!success)return;var group=match.GetGroup(1);var length=Encoding.UTF8.GetByteCount(captured);
 if(match.IndexInBytes!=0||match.LengthInBytes!=input.Length||!group.Success||group.IndexInBytes!=(rtl?input.Length-length:0)||group.LengthInBytes!=length||group.GetValueString()!=captured)throw new Exception("Capture range/value mismatch");
}
static int Detailed(OwnedRegex regex,byte[] input,CancellationToken token) {
 var m=regex.MatchDetailed(input,token);if(!m.Success)return 0;var g=m.GetGroup(1);return 1+m.IndexInBytes+m.LengthInBytes+g.IndexInBytes+g.LengthInBytes;
}
static int Consume(OwnedRegex regex,byte[] input,CancellationToken token) {
 var sum=0;foreach(var match in regex.EnumerateMatches(input,token))sum=unchecked(sum+1+match.IndexInBytes+match.LengthInBytes);return sum;
}
static(double Ns,double Bytes,double Ms)Measure(Lane lane,int count) {
 var bytes=GC.GetAllocatedBytesForCurrentThread();var start=Stopwatch.GetTimestamp();var sum=0;
 for(var i=0;i<count;i++)sum=unchecked(sum+lane.Call());
 var ms=Stopwatch.GetElapsedTime(start).TotalMilliseconds;var allocated=GC.GetAllocatedBytesForCurrentThread()-bytes;
 if(sum!=unchecked(count*lane.Expected))throw new Exception("Timed mismatch: "+lane.Name);
 Sink.Value=sum;return(ms*1e6/count,(double)allocated/count,ms);
}
record Case(string Name,string Part,int Repeats,bool Hit,bool Scalar);
record Lane(string Name,Func<int> Call,int Expected);
static class Sink{internal static int Value;}

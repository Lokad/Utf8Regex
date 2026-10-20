using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.PythonRe;
var phase=args.Length==0?"preflight":args[0];
void Emit(object value)=>Console.WriteLine(JsonSerializer.Serialize(value));
Emit(new{Kind="identity",Phase=phase,Core=Hash(typeof(Utf8Regex).Assembly.Location),Python=Hash(typeof(Utf8PythonRegex).Assembly.Location),Driver=Hash(typeof(Program).Assembly.Location),Runtime=Environment.Version.ToString()});
var policy=new Utf8ResourcePolicy{MaxWorkSteps=50000000,MaxDepth=1024,MaxWorkingBytes=64*1048576,MaxResultBytes=128*1048576};
using var tokens=new CancellationTokenSource();
var miss="xx "+string.Concat(Enumerable.Repeat("Ватсон ",64))+"zz";
var longMiss=string.Concat(Enumerable.Repeat("Ватсон ",8192));
var lanes=new List<(string Name,Func<int> Call,int Expected,Action Verify)>();
var specs=new[]{
 (Name:"ru-short-hit",Pattern:"Шерлок",Text:"xx Шерлок zz",Operation:0,Mode:"unlimited"),
 (Name:"ru-miss-search",Pattern:"Шерлок",Text:miss,Operation:0,Mode:"unlimited"),
 (Name:"ru-miss-match",Pattern:"Шерлок",Text:miss,Operation:1,Mode:"unlimited"),
 (Name:"ru-miss-full",Pattern:"Шерлок",Text:miss,Operation:2,Mode:"unlimited"),
 (Name:"ru-miss-search-text",Pattern:"Шерлок",Text:miss,Operation:3,Mode:"unlimited"),
 (Name:"ru-miss-match-text",Pattern:"Шерлок",Text:miss,Operation:4,Mode:"unlimited"),
 (Name:"ru-miss-full-text",Pattern:"Шерлок",Text:miss,Operation:5,Mode:"unlimited"),
 (Name:"ru-long-hit",Pattern:"Шерлок",Text:longMiss+"Шерлок",Operation:0,Mode:"unlimited"),
 (Name:"ru-long-miss",Pattern:"Шерлок",Text:longMiss,Operation:0,Mode:"unlimited"),
 (Name:"ru-exact-full",Pattern:"Шерлок",Text:"Шерлок",Operation:2,Mode:"unlimited"),
 (Name:"ru-exact-full-text",Pattern:"Шерлок",Text:"Шерлок",Operation:5,Mode:"unlimited"),
 (Name:"emoji-hit",Pattern:"😀",Text:"中😀尾",Operation:0,Mode:"unlimited"),
 (Name:"emoji-miss",Pattern:"😀",Text:new string('é',1024),Operation:0,Mode:"unlimited"),
 (Name:"ru-short-miss",Pattern:"Шерлок",Text:"xx",Operation:0,Mode:"unlimited"),
 (Name:"ru-empty-input",Pattern:"Шерлок",Text:"",Operation:0,Mode:"unlimited"),
 (Name:"ascii-hit-control",Pattern:"needle",Text:new string('x',65536)+"needle",Operation:0,Mode:"unlimited"),
 (Name:"ascii-miss-control",Pattern:"needle",Text:new string('x',65536),Operation:0,Mode:"unlimited"),
 (Name:"ru-miss-full-control",Pattern:"Шерлок",Text:miss,Operation:2,Mode:"finite"),
 (Name:"ru-exact-full-control",Pattern:"Шерлок",Text:"Шерлок",Operation:2,Mode:"finite"),
 (Name:"ru-long-miss-control",Pattern:"Шерлок",Text:longMiss,Operation:0,Mode:"finite"),
 (Name:"ru-miss-token-control",Pattern:"Шерлок",Text:miss,Operation:2,Mode:"token"),
 (Name:"ru-miss-deadline-control",Pattern:"Шерлок",Text:miss,Operation:2,Mode:"deadline")};
foreach(var spec in specs){
 var regex=new Utf8PythonRegex(spec.Pattern,PythonReCompileOptions.None,spec.Mode=="deadline"?TimeSpan.FromSeconds(10):Regex.InfiniteMatchTimeout,spec.Mode=="finite"?policy:default);
 var token=spec.Mode=="token"?tokens.Token:default;var input=new UTF8Encoding(false,true).GetBytes(spec.Text);var offset=spec.Text.IndexOf(spec.Pattern,StringComparison.Ordinal);
 if(spec.Operation%3==1&&offset!=0||spec.Operation%3==2&&spec.Text!=spec.Pattern)offset=-1;
 var start=offset<0?0:Encoding.UTF8.GetByteCount(spec.Text.AsSpan(0,offset));var end=start+Encoding.UTF8.GetByteCount(spec.Pattern);
 var expected=spec.Operation>=3?(offset<0?-1:spec.Pattern.Length):(offset<0?0:1+start+end+offset+offset+spec.Pattern.Length);
 string? kept=null;
 Func<int> action=spec.Operation>=3?()=>{kept=spec.Operation switch{3=>regex.SearchToString(input,0,token),4=>regex.MatchToString(input,0,token),_=>regex.FullMatchToString(input,0,token)};return kept?.Length??-1;}:()=>{var m=spec.Operation switch{0=>regex.Search(input,0,token),1=>regex.Match(input,0,token),_=>regex.FullMatch(input,0,token)};return m.Success?1+m.StartOffsetInBytes+m.EndOffsetInBytes+m.StartOffsetInUtf16+m.EndOffsetInUtf16:0;};
 Action verify=()=>{
  if(action()!=expected)throw new Exception("Public outcome: "+spec.Name);
  if(spec.Operation>=3){if(kept!=(offset<0?null:spec.Pattern))throw new Exception("Text: "+spec.Name);}
  else{var m=spec.Operation switch{0=>regex.Search(input,0,token),1=>regex.Match(input,0,token),_=>regex.FullMatch(input,0,token)};
   if(m.Success!=(offset>=0)||offset>=0&&(m.StartOffsetInBytes!=start||m.EndOffsetInBytes!=end||m.StartOffsetInUtf16!=offset||m.EndOffsetInUtf16!=offset+spec.Pattern.Length||m.GetValueString()!=spec.Pattern))throw new Exception("Public coordinates: "+spec.Name);}
 };
 verify();Emit(new{Kind="preflight",spec.Name,Bytes=input.Length,Expected=expected,Subject=HashBytes(input),spec.Pattern,spec.Operation,spec.Mode});lanes.Add((spec.Name,action,expected,verify));
}
foreach(var finite in new[]{false,true}){
 var regex=new Utf8Regex("needle",RegexOptions.CultureInvariant,Regex.InfiniteMatchTimeout,finite?policy:default);var input=Encoding.UTF8.GetBytes(new string('x',65536)+"needle");
 Func<int> action=()=>regex.Count(input);lanes.Add(("core-count-control/"+(finite?"finite":"unlimited"),action,1,()=>{if(action()!=1)throw new Exception("Core control");}));
}
foreach(var unicode in new[]{false,true})foreach(var decode in new[]{false,true}){
 var text=unicode?miss:new string('x',65536);var input=Encoding.UTF8.GetBytes(text);var regex=new Regex(unicode?"Шерлок":"needle",RegexOptions.CultureInvariant);
 Func<int> action=()=>{var m=regex.Match(decode?Encoding.UTF8.GetString(input):text);return m.Success?1+m.Index+m.Length:0;};
 lanes.Add(("dotnet-control/"+(unicode?"unicode":"ascii")+"/"+(decode?"decode":"predecoded"),action,0,()=>{if(action()!=0)throw new Exception("Dotnet control");}));
}
foreach(var lane in lanes)lane.Verify();
if(phase=="preflight"){foreach(var lane in lanes)Emit(new{Kind="lane",lane.Name,lane.Expected});return;}
var iterations=new int[lanes.Count];
for(var i=0;i<lanes.Count;i++){
 var lane=lanes[i];var warm=Stopwatch.StartNew();var calls=0;do{Sink.Value=lane.Call();calls++;}while(calls<10005||warm.ElapsedMilliseconds<500);
 var n=32;for(var attempt=0;attempt<6;attempt++){var m=Measure(lane,n);if(m.Ms>=50&&m.Ms<=150)break;n=(int)Math.Clamp(Math.Ceiling(n*65/Math.Max(.001,m.Ms)),1,10000000);}
 iterations[i]=n;Emit(new{Kind="warmup",lane.Name,Calls=calls,Milliseconds=warm.Elapsed.TotalMilliseconds});
}
// Recheck settled durations after all lanes have warmed; this is calibration,
// not a second source comparison or performance-protocol change.
for(var i=0;i<lanes.Count;i++){
 var fastest=double.PositiveInfinity;for(var confirm=0;confirm<3;confirm++)fastest=Math.Min(fastest,Measure(lanes[i],iterations[i]).Ms);
 iterations[i]=(int)Math.Clamp(Math.Ceiling(iterations[i]*50/Math.Max(.001,fastest)),iterations[i],10000000);
}
for(var sample=0;sample<5;sample++)for(var pos=0;pos<lanes.Count;pos++){
 var i=(sample&1)==0?pos:lanes.Count-1-pos;var lane=lanes[i];var m=Measure(lane,iterations[i]);Emit(new{Kind="sample",lane.Name,Sample=sample,Iterations=iterations[i],m.Ms,m.Ns,m.Bytes});
}
foreach(var lane in lanes)lane.Verify();Emit(new{Kind="complete"});
static string Hash(string path)=>HashBytes(File.ReadAllBytes(path));
static string HashBytes(byte[] data)=>Convert.ToHexString(SHA256.HashData(data));
static (double Ns,double Bytes,double Ms) Measure((string Name,Func<int> Call,int Expected,Action Verify) lane,int n){var bytes=GC.GetAllocatedBytesForCurrentThread();var start=Stopwatch.GetTimestamp();var sum=0;for(var i=0;i<n;i++)sum=unchecked(sum+lane.Call());var ms=Stopwatch.GetElapsedTime(start).TotalMilliseconds;var b=GC.GetAllocatedBytesForCurrentThread()-bytes;if(sum!=unchecked(lane.Expected*n))throw new Exception("Timed checksum");Sink.Value=sum;return(ms*1e6/n,(double)b/n,ms);}
static class Sink{internal static int Value;}

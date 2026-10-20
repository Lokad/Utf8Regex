using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.PythonRe;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using System.Text.Unicode;
var cases=new[]{
 (Name:"ascii-hit",Pattern:"needle",Text:new string('x',65536)+"needle"),
 (Name:"ascii-miss",Pattern:"needle",Text:new string('x',65536)),
 (Name:"cyrillic-hit",Pattern:"Шерлок",Text:"xx Шерлок zz"),
 (Name:"cyrillic-miss",Pattern:"Шерлок",Text:"xx "+string.Concat(Enumerable.Repeat("Ватсон ",64))+"zz")};
var lanes=new List<(string Name,Func<int> Call,int Expected)>();
void Emit(object x)=>Console.WriteLine(JsonSerializer.Serialize(x));
Emit(new{Kind="identity",Runtime=Environment.Version.ToString(),Core=Hash(typeof(Utf8Regex).Assembly.Location),Python=Hash(typeof(Utf8PythonRegex).Assembly.Location),Driver=Hash(typeof(Program).Assembly.Location)});
foreach(var item in cases){
 var input=new UTF8Encoding(false,true).GetBytes(item.Text);var core=new Utf8Regex(item.Pattern,RegexOptions.CultureInvariant);var py=new Utf8PythonRegex(item.Pattern);
 var offset=item.Text.IndexOf(item.Pattern,StringComparison.Ordinal);var found=offset>=0;
 var start=found?Encoding.UTF8.GetByteCount(item.Text.AsSpan(0,offset)):0;var end=found?start+Encoding.UTF8.GetByteCount(item.Pattern):0;
 var byteExpected=found?1+start+end:0;var publicExpected=found?byteExpected+offset+offset+item.Pattern.Length:0;
 var v=Utf8Validation.Validate(input);if(v.Utf16Length!=item.Text.Length||!Utf8.IsValid(input))throw new Exception("Invalid preflight");
 Emit(new{Kind="case",item.Name,item.Pattern,Bytes=input.Length,Utf16=item.Text.Length,Found=found,Subject=HashBytes(input),Route=core.Inspection.ExecutionKind.ToString()});
 lanes.Add((item.Name+"/public-search",()=>{var m=py.Search(input);return m.Success?1+m.StartOffsetInBytes+m.EndOffsetInBytes+m.StartOffsetInUtf16+m.EndOffsetInUtf16:0;},publicExpected));
 lanes.Add((item.Name+"/metadata-validation",()=>{var invocation=Invocation();return Utf8Validation.Validate(input,ref invocation).Utf16Length;},item.Text.Length));
 lanes.Add((item.Name+"/validity-only",()=>{var invocation=Invocation();Utf8Validation.ThrowIfInvalidOnly(input,ref invocation);return 1;},1));
 lanes.Add((item.Name+"/bcl-validity",()=>Utf8.IsValid(input)?1:0,1));
 lanes.Add((item.Name+"/native-search-prevalidated",()=>Find(core,input,false),byteExpected));
 lanes.Add((item.Name+"/metadata-plus-native",()=>Find(core,input,true),byteExpected));
 lanes.Add((item.Name+"/production-discovery",()=>{var invocation=Invocation();return core.FindLiteralForResult(input,0,ref invocation,out var s,out var e,out _)?1+s+e:0;},byteExpected));
}
foreach(var lane in lanes)if(lane.Call()!=lane.Expected)throw new Exception("Lane mismatch: "+lane.Name);
if(args.Contains("--preflight")){foreach(var lane in lanes)Emit(new{Kind="preflight",lane.Name,lane.Expected});return;}
var iterations=new int[lanes.Count];
for(var i=0;i<lanes.Count;i++){
 var lane=lanes[i];var sw=Stopwatch.StartNew();var calls=0;do{Sink.Value=lane.Call();calls++;}while(calls<10005||sw.ElapsedMilliseconds<500);
 var n=32;for(var trial=0;trial<6;trial++){var m=Measure(lane,n);if(m.Ms>=50&&m.Ms<=150)break;n=(int)Math.Clamp(Math.Ceiling(n*65/Math.Max(.001,m.Ms)),1,10000000);}
 iterations[i]=n;Emit(new{Kind="warmup",lane.Name,Calls=calls,Milliseconds=sw.Elapsed.TotalMilliseconds});
}
for(var sample=0;sample<5;sample++)for(var position=0;position<lanes.Count;position++){
 var i=(sample&1)==0?position:lanes.Count-1-position;var lane=lanes[i];var m=Measure(lane,iterations[i]);
 Emit(new{Kind="sample",lane.Name,Sample=sample,Iterations=iterations[i],m.Ns,m.Bytes,m.Ms});
}
foreach(var lane in lanes)if(lane.Call()!=lane.Expected)throw new Exception("Final mismatch");
Emit(new{Kind="complete"});
static Utf8SliceInvocation Invocation()=>new(Utf8ExecutionLimits.Unlimited,Regex.InfiniteMatchTimeout,default);
static int Find(Utf8Regex regex,byte[] input,bool validate){var invocation=Invocation();if(validate)_=Utf8Validation.Validate(input,ref invocation);if(!regex.TryFindNativeLiteralForResult(input,0,ref invocation,out var found,out var start,out var end))throw new Exception("Route decline");return found?1+start+end:0;}
static (double Ns,double Bytes,double Ms) Measure((string Name,Func<int> Call,int Expected) lane,int n){var bytes=GC.GetAllocatedBytesForCurrentThread();var t=Stopwatch.GetTimestamp();var sum=0;for(var i=0;i<n;i++)sum=unchecked(sum+lane.Call());var ms=Stopwatch.GetElapsedTime(t).TotalMilliseconds;var b=GC.GetAllocatedBytesForCurrentThread()-bytes;if(sum!=unchecked(n*lane.Expected))throw new Exception("Timed mismatch");Sink.Value=sum;return(ms*1e6/n,(double)b/n,ms);}
static string Hash(string path)=>HashBytes(File.ReadAllBytes(path));
static string HashBytes(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
static class Sink{internal static int Value;}

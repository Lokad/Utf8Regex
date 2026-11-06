using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.PythonRe;

var startup=Environment.ProcessorCount;using var process=Process.GetCurrentProcess();process.ProcessorAffinity=(nint)4;
string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
var core=typeof(Utf8Regex).Assembly;var python=typeof(Utf8PythonRegex).Assembly;
if(startup!=8||Hash(core.Location)!=args[1]||Hash(python.Location)!=args[2]||
    new[]{core,python}.Any(a=>AssemblyLoadContext.GetLoadContext(a)!=AssemblyLoadContext.Default))throw new Exception("Wrong producer/startup");
foreach(var key in new[]{"DOTNET_TieredCompilation","COMPlus_TieredCompilation","DOTNET_TieredPGO","COMPlus_TieredPGO","DOTNET_JitDisasm"})
 if(Environment.GetEnvironmentVariable(key)is not null)throw new Exception("Timing override");
var preflight=args.Contains("--preflight");var comparators=args.Contains("--comparators");
var timer=Stopwatch.StartNew();var rows=new List<object>();using var source=new CancellationTokenSource();
var policy=new Utf8ResourcePolicy{MaxWorkSteps=500_000_000,MaxDepth=16384,MaxWorkingBytes=64UL<<20,MaxResultBytes=64UL<<20};
const string pattern="item-([0-9]+)";
var text=string.Concat(Enumerable.Repeat("item-12 item-345 ",1024));var input=Encoding.UTF8.GetBytes(text);
if(input.Length!=17408||Convert.ToHexString(SHA256.HashData(input))!="332DF9A8E15D370BEA5C971A99487AB004C70A24DF73ECE9FF954619B5E8C3C1")throw new Exception("Wrong canonical case");
var expected=Enumerable.Range(0,2048).Select(i=>i%2==0?"12":"345").ToArray();
if(comparators){
 var bcl=new Regex(pattern,RegexOptions.Compiled);
 string[] Find(string value)=>bcl.Matches(value).Select(match=>match.Groups[1].Value).ToArray();
 Run("actual-findall-predecoded-dotnet",input,()=>Find(text),expected);
 Run("actual-findall-decode-then-dotnet",input,()=>Find(Encoding.UTF8.GetString(input)),expected);
}else{
 var owner=new Utf8PythonRegex(pattern);
 Run("actual-captured-findall-strings",input,()=>owner.FindAllToStrings(input).ScalarValues,expected);
 Run("actual-captured-findall-utf8",input,()=>owner.FindAllToUtf8(input).ScalarValues,expected.Select(Encoding.UTF8.GetBytes).ToArray());
 var finite=new Utf8PythonRegex(pattern,default,Timeout.InfiniteTimeSpan,policy);
 Run("actual-captured-findall-finite-live",input,()=>finite.FindAllToStrings(input,0,source.Token).ScalarValues,expected);
 var detailedExpected=new Regex(pattern).Matches(text).Select(m=>new Utf8PythonDetailedMatchData{LastIndex=1,
  Groups=m.Groups.Cast<Group>().Select((g,n)=>new Utf8PythonGroupMatchData{Number=n,Success=g.Success,
   ValueText=g.Value,StartOffsetInBytes=g.Index,EndOffsetInBytes=g.Index+g.Length,
   StartOffsetInUtf16=g.Index,EndOffsetInUtf16=g.Index+g.Length,HasContiguousByteRange=g.Success}).ToArray()}).ToArray();
 Run("actual-captured-detailed",input,()=>owner.FindIterDetailed(input),detailedExpected);
 var rollback=new Utf8PythonRegex("(?P<v>a)+a");var rollbackInput=Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("aaa;",512)));
 Run("single-group-backtracking",rollbackInput,()=>rollback.FindAllToStrings(rollbackInput).ScalarValues,Enumerable.Repeat("a",512).ToArray());
 var falsePrefix=Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("item-X ",4096))+"item-345");
 Run("false-full-prefixes-late-hit",falsePrefix,()=>owner.FindAllToStrings(falsePrefix).ScalarValues,new[]{"345"});
 var falseFirst=Encoding.UTF8.GetBytes(new string('i',8192)+"item-12");
 Run("false-first-bytes-late-hit",falseFirst,()=>owner.FindAllToStrings(falseFirst).ScalarValues,new[]{"12"});
 var longer=new Utf8PythonRegex("item-(0AAA|1BBB|2CCC|3DDD|4EEE|5FFF|6GGG|7HHH|8III|9JJJ)");
 var longerInput=Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("item-3DDD item-5FFF ",512)));
 Run("longer-discriminator-tails",longerInput,()=>longer.FindAllToStrings(longerInput).ScalarValues,Enumerable.Range(0,1024).Select(i=>i%2==0?"3DDD":"5FFF").ToArray());
 var unicode=Encoding.UTF8.GetBytes("é😀 "+text);
 Run("unicode-prefix-captured-findall",unicode,()=>owner.FindAllToStrings(unicode).ScalarValues,expected);
 var literalInput=Encoding.UTF8.GetBytes(new string('x',8192)+"needle");var coreLiteral=new Utf8Regex("needle");
 Run("unchanged-core-literal-count",literalInput,()=>coreLiteral.Count(literalInput),1);
 var literal=new Utf8PythonRegex("needle");
 Run("unchanged-python-literal-findall",literalInput,()=>literal.FindAllToStrings(literalInput).ScalarValues,new[]{"needle"});
 var namedInput="token-other"u8.ToArray();var named=new Utf8PythonRegex(@"(?P<word>[a-z]+)-(?P=word)");
 Run("unchanged-named-fullmatch-miss",namedInput,()=>named.FullMatch(namedInput).Success,false);
}
Console.WriteLine(JsonSerializer.Serialize(new{Worker=args[0],ComparatorsOnly=comparators,ElapsedSeconds=timer.Elapsed.TotalSeconds,
 StartupProcessors=startup,Affinity="4",Runtime=RuntimeInformation.FrameworkDescription,CoreSha256=Hash(core.Location),PythonSha256=Hash(python.Location),
 CoreVersion=core.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
 LoadedCoreAssemblies=AppDomain.CurrentDomain.GetAssemblies().Count(a=>a.GetName().Name=="Lokad.Utf8Regex"),
 LoadedPythonAssemblies=AppDomain.CurrentDomain.GetAssemblies().Count(a=>a.GetName().Name=="Lokad.Utf8Regex.PythonRe"),Rows=rows},new JsonSerializerOptions{WriteIndented=true}));

void Run<T>(string name,byte[] bytes,Func<T> call,T wanted){
 Check(call(),wanted);if(preflight){Console.Error.WriteLine($"{name}: {bytes.Length}bytes");return;}
 var warm=Stopwatch.StartNew();var warmCalls=0;
 do{Check(call(),wanted);warmCalls++;}while(warm.ElapsedMilliseconds<1000||warmCalls<128);
 warm.Stop();var iterations=1;var calibration=Measure(iterations,call,wanted);
 while(calibration.Milliseconds<20&&iterations<2_000_000){iterations=Math.Min(iterations*2,2_000_000);calibration=Measure(iterations,call,wanted);}
 iterations=(int)Math.Clamp(Math.Ceiling(iterations*75/Math.Max(calibration.Milliseconds,.000001)),1,2_000_000);
 var us=new double[5];var durations=new double[5];var allocated=new double[5];
 for(var sample=0;sample<5;sample++){
  GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();var result=Measure(iterations,call,wanted);
  if(result.Milliseconds<20)throw new Exception("Short sample: "+name);us[sample]=result.Milliseconds*1000/iterations;
  durations[sample]=result.Milliseconds;allocated[sample]=(double)result.Bytes/iterations;
 }
 rows.Add(new{Name=name,InputBytes=bytes.Length,InputSha256=Convert.ToHexString(SHA256.HashData(bytes)),Expected=wanted,
  Iterations=iterations,WarmCalls=warmCalls,WarmMilliseconds=warm.Elapsed.TotalMilliseconds,Microseconds=us,ElapsedMilliseconds=durations,AllocatedBytes=allocated});
}
static void Check<T>(T actual,T expected){
 if(actual is string[] a&&expected is string[] e){if(!a.AsSpan().SequenceEqual(e))throw new Exception("String result differs");}
 else if(actual is byte[][] b&&expected is byte[][] f){if(b.Length!=f.Length)throw new Exception("Byte result length differs");for(var i=0;i<b.Length;i++)if(!b[i].AsSpan().SequenceEqual(f[i]))throw new Exception("Byte result differs");}
 else if(actual is Utf8PythonDetailedMatchData[] d&&expected is Utf8PythonDetailedMatchData[] expectedDetailed){
  if(d.Length!=expectedDetailed.Length)throw new Exception("Detailed length differs");
  for(var i=0;i<d.Length;i++)if(d[i].LastIndex!=expectedDetailed[i].LastIndex||d[i].LastGroup!=expectedDetailed[i].LastGroup||!d[i].Groups.AsSpan().SequenceEqual(expectedDetailed[i].Groups))throw new Exception("Detailed results differ");
 }
 else if(!EqualityComparer<T>.Default.Equals(actual,expected))throw new Exception("Result differs");
}
static(double Milliseconds,long Bytes)Measure<T>(int iterations,Func<T> call,T expected){
 var results=new T[iterations];var bytes=GC.GetAllocatedBytesForCurrentThread();var stamp=Stopwatch.GetTimestamp();
 for(var i=0;i<iterations;i++)results[i]=call();var elapsed=Stopwatch.GetElapsedTime(stamp).TotalMilliseconds;bytes=GC.GetAllocatedBytesForCurrentThread()-bytes;
 foreach(var value in results)Check(value,expected);GC.KeepAlive(results);return(elapsed,bytes);
}

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
const string pattern="([a-z]+)-([0-9]+)";
var text=string.Concat(Enumerable.Repeat("item-12 other-345 ",1024));var input=Encoding.UTF8.GetBytes(text);
var expected=Enumerable.Range(0,2048).Select(i=>i%2==0?new[]{"item","12"}:new[]{"other","345"}).ToArray();
if(comparators){
 var bcl=new Regex(pattern,RegexOptions.Compiled);
 string[][] Find(string value)=>bcl.Matches(value).Select(m=>new[]{m.Groups[1].Value,m.Groups[2].Value}).ToArray();
 Run("actual-tuples-predecoded-dotnet",input,()=>Find(text),expected);
 Run("actual-tuples-decode-then-dotnet",input,()=>Find(Encoding.UTF8.GetString(input)),expected);
}else{
 var owner=new Utf8PythonRegex(pattern);
 Run("actual-two-capture-strings",input,()=>owner.FindAllToStrings(input).TupleValues,expected);
 Run("actual-two-capture-utf8",input,()=>owner.FindAllToUtf8(input).TupleValues,expected.Select(t=>t.Select(Encoding.UTF8.GetBytes).ToArray()).ToArray());
 var finite=new Utf8PythonRegex(pattern,default,Timeout.InfiniteTimeSpan,policy);
 Run("actual-two-capture-finite-live",input,()=>finite.FindAllToStrings(input,0,source.Token).TupleValues,expected);
 Run("actual-two-capture-detailed",input,()=>owner.FindIterDetailed(input),Detailed(pattern,text,_=>2));
 var unicode=Encoding.UTF8.GetBytes("é😀 "+text);
 Run("unicode-two-capture-strings",unicode,()=>owner.FindAllToStrings(unicode).TupleValues,expected);
 var optionalPattern="(?P<a>a)(?P<b>b)?";var optionalText=string.Concat(Enumerable.Repeat("aa ab ",256));var optionalInput=Encoding.UTF8.GetBytes(optionalText);var optional=new Utf8PythonRegex(optionalPattern);
 Run("optional-two-capture-detailed",optionalInput,()=>optional.FindIterDetailed(optionalInput),Detailed(optionalPattern,optionalText,m=>m.Groups[2].Success?2:1));
 var rollback=new Utf8PythonRegex("(?P<a>a)+(?P<b>a)");var rollbackInput=Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("aaa;",512)));
 Run("two-group-backtracking",rollbackInput,()=>rollback.FindAllToStrings(rollbackInput).TupleValues,Enumerable.Range(0,512).Select(_=>new[]{"a","a"}).ToArray());
 var twelve=new Utf8PythonRegex(string.Concat(Enumerable.Repeat("(a)",12)));var twelveInput=Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("aaaaaaaaaaaa;",128)));
 Run("twelve-linear-capture-strings",twelveInput,()=>twelve.FindAllToStrings(twelveInput).TupleValues,Enumerable.Range(0,128).Select(_=>Enumerable.Repeat("a",12).ToArray()).ToArray());
 var single=new Utf8PythonRegex("item-([0-9]+)");var singleInput=Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("item-12 item-345 ",1024)));
 Run("one-capture-strings-control",singleInput,()=>single.FindAllToStrings(singleInput).ScalarValues,Enumerable.Range(0,2048).Select(i=>i%2==0?"12":"345").ToArray());
 var oneRollback=new Utf8PythonRegex("(?P<a>a)+a");
 Run("one-group-backtracking-control",rollbackInput,()=>oneRollback.FindAllToStrings(rollbackInput).ScalarValues,Enumerable.Repeat("a",512).ToArray());
 var literalInput=Encoding.UTF8.GetBytes(new string('x',8192)+"needle");var literalCore=new Utf8Regex("needle");var literalPython=new Utf8PythonRegex("needle");
 Run("unchanged-core-literal-count",literalInput,()=>literalCore.Count(literalInput),1);
 Run("unchanged-python-literal-findall",literalInput,()=>literalPython.FindAllToStrings(literalInput).ScalarValues,new[]{"needle"});
 var namedInput="token-other"u8.ToArray();var named=new Utf8PythonRegex(@"(?P<word>[a-z]+)-(?P=word)");
 Run("unchanged-named-fullmatch-miss",namedInput,()=>named.FullMatch(namedInput).Success,false);
 const string smallPattern="(?P<a>ab)(?P<b>cd)";var small=new Utf8PythonRegex(smallPattern);var smallInput="abcd"u8.ToArray();
 Run("small-two-capture-fullmatch",smallInput,()=>small.FullMatchDetailedData(smallInput),Detailed(smallPattern,"abcd",_=>2)[0]);
 const string nestedPattern="(?P<outer>(?P<inner>a))";var nestedText=new string('a',256);var nestedInput=Encoding.UTF8.GetBytes(nestedText);var nested=new Utf8PythonRegex(nestedPattern);
 Run("nested-completion-order",nestedInput,()=>nested.FindIterDetailed(nestedInput),Detailed(nestedPattern,nestedText,_=>1));
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
 if(actual is string[][] t&&expected is string[][] u){if(t.Length!=u.Length)throw new Exception("Tuple count differs");for(var i=0;i<t.Length;i++)if(!t[i].AsSpan().SequenceEqual(u[i]))throw new Exception("Tuple differs");}
 else if(actual is byte[][][] v&&expected is byte[][][] w){if(v.Length!=w.Length)throw new Exception("Byte tuple count differs");for(var i=0;i<v.Length;i++){if(v[i].Length!=w[i].Length)throw new Exception("Byte tuple width differs");for(var j=0;j<v[i].Length;j++)if(!v[i][j].AsSpan().SequenceEqual(w[i][j]))throw new Exception("Byte tuple differs");}}
 else if(actual is string[] a&&expected is string[] e){if(!a.AsSpan().SequenceEqual(e))throw new Exception("String result differs");}
 else if(actual is byte[][] b&&expected is byte[][] f){if(b.Length!=f.Length)throw new Exception("Byte result length differs");for(var i=0;i<b.Length;i++)if(!b[i].AsSpan().SequenceEqual(f[i]))throw new Exception("Byte result differs");}
 else if(actual is Utf8PythonDetailedMatchData dd&&expected is Utf8PythonDetailedMatchData ee){if(dd.LastIndex!=ee.LastIndex||dd.LastGroup!=ee.LastGroup||!EqualGroups(dd.Groups,ee.Groups)||!EqualNames(dd.NameEntries,ee.NameEntries))throw new Exception("Detailed value differs");}
 else if(actual is Utf8PythonDetailedMatchData[] d&&expected is Utf8PythonDetailedMatchData[] expectedDetailed){
  if(d.Length!=expectedDetailed.Length)throw new Exception("Detailed length differs");
  for(var i=0;i<d.Length;i++)if(d[i].LastIndex!=expectedDetailed[i].LastIndex||d[i].LastGroup!=expectedDetailed[i].LastGroup||!EqualGroups(d[i].Groups,expectedDetailed[i].Groups)||!EqualNames(d[i].NameEntries,expectedDetailed[i].NameEntries))throw new Exception("Detailed results differ: actual="+JsonSerializer.Serialize(d[i])+" expected="+JsonSerializer.Serialize(expectedDetailed[i]));
 }
 else if(!EqualityComparer<T>.Default.Equals(actual,expected))throw new Exception("Result differs");
}
static(double Milliseconds,long Bytes)Measure<T>(int iterations,Func<T> call,T expected){
 var results=new T[iterations];var bytes=GC.GetAllocatedBytesForCurrentThread();var stamp=Stopwatch.GetTimestamp();
 for(var i=0;i<iterations;i++)results[i]=call();var elapsed=Stopwatch.GetElapsedTime(stamp).TotalMilliseconds;bytes=GC.GetAllocatedBytesForCurrentThread()-bytes;
 foreach(var value in results)Check(value,expected);GC.KeepAlive(results);return(elapsed,bytes);
}

static Utf8PythonDetailedMatchData[] Detailed(string pattern,string text,Func<Match,int?> last){
 var regex=new Regex(pattern.Replace("(?P<","(?<"));
 var names=regex.GetGroupNames().Where(n=>!int.TryParse(n,out _)).Select(n=>new PythonReNameEntry{Name=n,Number=regex.GroupNumberFromName(n)}).ToArray();
 return regex.Matches(text).Select(m=>new Utf8PythonDetailedMatchData{LastIndex=last(m),NameEntries=names,
  Groups=m.Groups.Cast<Group>().Select((g,n)=>new Utf8PythonGroupMatchData{Number=n,Success=g.Success,
   ValueText=g.Value,StartOffsetInBytes=g.Index,EndOffsetInBytes=g.Index+g.Length,
   StartOffsetInUtf16=g.Index,EndOffsetInUtf16=g.Index+g.Length,HasContiguousByteRange=g.Success}).ToArray()}).ToArray();
}
static bool EqualGroups(Utf8PythonGroupMatchData[] a,Utf8PythonGroupMatchData[] b){
 if(a.Length!=b.Length)return false;
 for(var i=0;i<a.Length;i++)if(a[i].Number!=b[i].Number||a[i].Success!=b[i].Success||a[i].ValueText!=b[i].ValueText||a[i].StartOffsetInBytes!=b[i].StartOffsetInBytes||a[i].EndOffsetInBytes!=b[i].EndOffsetInBytes||a[i].StartOffsetInUtf16!=b[i].StartOffsetInUtf16||a[i].EndOffsetInUtf16!=b[i].EndOffsetInUtf16||a[i].HasContiguousByteRange!=b[i].HasContiguousByteRange)return false;
 return true;
}
static bool EqualNames(PythonReNameEntry[] a,PythonReNameEntry[] b){
 if(a.Length!=b.Length)return false;
 foreach(var x in a){var found=false;foreach(var y in b)if(x.Name==y.Name&&x.Number==y.Number){found=true;break;}if(!found)return false;}
 return true;
}
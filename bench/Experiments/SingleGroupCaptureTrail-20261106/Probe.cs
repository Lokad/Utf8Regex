using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.PythonRe;

var core=typeof(Utf8Regex).Assembly;var python=typeof(Utf8PythonRegex).Assembly;
string Hash(string p)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));
if(Hash(core.Location)!=args[0]||Hash(python.Location)!=args[1])throw new Exception("Wrong producer graph");
var input=Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("item-12 item-345 ",1024)));
var owner=new Utf8PythonRegex("item-([0-9]+)");var flags=BindingFlags.NonPublic|BindingFlags.Instance;
var program=(Utf8SliceProgram)typeof(Utf8PythonRegex).GetField("_scalarProgram",flags)!.GetValue(owner)!;
var candidate=(Utf8Regex?)typeof(Utf8PythonRegex).GetField("_utf8Regex",flags)!.GetValue(owner);
var invocation=new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited,Timeout.InfiniteTimeSpan,default);
var state=new PythonReScalarMatchState(input,program,0,Timeout.InfiniteTimeSpan,owner,invocation,candidate);
object positioned;
try{
 if(!state.MoveNext(input)||!state.TryGetCaptureByteRange(1,out var start,out var end)||
    !input.AsSpan(start,end-start).SequenceEqual("12"u8)||state.GetLastIndex(input)!=1)throw new Exception("Production capture differs");
 var workspace=(Utf8SliceRunner.AttemptWorkspace)typeof(PythonReScalarMatchState).GetField("_workspace",flags)!.GetValue(state)!;
 positioned=new{state.ReservedBytes,workspace.Stacks.FrameCount,workspace.Stacks.TrailCount};
}finally{state.Dispose();}
if(state.ReservedBytes!=0)throw new Exception("Unrefunded reservation");
Console.WriteLine(JsonSerializer.Serialize(new{Scope="Untimed production state; no alternative executor",CoreSha256=Hash(core.Location),PythonSha256=Hash(python.Location),
 program.GroupCount,program.UsesCaptureOpeningJournal,Positioned=positioned,DisposedReservedBytes=state.ReservedBytes},new JsonSerializerOptions{WriteIndented=true}));

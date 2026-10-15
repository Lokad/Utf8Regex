using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RegexCacheCollection.Name)]
public sealed class Utf8RuntimeRetainedGraphTests
{
    private static readonly string[] Patterns=["abc","café","😀𐐨","foo|bar","zoo|apple|cat","a|z","ab|ac",
        "aaaaa|bbbbb|ccccc","abc|def|ghi|jkl","中文词|日本人|韩国人","中文|日本","中|日","😀|😃","éé|öü",
        "foo(?=bar)","中(?=文)",@"\b(?:foo|bar)\b","foo|foobar","a0x|a1y|b0x|b1y",
        "^[-+]?[0-9]+(?:\\.[0-9]+)?$","(?m:^(?:using|import)\\s+[a-z.]+)","begin(?:.|\\n)*?end",
        "(?<n>😀)\\k<n>","(?<=\\p{L})(?<n>.)"];
    public static IEnumerable<object[]> ActualShapes()=>from pattern in Patterns from compiled in new[]{false,true}
        select new object[]{pattern,compiled};
    [Theory]
    [InlineData("abc", false)] [InlineData("abc", true)]
    [InlineData("aaaaa|bbbbb|ccccc", false)] [InlineData("aaaaa|bbbbb|ccccc", true)]
    [InlineData("foo(?=bar)", false)] [InlineData("foo(?=bar)", true)]
    [InlineData("(?<n>😀)\\k<n>", false)] [InlineData("(?<n>😀)\\k<n>", true)]
    [InlineData("^[-+]?[0-9]+(?:\\.[0-9]+)?$", false)] [InlineData("^[-+]?[0-9]+(?:\\.[0-9]+)?$", true)]
    [InlineData("(?m:^(?:using|import)\\s+[a-z.]+)", false)] [InlineData("(?m:^(?:using|import)\\s+[a-z.]+)", true)]
    public void GlobalChargeCoversEveryDeclaredLibraryRuntimeReferent(string pattern, bool compiled)
    {
        Utf8RegexCache.ResetForTests();
        try
        {
            var regex=Utf8RegexCache.GetOrAdd(pattern,RegexOptions.CultureInvariant |
                (compiled?RegexOptions.Compiled:RegexOptions.None));
            var expected=InspectRuntimeGraph(regex);
            var field=typeof(Utf8RegexCache).GetField("s_cache",BindingFlags.Static|BindingFlags.NonPublic);
            Assert.NotNull(field); var cache=field.GetValue(null); Assert.NotNull(cache);
            var entry=Field(cache,"_first"); Assert.NotNull(entry);
            var manifest = Utf8GlobalCacheDescendantTests.InspectEntryPayloads(entry);
            foreach (var pair in expected)
                Assert.Contains(manifest,extent=>ReferenceEquals(extent.Value,pair.Key)&&extent.Bytes==pair.Value);
        }
        finally { Utf8RegexCache.ResetForTests(); }
    }

    [Theory]
    [MemberData(nameof(ActualShapes))]
    public void TypedCollectorMatchesActualRuntimeAndPreparedGraphs(string pattern,bool compiled)
    {
        var regex=new Utf8Regex(pattern,RegexOptions.CultureInvariant|(compiled?RegexOptions.Compiled:RegexOptions.None));
        AssertManifest(InspectRuntimeGraph(regex),Collect(Roots(regex)));
    }

    [Fact]
    public void EveryStoredRuntimeAndInlineLayoutKeepsInactiveSharedAndDistinctPayloads()
    {
        var (roots,layouts)=PopulateStoredLayouts();
        var library=typeof(Utf8Regex).Assembly;
        foreach(var type in RuntimeTypes(library)) Assert.Contains(type,layouts);
        foreach(var type in library.GetTypes().Where(static type=>type.IsSubclassOf(typeof(MulticastDelegate))&&
            type.DeclaringType is not null&&RuntimeTypeNames.Contains(type.DeclaringType.Name))) Assert.Contains(type,layouts);
        // The retired fallback emitter and its two delegate/receipt pairs no
        // longer contribute layouts. The loops above require every live type.
        Assert.Equal(95,layouts.Count);
        var expected=InspectRuntimeGraph(roots);
        Assert.True(expected.Keys.OfType<string>().Count(static text=>text=="qqq")>1);
        AssertManifest(expected,Collect(roots));
    }

    [Fact]
    public void RuntimeRootsCrossTheExactRetainedByteBoundary()
    {
        var (roots,_)=PopulateStoredLayouts(); var probe=Cache(1024*1024);
        Assert.Same(roots,Add(probe,"key",roots)); var maximum=probe.PayloadBytes; Assert.True(maximum>0);
        foreach(var bytes in new[]{maximum-1,maximum})
        {
            var cache=Cache(bytes); Assert.Same(roots,Add(cache,"key",roots));
            Assert.Equal(bytes==maximum?1:0,cache.Count); Assert.Equal(bytes==maximum?maximum:0,cache.PayloadBytes);
            cache.Clear(); Assert.Equal(0ul,cache.PayloadBytes); AssertManifest(InspectRuntimeGraph(roots),Collect(roots));
        }
    }

    [Fact]
    public void TwoEntriesConservativelyChargeTheirSharedGraphTwice()
    {
        var (roots,_)=PopulateStoredLayouts(); var graph=InspectRuntimeGraph(roots); var cache=Cache(1024*1024);
        Assert.Same(roots,Add(cache,"first",roots)); Assert.Same(roots,Add(cache,"second",roots)); Assert.Equal(2,cache.Count);
        var expected = 2 * graph.Values.Aggregate(0UL, static (sum, bytes) => sum + bytes);
        expected += Utf8ConstructionBudget.StringBytes(5) + Utf8ConstructionBudget.StringBytes(6);
        Assert.Equal(expected, cache.PayloadBytes);
        cache.Clear(); Assert.Equal(0ul,cache.PayloadBytes); AssertManifest(graph,Collect(roots));
    }

    [Fact]
    public void EveryWorkCutoffRestoresParentLoansAndPreservesSpentWork()
    {
        var roots=Roots(new Utf8Regex("a",RegexOptions.CultureInvariant)); ulong work;
        using(var measured=Utf8ConstructionScope.Enter()){_=Collect(roots);work=measured.Budget.WorkSteps;}
        for(var available=0ul;available<=work;available++)
        {
            using var owner=new Utf8ConstructionScope(ulong.MaxValue,ulong.MaxValue,2+available,ulong.MaxValue);
            _=owner.Budget.AllocateObject(17,static _=>new object());var checkpoint = owner.Budget.ReservedBytes;
            using(var child=Utf8ConstructionScope.Enter())
            {
                var failure=Record.Exception(()=>Collect(roots));
                if(available<work) Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps,Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                else Assert.Null(failure);
            }
            Assert.Equal(128ul,owner.Budget.ReservedBytes);Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            Assert.Equal(0ul,owner.Budget.Depth);Assert.True(owner.Budget.WorkSteps>=2);
        }
    }

    [Fact]
    public void ByteDenialAndPartialCancellationPreserveBorrowedRuntimeGraphs()
    {
        var (roots,_)=PopulateStoredLayouts();var expected=InspectRuntimeGraph(roots);
        using(var owner=new Utf8ConstructionScope(ulong.MaxValue,128,ulong.MaxValue,ulong.MaxValue))
        {
            _=owner.Budget.AllocateObject(17,static _=>new object());var checkpoint = owner.Budget.ReservedBytes;
            using(var child=Utf8ConstructionScope.Enter())
                Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes,Assert.Throws<Utf8ResourceLimitExceededException>(()=>Collect(roots)).ResourceKind);
            Assert.Equal(128ul,owner.Budget.ReservedBytes);Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        }
        using var cancellation=new CancellationTokenSource();
        using(var owner=Utf8ConstructionScope.Enter())
        {
            _=owner.Budget.AllocateObject(17,static _=>new object());var checkpoint = owner.Budget.ReservedBytes;
            owner.Budget.CancellationToken=cancellation.Token;
            owner.Budget.CheckpointHook=steps=>{if(steps>=30)cancellation.Cancel();};
            using(var child=Utf8ConstructionScope.Enter())Assert.Throws<OperationCanceledException>(()=>Collect(roots));
            Assert.Equal(128ul,owner.Budget.ReservedBytes);Assert.Equal(checkpoint, owner.Budget.ReservedBytes);Assert.True(owner.Budget.WorkSteps>=30);
        }
        AssertManifest(expected,Collect(roots));
    }

    [Fact]
    public void BoundAndMulticastDelegateGraphsAreRejectedInsteadOfBeingZeroCertified()
    {
        Action single=StaticCallback;
        Action bound=new CallbackReceiver().Invoke;
        var multicast=single+single;
        using var owner=Utf8ConstructionScope.Enter();
        using var storage=Utf8GraphInspection<Utf8ConstructionTextNoControl>.Create(1024,default);
        Utf8RuntimeRetainedStorage.CollectDelegate(single,storage);
        Assert.Throws<InvalidOperationException>(()=>Utf8RuntimeRetainedStorage.CollectDelegate(bound,storage));
        Assert.Throws<InvalidOperationException>(()=>Utf8RuntimeRetainedStorage.CollectDelegate(multicast,storage));
        var manifest=storage.Finish();var extent=Assert.Single(manifest);Assert.Same(single,extent.Value);Assert.Equal(80ul,extent.Bytes);
    }

    [Fact]
    public void EveryCompiledRuntimeFactoryProducesACollectableBorrowedPlanGraph()
    {
        var prepared=Utf8ExecutionRetainedGraphTests.Prepared(new Utf8Regex("aaaaa|bbbbb|ccccc",RegexOptions.CultureInvariant));
        var verifier=Utf8VerifierRuntime.Create(prepared);
        foreach(var kind in Enum.GetValues<Utf8CompiledEngineKind>())
        {
            var runtime=Utf8CompiledRuntimeFactory.Create(new Utf8CompiledEngine(kind),prepared,verifier,RegexOptions.None);
            object[] roots=[runtime];AssertManifest(InspectRuntimeGraph(roots),Collect(roots));
        }
    }

    private sealed class CallbackReceiver { internal void Invoke(){} }
    private static void StaticCallback(){}

    private static readonly string[] RuntimeTypeNames=[nameof(Utf8RegexProgram),nameof(Utf8VerifierRuntime),
        nameof(Utf8ExactLiteralCompiledEngineRuntime),nameof(Utf8LiteralFamilyCompiledEngineRuntime),nameof(Utf8LiteralCompiledEngineRuntime),
        nameof(Utf8StructuralFamilyCompiledEngineRuntime),nameof(Utf8SimplePatternCompiledEngineRuntime),nameof(Utf8StructuralLinearAutomatonCompiledEngineRuntime),
        nameof(Utf8ByteSafeLinearCompiledEngineRuntime),nameof(Utf8SearchGuidedFallbackCompiledEngineRuntime),nameof(Utf8CompiledFallbackCompiledEngineRuntime),
        nameof(Utf8FallbackRegexCompiledEngineRuntime),nameof(Utf8CompiledSymmetricLiteralWindowCounter),nameof(Utf8EmittedDeterministicMatcher),
        nameof(Utf8EmittedKernelMatcher),nameof(Utf8EmittedLiteralFamilyCounter),nameof(Utf8EmittedTokenFamilyMatcher),
        nameof(Utf8EmittedWholeMatcher),nameof(Utf8NoStructuralVerifierRuntime),nameof(Utf8AsciiStructuralVerifierRuntime),
        nameof(Utf8ByteSafeLinearVerifierRuntime),nameof(Utf8ByteSafeLazyDfaVerifierRuntime),nameof(Utf8NoStructuralLinearRuntime),
        nameof(Utf8AsciiInstructionLinearRuntime),nameof(Utf8AsciiStructuralFamilyLinearRuntime),"CompositeTrailingLiteralSearch"];
    private static IEnumerable<Type> RuntimeTypes(Assembly library)=>library.GetTypes().Where(static type=>RuntimeTypeNames.Contains(type.Name));

    private static (object[] Roots,HashSet<Type> Layouts) PopulateStoredLayouts()
    {
        // Stored-state sentinel only; no matching or construction-admission claim.
        var prepared=Utf8ExecutionRetainedGraphTests.Prepared(new Utf8Regex(new string('q',3),RegexOptions.CultureInvariant));
        var secondPrepared=Utf8ExecutionRetainedGraphTests.Prepared(new Utf8Regex(new string('q',3),RegexOptions.CultureInvariant));
        Assert.Equal(prepared.SemanticRegex.Pattern,secondPrepared.SemanticRegex.Pattern);
        Assert.NotSame(prepared.SemanticRegex.Pattern,secondPrepared.SemanticRegex.Pattern);
        var preparedReferences=0;
        var catalog=prepared.SemanticRegex.CaptureCatalog;Assert.NotNull(catalog);
        var values=new Dictionary<Type,object>{[typeof(Utf8PreparedRegex)]=prepared,[typeof(Utf8CaptureCatalog)]=catalog};
        var layouts=new HashSet<Type>();var library=typeof(Utf8Regex).Assembly;
        var referenceTest=typeof(Utf8RuntimeRetainedGraphTests).GetMethod(nameof(ContainsReferences),BindingFlags.Static|BindingFlags.NonPublic);Assert.NotNull(referenceTest);
        var roots=RuntimeTypes(library).Where(static type=>type.Name!="CompositeTrailingLiteralSearch").Select(Build).ToArray();
        return(roots,layouts);

        object Build(Type type)
        {
            if(type==typeof(string))return new string('q',3);
            if(type==typeof(Utf8PreparedRegex))return preparedReferences++%2==0?prepared:secondPrepared;
            if(!type.IsValueType&&values.TryGetValue(type,out var shared))return shared;
            if(type.IsArray)
            {
                var element=type.GetElementType();Assert.NotNull(element);var array=Array.CreateInstance(element,2);values.Add(type,array);
                for(var i=0;i<array.Length;i++)array.SetValue(Build(element),i);return array;
            }
            var nullable=Nullable.GetUnderlyingType(type);if(nullable is not null)return Build(nullable);
            if(typeof(Delegate).IsAssignableFrom(type))
            {
                var invoke=type.GetMethod("Invoke");Assert.NotNull(invoke);
                var method=new DynamicMethod("StoredLayout_"+type.Name,invoke.ReturnType,invoke.GetParameters().Select(static p=>p.ParameterType).ToArray(),typeof(Utf8RuntimeRetainedGraphTests).Module);
                Assert.True(invoke.ReturnType==typeof(int)||invoke.ReturnType==typeof(bool));
                var info=method.GetDynamicILInfo();info.SetCode([0x16,0x2a],1);info.SetLocalSignature([0x07,0x00]);
                var callback=method.CreateDelegate(type);values.Add(type,callback);layouts.Add(type);return callback;
            }
            if(!Assert.IsType<bool>(referenceTest.MakeGenericMethod(type).Invoke(null,[])))
            {var primitive=Activator.CreateInstance(type);Assert.NotNull(primitive);return primitive;}
            if(type.IsAbstract)return Build(RuntimeTypes(library).First(candidate=>!candidate.IsAbstract&&type.IsAssignableFrom(candidate)));
            if(type.IsGenericType&&type.GetGenericTypeDefinition()==typeof(Utf8EmittedDelegate<>))
            {
                var callback=Assert.IsAssignableFrom<Delegate>(Build(type.GetGenericArguments()[0]));
                var receipt=Activator.CreateInstance(type,callback,Utf8EmittedManagedStorageTests.InspectManagedCopies(callback));
                Assert.NotNull(receipt);layouts.Add(type);return receipt;
            }
            Assert.Equal(library,type.Assembly);var result=RuntimeHelpers.GetUninitializedObject(type);
            if(!type.IsValueType)values.Add(type,result);layouts.Add(type);
            foreach(var field in Fields(type))
                if(Assert.IsType<bool>(referenceTest.MakeGenericMethod(field.FieldType).Invoke(null,[])))field.SetValue(result,Build(field.FieldType));
            return result;
        }
    }
    private static bool ContainsReferences<T>()=>RuntimeHelpers.IsReferenceOrContainsReferences<T>();
    private static object[] Roots(Utf8Regex regex)
    {
        var program=Assert.IsType<Utf8RegexProgram>(Field(regex,"_program"));
        return Field(regex,"_fusedAsciiBoundaryLiteralFamilyCounter") is {} counter?[program,counter]:[program];
    }
    private static Utf8RetainedPayloadExtent[] Collect(object[] roots)
    {
        using var owner=Utf8ConstructionScope.Enter();
        using var storage=Utf8GraphInspection<Utf8ConstructionTextNoControl>.Create(64*1024*1024,default);
        CollectRoots(roots,storage);Assert.False(storage.Exceeded);return storage.Finish();
    }
    private static void CollectRoots<TControl>(object[] roots,Utf8GraphInspection<TControl> storage)
        where TControl:struct,IUtf8ConstructionTextControl
    {
        foreach(var root in roots)
            switch(root)
            {
                case Utf8RegexProgram program:program.CollectRetainedStorage(storage);break;
                case Utf8CompiledEngineRuntime compiled:compiled.CollectRetainedStorage(storage);break;
                case Utf8VerifierRuntime verifier:verifier.CollectRetainedStorage(storage);break;
                case Utf8StructuralVerifierRuntime structural:structural.CollectRetainedStorage(storage);break;
                case Utf8StructuralLinearRuntime linear:linear.CollectRetainedStorage(storage);break;
                case Utf8CompiledSymmetricLiteralWindowCounter counter:counter.CollectRetainedStorage(storage);break;
                case Utf8EmittedDeterministicMatcher deterministic:deterministic.CollectRetainedStorage(storage);break;
                case Utf8EmittedKernelMatcher kernel:kernel.CollectRetainedStorage(storage);break;
                case Utf8EmittedLiteralFamilyCounter family:family.CollectRetainedStorage(storage);break;
                case Utf8EmittedTokenFamilyMatcher token:token.CollectRetainedStorage(storage);break;
                case Utf8EmittedWholeMatcher whole:whole.CollectRetainedStorage(storage);break;
                default:throw new InvalidOperationException("Unqualified test root: "+root.GetType().FullName);
            }
    }
    private static void AssertManifest(Dictionary<object,ulong> expected,Utf8RetainedPayloadExtent[] manifest)
    {
        Assert.Equal(expected.Count,manifest.Length);
        foreach(var extent in manifest){Assert.True(expected.TryGetValue(extent.Value,out var bytes));Assert.Equal(bytes,extent.Bytes);}
        Assert.Equal(manifest.Length,manifest.Select(static extent=>extent.Value).Distinct(ReferenceEqualityComparer.Instance).Count());
    }
    private static Utf8BoundedPreparationCache<string,object[]> Cache(ulong maximum)=>
        Utf8BoundedPreparationCache<string,object[]>.CreateRootAndDescendantRetained(8,StringComparer.Ordinal,64*1024,maximum);
    private static object[] Add(Utf8BoundedPreparationCache<string,object[]> cache,string key,object[] roots)=>
        cache.GetOrAdd(key,roots,static(_,value)=>value,default(Utf8ConstructionTextNoControl),default(RuntimeStorage));
    private readonly struct RuntimeStorage:IUtf8RetainedStorage<string,object[]>
    {
        public bool IsCertified=>true;
        // Borrowed test root vector and opaque CLR descendants are outside scope.
        public Utf8RetainedStorageScope Scope=>Utf8RetainedStorageScope.RootsAndBoundedDescendants;
        public ulong GetKeyCharge(string key) => Utf8ConstructionBudget.StringBytes(key.Length);
        public bool TryGetValueCharge<TControl>(object[] value, ulong maximum, TControl control, out ulong charge)
            where TControl : struct, IUtf8ConstructionTextControl
        {
            using var storage = Utf8GraphInspection<TControl>.Create(maximum, control);
            CollectRoots(value,storage);
            charge = storage.FinishCharge();
            return !storage.Exceeded;
        }
    }

    internal static Dictionary<object,ulong> InspectRuntimeGraph(Utf8Regex regex)
    {
        var program=Assert.IsType<Utf8RegexProgram>(Field(regex,"_program"));
        return InspectRuntimeGraph(program,Field(regex,"_fusedAsciiBoundaryLiteralFamilyCounter"));
    }

    private static Dictionary<object,ulong> InspectRuntimeGraph(params object?[] roots)
    {
        var seen=new Dictionary<object,ulong>(ReferenceEqualityComparer.Instance);
        var pending=new Queue<object>(); foreach(var root in roots) if(root is not null) pending.Enqueue(root);
        while(pending.TryDequeue(out var value))
        {
            var type=value.GetType();
            if(type.IsPrimitive||type.IsEnum||seen.ContainsKey(value)) continue;
            if(ReferenceEquals(value,StringComparer.Ordinal)||ReferenceEquals(value,EqualityComparer<int>.Default)) continue;
            if(value is Utf8PreparedRegex prepared)
            {
                foreach(var pair in Utf8PreparedRetainedGraphTests.InspectPreparedGraph(prepared))
                    if(!seen.TryAdd(pair.Key,pair.Value)) Assert.Equal(seen[pair.Key],pair.Value);
                continue;
            }
            if(value is string text)
            { if(text.Length>0) seen.Add(value,Utf8ConstructionBudget.StringBytes(text.Length)); continue; }
            if(value is Array array)
            {
                if(array.Length==0) continue;
                seen.Add(array,ArrayBytes(array));
                foreach(var child in array) if(child is not null) pending.Enqueue(child);
                continue;
            }
            var fields=Fields(type);
            if(type.IsGenericType&&type.GetGenericTypeDefinition()==typeof(Utf8EmittedDelegate<>))
            {
                var emitted=Assert.IsAssignableFrom<Delegate>(type.GetProperty("Callback")?.GetValue(value));
                var bytes=Utf8EmittedManagedStorageTests.InspectManagedCopies(emitted);
                Assert.Equal(bytes,type.GetProperty("ManagedArrayBytes")?.GetValue(value));
                var extent=80ul+bytes;
                if(!seen.TryAdd(emitted,extent))Assert.Equal(extent,seen[emitted]);
                continue;
            }
            if(value is Delegate callback)
            {
                // Only direct Delegate/MulticastDelegate state is qualified here.
                // Dynamic method/resolver/JIT descendants remain a declared boundary.
                Assert.Null(callback.Target);
                Assert.True(callback.HasSingleTarget);
                Assert.Equal(new[]{typeof(object),typeof(object),typeof(object),typeof(nint),typeof(nint),typeof(nint)}.Select(static t=>t.FullName).Order(),
                    fields.Select(static f=>f.FieldType.FullName).Order());
                seen.Add(value,Utf8ConstructionBudget.ObjectBytes<(object,object,nint,nint,object,nint)>());
                continue;
            }
            if(!type.IsValueType)
            {
                Assert.Equal(typeof(Utf8Regex).Assembly,type.Assembly);
                seen.Add(value,MetadataBytes(type));
                Assert.All(fields,static field=>Assert.True(field.IsInitOnly));
            }
            foreach(var field in fields)
                if(!field.FieldType.IsPrimitive&&!field.FieldType.IsEnum&&field.GetValue(value) is {} child) pending.Enqueue(child);
        }
        return seen;
    }

    private static ulong MetadataBytes(Type type)
    {
        var state=type.GetNestedType("ConstructionState",BindingFlags.NonPublic);
        if(state is not null) return StateBytes(state);
        if(typeof(Utf8StructuralVerifierRuntime).IsAssignableFrom(type))
            return Utf8ConstructionBudget.ObjectBytes<Utf8StructuralVerifierRuntime.ConstructionState>();
        if(typeof(Utf8StructuralLinearRuntime).IsAssignableFrom(type)) return Utf8ConstructionBudget.ObjectBytes<Utf8PreparedRegex>();
        return type.Name switch
        {
            nameof(Utf8ExactLiteralCompiledEngineRuntime) or nameof(Utf8LiteralFamilyCompiledEngineRuntime)
                =>Utf8ConstructionBudget.ObjectBytes<Utf8LiteralCompiledEngineRuntime>(),
            nameof(Utf8EmittedWholeMatcher)=>Utf8ConstructionBudget.ObjectBytes<Utf8EmittedDelegate<Utf8EmittedWholeMatcher.MatchDelegate>>(),
            nameof(Utf8EmittedDeterministicMatcher)=>Utf8ConstructionBudget.ObjectBytes<
                (Utf8EmittedDelegate<Utf8EmittedDeterministicMatcher.FindNextDelegate>,byte[],AsciiCharClass[],bool)>(),
            "CompositeTrailingLiteralSearch"=>Utf8ConstructionBudget.ObjectBytes<(PreparedSubstringSearch,int,int)>(),
            nameof(PreparedByteSet)=>Utf8ConstructionBudget.ObjectBytes<PreparedByteSetState>(),
            nameof(PreparedAsciiIgnoreCaseLiteralSetSearchData)=>StateBytes(typeof(Utf8Regex).Assembly.GetType(
                "Lokad.Utf8Regex.Internal.Search.PreparedAsciiIgnoreCaseLiteralSetSearchDataState",true)),
            nameof(Utf8CaptureCatalog)=>80,
            "Utf8ConstructionReadOnlyMap`2"=>64,
            _=>throw new InvalidOperationException("Unqualified runtime referent: "+type.FullName),
        };
    }

    private static ulong StateBytes(Type? state)
    {
        Assert.NotNull(state);
        var method=typeof(Utf8ConstructionBudget).GetMethod(nameof(Utf8ConstructionBudget.ObjectBytes),BindingFlags.Static|BindingFlags.NonPublic);
        Assert.NotNull(method); return Assert.IsType<ulong>(method.MakeGenericMethod(state).Invoke(null,[]));
    }
    private static ulong ArrayBytes(Array array)
    {
        var method=typeof(Utf8ConstructionBudget).GetMethod(nameof(Utf8ConstructionBudget.ArrayBytes),BindingFlags.Static|BindingFlags.NonPublic);
        Assert.NotNull(method); return Assert.IsType<ulong>(method.MakeGenericMethod(array.GetType().GetElementType()).Invoke(null,[array.Length]));
    }
    private static FieldInfo[] Fields(Type type)
    {
        var fields=new List<FieldInfo>();
        for(var current=type;current is not null&&current!=typeof(object);current=current.BaseType)
            fields.AddRange(current.GetFields(Instance|BindingFlags.DeclaredOnly));
        return fields.ToArray();
    }
    private static object? Field(object value,string name)
    {
        var field=Fields(value.GetType()).Single(f=>f.Name==name); return field.GetValue(value);
    }
    private const BindingFlags Instance=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
}

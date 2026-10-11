using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Caching;

// Complete library-owned scalar program graph. Borrowed framework comparer
// singletons have independent ownership. Profile delegates require their own
// typed collector; they cannot receive a zero-byte opaque graph certificate.
internal static class Utf8ScalarProgramRetainedStorage
{
    internal static void Collect<TControl>(Utf8SliceProgram program, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        using var pending = Utf8ConstructionBuffer<object>.Create(Utf8ConstructionScope.CurrentBudget);
        pending.Push(program);
        while (!storage.Exceeded && pending.TryPop(out var value))
        {
            switch (value)
            {
                case Utf8SliceProgram body:
                    if (body.BackreferenceLowerings.Length != 0)
                        throw new InvalidOperationException("Profile scalar delegates require their owning profile collector.");
                    if (!storage.TryAdd(body, Utf8SliceProgram.ObjectBytes)) break;
                    storage.Array(body.Instructions);
                    storage.Array(body.NonBacktrackingEpsilonOrder);
                    storage.Array(body.BackreferenceLowerings);
                    storage.Array(body.SetMasks);
                    foreach (var mask in body.SetMasks) storage.Array(mask);
                    storage.Array(body.CategoryTables);
                    foreach (var table in body.CategoryTables) { storage.Check(); pending.Push(table); }
                    storage.Array(body.CategorySetMembers);
                    foreach (var members in body.CategorySetMembers)
                    {
                        storage.Array(members);
                        foreach (var member in members) { storage.Check(); pending.Push(member); }
                    }
                    storage.Array(body.Assertions);
                    foreach (var assertion in body.Assertions) { storage.Check(); pending.Push(assertion); }
                    storage.Array(body.GroupNumbers);
                    storage.Array(body.GroupNames);
                    foreach (var name in body.GroupNames) storage.Text(name);
                    body.CaptureCatalog?.CollectStorage(storage);
                    break;
                case Utf8SliceAssertion assertion:
                    if (storage.TryAdd(assertion, Utf8SliceAssertion.ObjectBytes)) pending.Push(assertion.Body);
                    break;
                case Utf8SliceCategoryTable table:
                    if (!storage.TryAdd(table, Utf8SliceCategoryTable.ObjectBytes)) break;
                    storage.Array(table.Ranges);
                    if (table.ClassProgram is { } classProgram) pending.Push(classProgram);
                    break;
                case Utf8SliceNode.CategorySetMember member:
                    if (storage.TryAdd(member, Utf8ConstructionBudget.ObjectBytes<(Utf8SliceCategoryTable, bool)>()))
                        pending.Push(member.Table);
                    break;
                case Utf8SliceClassProgram subtraction:
                    subtraction.CollectStorage(storage, pending);
                    break;
                default:
                    throw new InvalidOperationException("Unqualified scalar retained graph child.");
            }
        }
    }
}

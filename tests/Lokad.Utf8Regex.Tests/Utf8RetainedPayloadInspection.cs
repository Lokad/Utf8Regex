using System.Reflection;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// Graph membership receipts exist only in independent qualification tooling.
// Production retains numeric program/entry charges and no traversal identities.
internal readonly record struct Utf8RetainedPayloadExtent(object Value, ulong Bytes);

internal static class Utf8RetainedPayloadInspection
{
    internal static Utf8RetainedPayloadExtent[] Finish<TControl>(this Utf8GraphInspection<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        if (storage.Exceeded) return [];
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        var seenField = storage.GetType().GetField("_seen", fields);
        Assert.NotNull(seenField);
        var map = Assert.IsType<Utf8ConstructionMap<Utf8GraphIdentity, ulong>>(seenField.GetValue(storage));
        var entriesField = map.GetType().GetField("_entries", fields);
        Assert.NotNull(entriesField);
        var entries = Assert.IsType<Utf8ConstructionMap<Utf8GraphIdentity, ulong>.Entry[]>(entriesField.GetValue(map));
        var values = entries.Where(static entry => entry.Used)
            .Select(static entry => new Utf8RetainedPayloadExtent(entry.Key.Value, entry.Value)).ToArray();
        Assert.Equal(map.Count, values.Length);
        Assert.Equal(storage.FinishCharge(), values.Aggregate(0UL, static (total, extent) => total + extent.Bytes));
        return values;
    }
}

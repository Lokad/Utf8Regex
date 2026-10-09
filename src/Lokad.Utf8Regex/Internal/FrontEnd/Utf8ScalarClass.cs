namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>Owned scalar class data. Each subtraction segment has a negation,
/// range-endpoint count and predicate count followed by half-open scalar ranges
/// and predicates. Counts and endpoints are full integers; U+110000 is the
/// exclusive upper bound. No subject is interpreted as UTF-16 units.
/// </summary>
internal sealed class Utf8ScalarClass
{
    private readonly record struct ConstructionState(int[] Data, string? NativeProjection);
    private Utf8ScalarClass(ConstructionState state)
    {
        Data = state.Data;
        NativeProjection = state.NativeProjection;
    }

    internal int[] Data { get; }

    // Temporary preparation projection for existing qualified native kernels.
    // A class too large for that format remains fully served by the scalar VM.
    // Retire this projection with its remaining native preparation consumers.
    internal string? NativeProjection { get; }

    internal static Utf8ScalarClass Create(Utf8ConstructionBuffer<int> data)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var projection = TryProject(data.Span, scope.Budget);
        var result = scope.Budget.AllocateObject(new ConstructionState(data.Take(), projection),
            static state => new Utf8ScalarClass(state));
        scope.Complete();
        return result;
    }

    // Import borrowed predefined/native fixture payloads once during preparation.
    // Malformed internal data declines; it is never a second execution route.
    internal static Utf8ScalarClass? TryImport(string wire)
    {
        using var scope = Utf8ConstructionScope.Enter();
        using var data = Utf8ConstructionBuffer<int>.Create(scope.Budget);
        var position = 0;
        while (position < wire.Length)
        {
            scope.Budget.ChargeWork(1);
            if (wire.Length - position < 3) return null;
            var negated = wire[position++] != 0;
            var rangeCount = (int)wire[position++];
            var categoryCount = (int)wire[position++];
            if (rangeCount + categoryCount > wire.Length - position) return null;
            using var ranges = Utf8ConstructionBuffer<int>.Create(scope.Budget);
            for (var index = 0; index < rangeCount; index += 2)
            {
                scope.Budget.ChargeWork(1);
                var low = (int)wire[position + index];
                var high = index + 1 == rangeCount || wire[position + index + 1] == 0
                    ? 0x10000 : wire[position + index + 1];
                if (high <= low) return null;
                ranges.Add(low); ranges.Add(high);
            }
            position += rangeCount;
            using var categories = Utf8ConstructionBuffer<int>.Create(scope.Budget);
            if (!ImportCategories(wire.AsSpan(position, categoryCount), ranges, categories)) return null;
            position += categoryCount;
            data.Add(negated ? 1 : 0); data.Add(ranges.Count); data.Add(categories.Count);
            data.AddRange(ranges.Span); data.AddRange(categories.Span);
        }
        if (data.Count == 0) return null;
        var result = scope.Budget.AllocateObject(new ConstructionState(data.Take(), wire),
            static state => new Utf8ScalarClass(state));
        scope.Complete();
        return result;
    }

    internal static bool ImportCategories(ReadOnlySpan<char> encoded,
        Utf8ConstructionBuffer<int> ranges, Utf8ConstructionBuffer<int> categories)
    {
        var owner = Utf8ConstructionScope.CurrentBudget;
        for (var index = 0; index < encoded.Length; index++)
        {
            owner.ChargeWork(1);
            if (encoded[index] == Utf8OwnedCharClass.ScalarRangePredicateCode)
            {
                if (++index >= encoded.Length) return false;
                var count = encoded[index];
                if ((count & 1) != 0 || count > (encoded.Length - index - 1) / 2) return false;
                for (var pair = 0; pair < count; pair += 2)
                {
                    owner.ChargeWork(1);
                    var low = encoded[++index] | encoded[++index] << 16;
                    var high = encoded[++index] | encoded[++index] << 16;
                    if (low < 0 || high > 0x110000 || low >= high) return false;
                    ranges.Add(low); ranges.Add(high);
                }
                continue;
            }
            if (encoded[index] == Utf8OwnedCharClass.RangePredicateCode)
            {
                if (index + 2 >= encoded.Length) return false;
                var negated = encoded[++index] != 0;
                var count = encoded[++index];
                if (count > encoded.Length - index - 1) return false;
                categories.Add(Utf8OwnedCharClass.RangePredicateCode);
                categories.Add(negated ? 1 : 0); categories.Add(count + (count & 1));
                for (var pair = 0; pair < count; pair += 2)
                {
                    owner.ChargeWork(1);
                    var low = (int)encoded[++index];
                    var high = pair + 1 == count ? 0x10000 : (int)encoded[++index];
                    if (high == 0) high = 0x10000;
                    if (low >= high) return false;
                    categories.Add(low); categories.Add(high);
                }
                continue;
            }
            var code = (short)encoded[index];
            if (code is 100 or -100) { categories.Add(code); continue; }
            if (code == 0)
            {
                categories.Add(0);
                var start = index;
                while (++index < encoded.Length && encoded[index] != 0)
                {
                    owner.ChargeWork(1);
                    var member = (short)encoded[index];
                    if (Math.Abs((int)member) is < 1 or > 30) return false;
                    categories.Add(member);
                }
                if (index == encoded.Length || index == start + 1) return false;
                categories.Add(0); continue;
            }
            if (Math.Abs((int)code) is < 1 or > 30) return false;
            categories.Add(code);
        }
        return true;
    }

    private static string? TryProject(ReadOnlySpan<int> data, Utf8ConstructionBudget owner)
    {
        using var wire = Utf8ConstructionBuffer<char>.Create(owner);
        var position = 0;
        while (position < data.Length)
        {
            owner.ChargeWork(1);
            var negated = data[position++];
            var rangeCount = data[position++];
            var categoryCount = data[position++];
            var ranges = data.Slice(position, rangeCount); position += rangeCount;
            var categories = data.Slice(position, categoryCount); position += categoryCount;
            var bmpCount = 0; var wideCount = 0;
            for (var index = 0; index < ranges.Length; index += 2)
            {
                owner.ChargeWork(1);
                var low = ranges[index]; var high = ranges[index + 1];
                if (low >= 0xd800 && high <= 0xe000) { wideCount += 2; continue; }
                if (low < 0x10000) bmpCount += high < 0x10000 ? 2 : 1;
                if (high > 0x10000) wideCount += 2;
            }
            var projectedCategories = (long)categoryCount + (wideCount == 0 ? 0 : 2 + 2L * wideCount);
            if (bmpCount > char.MaxValue || wideCount > char.MaxValue || projectedCategories > char.MaxValue)
                return null;
            wire.Add((char)negated); wire.Add((char)bmpCount); wire.Add((char)projectedCategories);
            for (var index = 0; index < ranges.Length; index += 2)
            {
                owner.ChargeWork(1);
                var low = ranges[index]; var high = ranges[index + 1];
                if (low >= 0xd800 && high <= 0xe000) continue;
                if (low < 0x10000) { wire.Add((char)low); if (high < 0x10000) wire.Add((char)high); }
            }
            // Category codes are small signed integers. Range predicates here
            // come only from BMP property tables; matching keeps full endpoints.
            for (var index = 0; index < categories.Length; index++)
            {
                owner.ChargeWork(1);
                if (categories[index] != Utf8OwnedCharClass.RangePredicateCode)
                    { wire.Add(unchecked((char)categories[index])); continue; }
                wire.Add(Utf8OwnedCharClass.RangePredicateCode);
                wire.Add((char)categories[++index]);
                var count = categories[++index]; wire.Add((char)count);
                for (var endpoint = 0; endpoint < count; endpoint++)
                {
                    owner.ChargeWork(1);
                    wire.Add(unchecked((char)categories[++index]));
                }
            }
            if (wideCount == 0) continue;
            wire.Add(Utf8OwnedCharClass.ScalarRangePredicateCode); wire.Add((char)wideCount);
            for (var index = 0; index < ranges.Length; index += 2)
            {
                owner.ChargeWork(1);
                var low = ranges[index]; var high = ranges[index + 1];
                if (low >= 0xd800 && high <= 0xe000) AppendWide(low, high, wire);
                else if (high > 0x10000) AppendWide(Math.Max(low, 0x10000), high, wire);
            }
        }
        return owner.CopyText(wire.Span);

        static void AppendWide(int low, int high, Utf8ConstructionBuffer<char> target)
        {
            target.Add((char)low); target.Add((char)(low >> 16));
            target.Add((char)high); target.Add((char)(high >> 16));
        }
    }
}

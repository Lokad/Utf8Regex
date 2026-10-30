using System.Globalization;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>A capture declaration awaiting unnamed-first numbering.</summary>
internal readonly record struct Utf8CaptureDefinition(string Name, int DeclaredNumber, bool IsAutomatic, int OpenPosition);

/// <summary>Shared capture identities for the owned front end and executable slice.</summary>
internal sealed class Utf8CaptureCatalog
{
    private readonly record struct ConstructionState(
        int[] DefinitionNumbers, int[] GroupNumbers, string[] GroupNames,
        Utf8ConstructionReadOnlyMap<string, int>? NameToNumber,
        Utf8ConstructionReadOnlyMap<int, int>? SparseMapping,
        bool HasSharedCaptureDeclarations, bool HasExplicitCaptureNumbers,
        KeyValuePair<byte[], int>[]? Utf8Names);

    private readonly record struct AutomaticDefinition(int Index, int OpenPosition);

    private Utf8CaptureCatalog(ConstructionState state)
    {
        DefinitionNumbers = state.DefinitionNumbers;
        GroupNumbers = state.GroupNumbers;
        GroupNames = state.GroupNames;
        NameToNumber = state.NameToNumber;
        SparseMapping = state.SparseMapping;
        HasSharedCaptureDeclarations = state.HasSharedCaptureDeclarations;
        HasExplicitCaptureNumbers = state.HasExplicitCaptureNumbers;
        Utf8Names = state.Utf8Names;
    }

    internal static Utf8CaptureCatalog Create(int unnamedCount, ReadOnlySpan<Utf8CaptureDefinition> definitions,
        bool hasReferencedCaptures)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            // The common unnamed-only catalog needs no scratch collections.
            if (definitions.IsEmpty)
            {
                var numbers = budget.AllocateArray<int>(checked(unnamedCount + 1), false);
                var names = budget.AllocateArray<string>(numbers.Length, false);
                for (var slot = 0; slot < numbers.Length; slot++)
                {
                    budget.ChargeWork(1);
                    numbers[slot] = slot;
                    names[slot] = NumberName(slot, budget);
                }
                var dense = budget.AllocateObject(new ConstructionState([], numbers, names, null, null,
                    hasReferencedCaptures, false, null), static state => new Utf8CaptureCatalog(state));
                scope.Complete();
                return dense;
            }

            using var arena = Utf8ConstructionArena.Create(budget);
            var definitionNumbers = arena.Allocate<int>(definitions.Length, true);
            var automaticCount = 0;
            var explicitNumbers = false;
            for (var index = 0; index < definitions.Length; index++)
            {
                budget.ChargeWork(1);
                var definition = definitions[index];
                if (definition.IsAutomatic) automaticCount++;
                else
                {
                    definitionNumbers[index] = definition.DeclaredNumber;
                    explicitNumbers |= definition.DeclaredNumber > 0 && definition.Name.Length != 0;
                }
            }
            if (automaticCount != 0)
                AssignAutomaticNumbers(unnamedCount, definitions, definitionNumbers, automaticCount, arena);

            var candidates = arena.Allocate<int>(checked(1 + unnamedCount + definitions.Length), false);
            var count = 1;
            for (var slot = 1; slot <= unnamedCount; slot++)
            {
                budget.ChargeWork(1);
                candidates[count++] = slot;
            }
            foreach (var number in definitionNumbers)
            {
                budget.ChargeWork(1);
                if (number > 0) candidates[count++] = number;
            }
            Utf8ConstructionSort.Sort(candidates.AsSpan(0, count), static (left, right) => left.CompareTo(right), budget);
            var unique = 1;
            for (var index = 1; index < count; index++)
            {
                budget.ChargeWork(1);
                if (candidates[index] != candidates[index - 1]) candidates[unique++] = candidates[index];
            }
            var groupNumbers = arena.Allocate<int>(unique, true);
            budget.ChargeWork((ulong)unique);
            candidates.AsSpan(0, unique).CopyTo(groupNumbers);
            var groupNames = arena.Allocate<string>(unique, true);
            for (var index = 0; index < definitions.Length; index++)
            {
                budget.ChargeWork(1);
                if (definitions[index].IsAutomatic)
                    groupNames[FindOrdinal(groupNumbers, definitionNumbers[index], budget)] = definitions[index].Name;
            }
            for (var ordinal = 0; ordinal < unique; ordinal++)
            {
                budget.ChargeWork(1);
                groupNames[ordinal] ??= NumberName(groupNumbers[ordinal], budget);
            }

            Utf8ConstructionReadOnlyMap<int, int>? sparseMapping = null;
            if (groupNumbers[^1] != unique - 1)
            {
                using var sparse = Utf8ConstructionMap<int, int>.Create(budget, EqualityComparer<int>.Default);
                for (var ordinal = 0; ordinal < unique; ordinal++)
                {
                    budget.ChargeWork(1);
                    sparse[groupNumbers[ordinal]] = ordinal;
                }
                sparseMapping = sparse.Freeze(arena);
            }
            Utf8ConstructionReadOnlyMap<string, int>? nameToNumber = null;
            if (automaticCount != 0 || sparseMapping is not null)
            {
                using var names = Utf8ConstructionMap<string, int>.Create(budget, StringComparer.Ordinal);
                names["0"] = 0;
                if (automaticCount == 0 && sparseMapping is not null)
                    for (var ordinal = 0; ordinal < unique; ordinal++)
                    {
                        budget.ChargeWork(1);
                        names[groupNames[ordinal]] = groupNumbers[ordinal];
                    }
                for (var slot = 1; slot <= unnamedCount; slot++)
                {
                    budget.ChargeWork(1);
                    names[groupNames[slot]] = slot;
                }
                for (var index = 0; index < definitions.Length; index++)
                {
                    budget.ChargeWork(1);
                    var definition = definitions[index];
                    var name = definition.IsAutomatic ? definition.Name
                        : groupNames[FindOrdinal(groupNumbers, definition.DeclaredNumber, budget)];
                    names[name] = definitionNumbers[index];
                }
                nameToNumber = names.Freeze(arena);
            }

            // Encode the validated lookup keys once, under the same program
            // owner. Dense numeric-only catalogs need no retained UTF-8 keys.
            KeyValuePair<byte[], int>[]? utf8Names = null;
            if (nameToNumber is not null)
            {
                utf8Names = budget.AllocateArray<KeyValuePair<byte[], int>>(nameToNumber.Count, false);
                var index = 0;
                foreach (var entry in nameToNumber)
                {
                    budget.ChargeWork(1);
                    utf8Names[index++] = new(budget.EncodeUtf8(entry.Key.AsSpan()), entry.Value);
                }
            }
            var catalog = budget.AllocateObject(new ConstructionState(definitionNumbers, groupNumbers, groupNames,
                nameToNumber, sparseMapping, definitions.Length + unnamedCount > unique - 1 || hasReferencedCaptures,
                explicitNumbers, utf8Names), static state => new Utf8CaptureCatalog(state));
            arena.Complete();
            scope.Complete();
            return catalog;
        }
        finally { budget.LeaveDepth(); }
    }

    private static void AssignAutomaticNumbers(int unnamedCount, ReadOnlySpan<Utf8CaptureDefinition> definitions,
        int[] numbers, int automaticCount, Utf8ConstructionArena arena)
    {
        var budget = arena.Budget;
        using var taken = Utf8ConstructionMap<int, bool>.Create(budget, EqualityComparer<int>.Default);
        using var autoSlots = Utf8ConstructionMap<string, int>.Create(budget, StringComparer.Ordinal);
        var automatic = arena.Allocate<AutomaticDefinition>(automaticCount, false);
        for (var slot = 1; slot <= unnamedCount; slot++)
        {
            budget.ChargeWork(1);
            taken[slot] = true;
        }
        var count = 0;
        for (var index = 0; index < definitions.Length; index++)
        {
            budget.ChargeWork(1);
            var definition = definitions[index];
            if (definition.IsAutomatic) automatic[count++] = new(index, definition.OpenPosition);
            else if (definition.DeclaredNumber > 0) taken[definition.DeclaredNumber] = true;
        }
        // Definitions append at closing parentheses; names number in opening order.
        Utf8ConstructionSort.Sort(automatic.AsSpan(), static (left, right) => left.OpenPosition.CompareTo(right.OpenPosition), budget);
        var nextFree = 1;
        foreach (var item in automatic)
        {
            budget.ChargeWork(1);
            var definition = definitions[item.Index];
            if (autoSlots.TryGetValue(definition.Name, out var shared)) numbers[item.Index] = shared;
            else
            {
                while (taken.ContainsKey(nextFree))
                {
                    budget.ChargeWork(1);
                    nextFree++;
                }
                numbers[item.Index] = nextFree;
                taken[nextFree] = true;
                autoSlots[definition.Name] = nextFree;
            }
        }
    }

    private static int FindOrdinal(int[] numbers, int number, Utf8ConstructionBudget budget)
    {
        var left = 0;
        var right = numbers.Length - 1;
        while (left <= right)
        {
            budget.ChargeWork(1);
            var middle = left + (right - left) / 2;
            if (numbers[middle] == number) return middle;
            if (numbers[middle] < number) left = middle + 1;
            else right = middle - 1;
        }
        throw new InvalidOperationException("A declared capture has no catalog ordinal.");
    }

    private static string NumberName(int number, Utf8ConstructionBudget budget)
    {
        if (number == 0) return "0";
        Span<char> text = stackalloc char[10];
        budget.ChargeWork((ulong)text.Length);
        if (!number.TryFormat(text, out var length, provider: CultureInfo.InvariantCulture))
            throw new InvalidOperationException("A capture number must fit its decimal representation.");
        return budget.CopyText(text[..length]);
    }

    internal int[] DefinitionNumbers { get; }
    internal int[] GroupNumbers { get; }
    internal string[] GroupNames { get; }
    internal Utf8ConstructionReadOnlyMap<string, int>? NameToNumber { get; }
    internal Utf8ConstructionReadOnlyMap<int, int>? SparseMapping { get; }
    internal bool HasSharedCaptureDeclarations { get; }
    internal bool HasExplicitCaptureNumbers { get; }
    private KeyValuePair<byte[], int>[]? Utf8Names { get; }

    internal int GroupNumberFromUtf8Name(ReadOnlySpan<byte> name, CancellationToken cancellationToken)
    {
        Utf8PatternMetadata.ValidateName(name, cancellationToken);
        if (name.IsEmpty) return -1;
        if (Utf8Names is { } names)
        {
            foreach (var entry in names)
                if (Utf8PatternMetadata.NameEquals(entry.Key, name, cancellationToken)) return entry.Value;
            cancellationToken.ThrowIfCancellationRequested();
            return -1;
        }
        // Match the existing dense numeric parser, including leading zeros,
        // with bounded cancellation even for an arbitrarily long zero prefix.
        uint numeric = 0;
        for (var index = 0; index < name.Length; index++)
        {
            if ((index & 2047) == 0) cancellationToken.ThrowIfCancellationRequested();
            var digit = (uint)(name[index] - '0');
            if (digit > 9 || numeric > (uint.MaxValue - digit) / 10) return -1;
            numeric = numeric * 10 + digit;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return numeric < GroupNumbers.Length ? (int)numeric : -1;
    }

    internal string GroupNameFromNumber(int number)
    {
        var ordinal = OrdinalFromNumber(number);
        return (uint)ordinal < (uint)GroupNames.Length ? GroupNames[ordinal] : string.Empty;
    }

    internal int OrdinalFromNumber(int number)
        => SparseMapping is null ? (uint)number < (uint)GroupNumbers.Length ? number : -1
            : SparseMapping.TryGetValue(number, out var ordinal) ? ordinal : -1;

    internal int OrdinalFromName(string name) => OrdinalFromNumber(GroupNumberFromName(name));

    internal static bool TryParseNumber(string text, out int number)
        => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number);

    internal int GroupNumberFromName(string name)
    {
        if (NameToNumber is not null) return NameToNumber.TryGetValue(name, out var number) ? number : -1;
        return uint.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var numeric) && numeric < GroupNumbers.Length
            ? (int)numeric : -1;
    }
}

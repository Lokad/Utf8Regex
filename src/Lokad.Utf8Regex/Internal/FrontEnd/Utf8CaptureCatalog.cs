using System.Collections;
using System.Globalization;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>A capture declaration awaiting unnamed-first numbering.</summary>
internal readonly record struct Utf8CaptureDefinition(string Name, int DeclaredNumber, bool IsAutomatic, int OpenPosition);

/// <summary>Shared capture identities for the owned front end and executable slice.</summary>
internal sealed class Utf8CaptureCatalog
{
    internal Utf8CaptureCatalog(int unnamedCount, Utf8CaptureDefinition[] definitions)
    {
        var taken = new HashSet<int>();
        for (var slot = 1; slot <= unnamedCount; slot++) taken.Add(slot);
        foreach (var definition in definitions)
            if (!definition.IsAutomatic && definition.DeclaredNumber > 0) taken.Add(definition.DeclaredNumber);

        DefinitionNumbers = new int[definitions.Length];
        var automatic = new List<int>();
        for (var index = 0; index < definitions.Length; index++)
        {
            if (definitions[index].IsAutomatic) automatic.Add(index);
            else DefinitionNumbers[index] = definitions[index].DeclaredNumber;
        }
        // Definitions may append at closing parentheses; names number in opening order.
        automatic.Sort((left, right) => definitions[left].OpenPosition.CompareTo(definitions[right].OpenPosition));
        var autoSlots = new Dictionary<string, int>(StringComparer.Ordinal);
        var nextFree = 1;
        foreach (var index in automatic)
        {
            var definition = definitions[index];
            if (autoSlots.TryGetValue(definition.Name, out var shared))
            {
                DefinitionNumbers[index] = shared;
                continue;
            }
            while (taken.Contains(nextFree)) nextFree++;
            DefinitionNumbers[index] = nextFree;
            taken.Add(nextFree);
            autoSlots[definition.Name] = nextFree;
        }

        var slots = new SortedSet<int> { 0 };
        for (var slot = 1; slot <= unnamedCount; slot++) slots.Add(slot);
        foreach (var slot in DefinitionNumbers)
            if (slot > 0) slots.Add(slot);
        GroupNumbers = new int[slots.Count];
        slots.CopyTo(GroupNumbers);
        HasSharedCaptureDeclarations = definitions.Length + unnamedCount > slots.Count - 1;

        var autoBySlot = new Dictionary<int, string>();
        for (var index = 0; index < definitions.Length; index++)
            if (definitions[index].IsAutomatic) autoBySlot.TryAdd(DefinitionNumbers[index], definitions[index].Name);
        GroupNames = new string[slots.Count];
        for (var ordinal = 0; ordinal < GroupNumbers.Length; ordinal++)
        {
            var number = GroupNumbers[ordinal];
            GroupNames[ordinal] = autoBySlot.TryGetValue(number, out var name) ? name : number.ToString(CultureInfo.InvariantCulture);
        }

        if (GroupNumbers[^1] != GroupNumbers.Length - 1)
        {
            var sparse = new Hashtable();
            for (var ordinal = 0; ordinal < GroupNumbers.Length; ordinal++) sparse[GroupNumbers[ordinal]] = ordinal;
            SparseMapping = sparse;
        }
        if (automatic.Count != 0 || SparseMapping is not null)
        {
            var names = new Hashtable { { "0", 0 } };
            if (automatic.Count == 0 && SparseMapping is not null)
                foreach (var number in GroupNumbers) names[number.ToString(CultureInfo.InvariantCulture)] = number;
            for (var slot = 1; slot <= unnamedCount; slot++) names[slot.ToString(CultureInfo.InvariantCulture)] = slot;
            for (var index = 0; index < definitions.Length; index++)
            {
                var definition = definitions[index];
                names[definition.IsAutomatic ? definition.Name : definition.DeclaredNumber.ToString(CultureInfo.InvariantCulture)] = DefinitionNumbers[index];
            }
            NameToNumber = names;
        }
    }

    internal int[] DefinitionNumbers { get; }
    internal int[] GroupNumbers { get; }
    internal string[] GroupNames { get; }
    internal Hashtable? NameToNumber { get; }
    internal Hashtable? SparseMapping { get; }
    internal bool HasSharedCaptureDeclarations { get; }

    internal int GroupNumberFromName(string name)
    {
        if (NameToNumber is not null) return NameToNumber[name] is int number ? number : -1;
        return uint.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var numeric) && numeric < GroupNumbers.Length
            ? (int)numeric : -1;
    }

    // Reference syntax accepts a defined number even when the public name table
    // publishes a named alias for that number. This differs from the name API.
    internal int ReferenceNumber(string name)
    {
        if (name.Length != 0 && char.IsAsciiDigit(name[0]))
            return int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && Array.BinarySearch(GroupNumbers, number) >= 0
                ? number : -1;
        return NameToNumber?[name] is int named ? named : -1;
    }
}

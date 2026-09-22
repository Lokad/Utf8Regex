using System.Collections;
using System.Globalization;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>
/// Owned group metadata derived from the vendored front-end parse tree, replicating the
/// pinned BCL transfer and query algorithms (external/runtime 9ffface2f3f, Regex.Init and
/// RegexParser.GroupNameFromNumber) instead of constructing a BCL Regex to ask it.
/// A null parse tree (invalid patterns, which never reach construction) yields no metadata
/// and callers retain their BCL fallback.
/// </summary>
internal sealed class Utf8GroupMetadata
{
    private Utf8GroupMetadata(Hashtable? caps, string[]? capsList, Hashtable? capnames, int capsize, string[] groupNames, int[] groupNumbers)
    {
        Caps = caps;
        CapsList = capsList;
        Capnames = capnames;
        Capsize = capsize;
        GroupNames = groupNames;
        GroupNumbers = groupNumbers;
    }

    public string[] GroupNames { get; }

    public int[] GroupNumbers { get; }

    private Hashtable? Caps { get; }

    private string[]? CapsList { get; }

    private Hashtable? Capnames { get; }

    private int Capsize { get; }

    public static bool TryCreate(RuntimeFrontEnd.RegexTree? tree, out Utf8GroupMetadata? metadata)
    {
        if (tree is null)
        {
            metadata = null;
            return false;
        }

        var caps = tree.CaptureNumberSparseMapping;
        var capsList = tree.CaptureNames;
        string[] groupNames;
        if (capsList is null)
        {
            groupNames = new string[tree.CaptureCount];
            for (var i = 0; i < groupNames.Length; i++)
            {
                groupNames[i] = ((uint)i).ToString();
            }
        }
        else
        {
            groupNames = capsList.AsSpan().ToArray();
        }

        int[] groupNumbers;
        if (caps is null)
        {
            groupNumbers = new int[tree.CaptureCount];
            for (var i = 0; i < groupNumbers.Length; i++)
            {
                groupNumbers[i] = i;
            }
        }
        else
        {
            groupNumbers = new int[caps.Count];
            IDictionaryEnumerator enumerator = caps.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (enumerator.Value is not object boxedValue || enumerator.Key is not object boxedKey)
                {
                    throw new NullReferenceException();
                }

                groupNumbers[(int)boxedValue] = (int)boxedKey;
            }

            Array.Sort(groupNumbers);
        }

        metadata = new Utf8GroupMetadata(
            caps,
            capsList,
            tree.CaptureNameToNumberMapping,
            tree.CaptureCount,
            groupNames,
            groupNumbers);
        return true;
    }

    public int GroupNumberFromName(string name)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (Capnames is not null)
        {
            return Capnames[name] is int mapped ? mapped : -1;
        }

        return uint.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out uint result) && result < Capsize
            ? (int)result
            : -1;
    }

    public string GroupNameFromNumber(int number)
    {
        var caps = Caps;
        var capsList = CapsList;
        if (capsList is null)
        {
            return (uint)number < (uint)Capsize ? ((uint)number).ToString() : string.Empty;
        }

        var mapped = number;
        if (caps is not null)
        {
            if (caps[number] is not int sparseIndex)
            {
                return string.Empty;
            }

            mapped = sparseIndex;
        }

        return (uint)mapped < (uint)capsList.Length ? capsList[mapped] : string.Empty;
    }
}

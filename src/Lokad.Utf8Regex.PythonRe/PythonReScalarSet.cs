using System.Numerics;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe;

// Sparse 256-code-point pages replace the class folder's red/black trees.
// Page arrays, directory/key capacity and every descriptor are admitted first.
internal sealed class PythonReScalarSet : IDisposable
{
    private readonly Utf8ConstructionBudget budget;
    private readonly Utf8ConstructionMap<int, int> indices;
    private readonly Utf8ConstructionBuffer<uint[]> pages;
    private readonly Utf8ConstructionBuffer<int> keys;

    private PythonReScalarSet(Utf8ConstructionBudget owner, Utf8ConstructionMap<int, int> directory,
        Utf8ConstructionBuffer<uint[]> pageList, Utf8ConstructionBuffer<int> keyList)
    {
        budget = owner;
        indices = directory;
        pages = pageList;
        keys = keyList;
    }
    internal static PythonReScalarSet Create(Utf8ConstructionBudget owner)
    {
        var directory = Utf8ConstructionMap<int, int>.Create(owner, EqualityComparer<int>.Default);
        var pageList = Utf8ConstructionBuffer<uint[]>.Create(owner);
        var keyList = Utf8ConstructionBuffer<int>.Create(owner);
        return owner.AllocateObject((owner, directory, pageList, keyList), static state =>
            new PythonReScalarSet(state.owner, state.directory, state.pageList, state.keyList));
    }

    internal void Add(int scalar)
    {
        budget.ChargeWork(1);
        var key = scalar >> 8;
        if (!indices.TryGetValue(key, out var index))
        {
            var page = budget.AllocateArray<uint>(8, false);
            try { pages.Add(page); }
            catch { budget.ReleaseArray(page, false); throw; }
            index = pages.Count - 1;
            keys.Add(key);
            indices.Add(key, index);
        }
        pages[index][(scalar & 255) >> 5] |= 1u << (scalar & 31);
    }

    public Enumerator GetEnumerator()
    {
        keys.Sort(static (left, right) => left.CompareTo(right));
        return new Enumerator(this);
    }

    internal struct Enumerator(PythonReScalarSet set)
    {
        private int _pageIndex;
        private int _wordIndex = -1;
        private uint _bits;
        private int _scalar;
        public readonly int Current => _scalar;
        public bool MoveNext()
        {
            while (_pageIndex < set.keys.Count)
            {
                set.budget.ChargeWork(1);
                if (_bits != 0)
                {
                    _scalar = (set.keys[_pageIndex] << 8) + (_wordIndex << 5) + BitOperations.TrailingZeroCount(_bits);
                    _bits &= _bits - 1;
                    return true;
                }
                if (++_wordIndex == 8) { _pageIndex++; _wordIndex = -1; continue; }
                _bits = set.pages[set.indices[set.keys[_pageIndex]]][_wordIndex];
            }
            return false;
        }
    }

    public void Dispose()
    {
        // Cleanup never probes a metered map or resumes an allocating iterator.
        for (var index = 0; index < pages.Count; index++) budget.ReleaseArray(pages[index], false);
        keys.Dispose();
        pages.Dispose();
        indices.Dispose();
    }
}

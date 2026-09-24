namespace Lokad.Utf8Regex.Internal.FrontEnd;

using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

// Owned find-optimization facts, first slice: lengths and anchors. Pure functions
// over runtime trees of either provenance, differentially pinned against legacy
// instances. Literals, prefixes, and fixed-distance facts arrive in later slices;
// consumers migrate hub by hub. Length arithmetic saturates exactly like the
// pinned formulas: minimums cap at int.MaxValue - 1 past int.MaxValue, maximums
// turn null past int.MaxValue; a lone bound of int.MaxValue stays put.
internal static class Utf8OwnedFindOptimizations
{
    public static int ComputeMinLength(RuntimeFrontEnd.RegexNode node)
    {
        switch (node.Kind)
        {
            case RuntimeFrontEnd.RegexNodeKind.One:
            case RuntimeFrontEnd.RegexNodeKind.Notone:
            case RuntimeFrontEnd.RegexNodeKind.Set:
                return 1;
            case RuntimeFrontEnd.RegexNodeKind.Multi:
                return node.Str?.Length ?? 1;
            case RuntimeFrontEnd.RegexNodeKind.Oneloop:
            case RuntimeFrontEnd.RegexNodeKind.Notoneloop:
            case RuntimeFrontEnd.RegexNodeKind.Setloop:
            case RuntimeFrontEnd.RegexNodeKind.Onelazy:
            case RuntimeFrontEnd.RegexNodeKind.Notonelazy:
            case RuntimeFrontEnd.RegexNodeKind.Setlazy:
            case RuntimeFrontEnd.RegexNodeKind.Oneloopatomic:
            case RuntimeFrontEnd.RegexNodeKind.Notoneloopatomic:
            case RuntimeFrontEnd.RegexNodeKind.Setloopatomic:
                return node.M;
            case RuntimeFrontEnd.RegexNodeKind.Loop:
            case RuntimeFrontEnd.RegexNodeKind.Lazyloop:
                return CapMin((long)node.M * ChildMinLength(node));
            case RuntimeFrontEnd.RegexNodeKind.Capture:
            case RuntimeFrontEnd.RegexNodeKind.Group:
            case RuntimeFrontEnd.RegexNodeKind.Atomic:
            case RuntimeFrontEnd.RegexNodeKind.Concatenate:
                return SumMinLength(node);
            case RuntimeFrontEnd.RegexNodeKind.Alternate:
            case RuntimeFrontEnd.RegexNodeKind.BackreferenceConditional:
            case RuntimeFrontEnd.RegexNodeKind.ExpressionConditional:
                return AlternateMinLength(node);
            default:
                return 0;
        }

        static int ChildMinLength(RuntimeFrontEnd.RegexNode parent)
        {
            return parent.ChildCount == 0 ? 0 : ComputeMinLength(parent.Child(0));
        }

        static int SumMinLength(RuntimeFrontEnd.RegexNode parent)
        {
            long total = 0;
            for (var i = 0; i < parent.ChildCount; i++)
            {
                total += ComputeMinLength(parent.Child(i));
                if (total > int.MaxValue)
                {
                    return int.MaxValue - 1;
                }
            }

            return CapMin(total);
        }

        static int AlternateMinLength(RuntimeFrontEnd.RegexNode parent)
        {
            var minimum = int.MaxValue;
            for (var i = 0; i < parent.ChildCount; i++)
            {
                var branch = ComputeMinLength(parent.Child(i));
                if (branch < minimum)
                {
                    minimum = branch;
                }
            }

            return minimum == int.MaxValue ? 0 : minimum;
        }

        static int CapMin(long value)
        {
            return value > int.MaxValue ? int.MaxValue - 1 : (int)value;
        }
    }

    public static int? ComputeMaxLength(RuntimeFrontEnd.RegexNode node)
    {
        switch (node.Kind)
        {
            case RuntimeFrontEnd.RegexNodeKind.One:
            case RuntimeFrontEnd.RegexNodeKind.Notone:
            case RuntimeFrontEnd.RegexNodeKind.Set:
                return 1;
            case RuntimeFrontEnd.RegexNodeKind.Multi:
                return node.Str?.Length ?? 1;
            case RuntimeFrontEnd.RegexNodeKind.Oneloop:
            case RuntimeFrontEnd.RegexNodeKind.Notoneloop:
            case RuntimeFrontEnd.RegexNodeKind.Setloop:
            case RuntimeFrontEnd.RegexNodeKind.Onelazy:
            case RuntimeFrontEnd.RegexNodeKind.Notonelazy:
            case RuntimeFrontEnd.RegexNodeKind.Setlazy:
            case RuntimeFrontEnd.RegexNodeKind.Oneloopatomic:
            case RuntimeFrontEnd.RegexNodeKind.Notoneloopatomic:
            case RuntimeFrontEnd.RegexNodeKind.Setloopatomic:
                return node.N == int.MaxValue ? null : node.N;
            case RuntimeFrontEnd.RegexNodeKind.Loop:
            case RuntimeFrontEnd.RegexNodeKind.Lazyloop:
                return RepeatMaxLength(node);
            case RuntimeFrontEnd.RegexNodeKind.Capture:
            case RuntimeFrontEnd.RegexNodeKind.Group:
            case RuntimeFrontEnd.RegexNodeKind.Atomic:
            case RuntimeFrontEnd.RegexNodeKind.Concatenate:
                return SumMaxLength(node);
            case RuntimeFrontEnd.RegexNodeKind.Alternate:
            case RuntimeFrontEnd.RegexNodeKind.BackreferenceConditional:
            case RuntimeFrontEnd.RegexNodeKind.ExpressionConditional:
                return AlternateMaxLength(node);
            default:
                return 0;
        }

        static int? RepeatMaxLength(RuntimeFrontEnd.RegexNode parent)
        {
            var child = parent.ChildCount == 0 ? 0 : ComputeMaxLength(parent.Child(0));
            if (parent.N == int.MaxValue || child is null)
            {
                return null;
            }

            return CapMax((long)parent.N * child.Value);
        }

        static int? SumMaxLength(RuntimeFrontEnd.RegexNode parent)
        {
            long total = 0;
            for (var i = 0; i < parent.ChildCount; i++)
            {
                var child = ComputeMaxLength(parent.Child(i));
                if (child is null)
                {
                    return null;
                }

                total += child.Value;
                if (total > int.MaxValue)
                {
                    return null;
                }
            }

            return CapMax(total);
        }

        static int? AlternateMaxLength(RuntimeFrontEnd.RegexNode parent)
        {
            int? maximum = 0;
            for (var i = 0; i < parent.ChildCount; i++)
            {
                var branch = ComputeMaxLength(parent.Child(i));
                if (branch is null)
                {
                    return null;
                }

                if (branch > maximum)
                {
                    maximum = branch;
                }
            }

            return maximum;
        }

        static int? CapMax(long value)
        {
            return value > int.MaxValue ? null : (int?)value;
        }
    }

    public static RuntimeFrontEnd.RegexNodeKind FindLeadingAnchor(RuntimeFrontEnd.RegexNode node)
    {
        switch (node.Kind)
        {
            case RuntimeFrontEnd.RegexNodeKind.Capture:
            case RuntimeFrontEnd.RegexNodeKind.Group:
            case RuntimeFrontEnd.RegexNodeKind.Atomic:
            case RuntimeFrontEnd.RegexNodeKind.Concatenate:
                return node.ChildCount == 0 ? RuntimeFrontEnd.RegexNodeKind.Unknown : FindLeadingAnchor(node.Child(0));
            case RuntimeFrontEnd.RegexNodeKind.Beginning:
            case RuntimeFrontEnd.RegexNodeKind.Bol:
            case RuntimeFrontEnd.RegexNodeKind.Start:
            case RuntimeFrontEnd.RegexNodeKind.EndZ:
            case RuntimeFrontEnd.RegexNodeKind.End:
                return node.Kind;
            default:
                return RuntimeFrontEnd.RegexNodeKind.Unknown;
        }
    }

    public static RuntimeFrontEnd.RegexNodeKind FindTrailingAnchor(RuntimeFrontEnd.RegexNode node)
    {
        switch (node.Kind)
        {
            case RuntimeFrontEnd.RegexNodeKind.Capture:
            case RuntimeFrontEnd.RegexNodeKind.Group:
            case RuntimeFrontEnd.RegexNodeKind.Atomic:
            case RuntimeFrontEnd.RegexNodeKind.Concatenate:
                return node.ChildCount == 0 ? RuntimeFrontEnd.RegexNodeKind.Unknown : FindTrailingAnchor(node.Child(node.ChildCount - 1));
            case RuntimeFrontEnd.RegexNodeKind.EndZ:
            case RuntimeFrontEnd.RegexNodeKind.End:
            case RuntimeFrontEnd.RegexNodeKind.Eol:
                return node.Kind;
            default:
                return RuntimeFrontEnd.RegexNodeKind.Unknown;
        }
    }
}

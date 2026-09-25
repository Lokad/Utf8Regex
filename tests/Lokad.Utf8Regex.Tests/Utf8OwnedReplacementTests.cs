using System.Text;
using Lokad.Utf8Regex.Internal.Replacement;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Tests;

// Owned replacement template parsing: the differential battery pins the owned
// scan, group resolution, and literal-text extraction identical to the legacy
// instance (oracle first, then owned) across the probed template catalog and
// three group contexts.
public sealed class Utf8OwnedReplacementTests
{
    private static readonly int[][] GroupNumbers =
    [
        [0, 1, 2],
        [0],
        [],
    ];

    private static readonly string[][] GroupNames =
    [
        ["one", "two"],
        [],
        [],
    ];

    public static IEnumerable<object?[]> ParseCases()
    {        yield return new object?[] { "", "[]", true, "" };
        yield return new object?[] { "abc", "[Literal:abc:-1:-]", true, "abc" };
        yield return new object?[] { "$", "[Literal:$:-1:-]", true, "$" };
        yield return new object?[] { "$$", "[Literal:$:-1:-]", true, "$" };
        yield return new object?[] { "$$$", "[Literal:$$:-1:-]", true, "$$" };
        yield return new object?[] { "$$$$", "[Literal:$$:-1:-]", true, "$$" };
        yield return new object?[] { "a$", "[Literal:a$:-1:-]", true, "a$" };
        yield return new object?[] { "$a", "[Literal:$a:-1:-]", true, "$a" };
        yield return new object?[] { "$ ", "[Literal:$ :-1:-]", true, "$ " };
        yield return new object?[] { "$-", "[Literal:$-:-1:-]", true, "$-" };
        yield return new object?[] { "$.", "[Literal:$.:-1:-]", true, "$." };
        yield return new object?[] { "$:", "[Literal:$::-1:-]", true, "$:" };
        yield return new object?[] { "$;", "[Literal:$;:-1:-]", true, "$;" };
        yield return new object?[] { "$,", "[Literal:$,:-1:-]", true, "$," };
        yield return new object?[] { "$@", "[Literal:$@:-1:-]", true, "$@" };
        yield return new object?[] { "$&", "[WholeMatch::-1:-]", false, "" };
        yield return new object?[] { "$`", "[LeftPortion::-1:-]", false, "" };
        yield return new object?[] { "$'", "[RightPortion::-1:-]", false, "" };
        yield return new object?[] { "$+", "[LastGroup::-1:-]", false, "" };
        yield return new object?[] { "$_", "[WholeString::-1:-]", false, "" };
        yield return new object?[] { "pre$&post", "[Literal:pre:-1:-;WholeMatch::-1:-;Literal:post:-1:-]", false, "" };
        yield return new object?[] { "$&$&", "[WholeMatch::-1:-;WholeMatch::-1:-]", false, "" };
        yield return new object?[] { "$+$1", "[LastGroup::-1:-;Group::1:-]", false, "" };
        yield return new object?[] { "$0", "[Group::0:-]", false, "" };
        yield return new object?[] { "$1", "[Group::1:-]", false, "" };
        yield return new object?[] { "$2", "[Group::2:-]", false, "" };
        yield return new object?[] { "$9", "[Group::9:-]", false, "" };
        yield return new object?[] { "$12", "[Group::12:-]", false, "" };
        yield return new object?[] { "$123", "[Group::123:-]", false, "" };
        yield return new object?[] { "$01", "[Group::1:-]", false, "" };
        yield return new object?[] { "$00", "[Group::0:-]", false, "" };
        yield return new object?[] { "$1x", "[Group::1:-;Literal:x:-1:-]", false, "" };
        yield return new object?[] { "$12x", "[Group::12:-;Literal:x:-1:-]", false, "" };
        yield return new object?[] { "a$1b", "[Literal:a:-1:-;Group::1:-;Literal:b:-1:-]", false, "" };
        yield return new object?[] { "$1$2", "[Group::1:-;Group::2:-]", false, "" };
        yield return new object?[] { "$2$1", "[Group::2:-;Group::1:-]", false, "" };
        yield return new object?[] { "$2147483647", "[Group::2147483647:-]", false, "" };
        yield return new object?[] { "$2147483648", "[Literal:$2147483648:-1:-]", true, "$2147483648" };
        yield return new object?[] { "$99999999999999999999999", "[Literal:$99999999999999999999999:-1:-]", true, "$99999999999999999999999" };
        yield return new object?[] { "$00000000000000000000001", "[Group::1:-]", false, "" };
        yield return new object?[] { "${1}", "[Group::1:b]", false, "" };
        yield return new object?[] { "${12}", "[Group::12:b]", false, "" };
        yield return new object?[] { "${0}", "[Group::0:b]", false, "" };
        yield return new object?[] { "${1}x", "[Group::1:b;Literal:x:-1:-]", false, "" };
        yield return new object?[] { "${12}x", "[Group::12:b;Literal:x:-1:-]", false, "" };
        yield return new object?[] { "${}", "[Literal:${}:-1:-]", true, "${}" };
        yield return new object?[] { "${x}", "[Group:x:-1:b]", false, "" };
        yield return new object?[] { "${1x}", "[Group:1x:-1:b]", false, "" };
        yield return new object?[] { "${12x34}", "[Group:12x34:-1:b]", false, "" };
        yield return new object?[] { "${ 1}", "[Literal:${ 1}:-1:-]", true, "${ 1}" };
        yield return new object?[] { "${1 }", "[Group:1 :-1:b]", false, "" };
        yield return new object?[] { "${-1}", "[Literal:${-1}:-1:-]", true, "${-1}" };
        yield return new object?[] { "${+1}", "[Literal:${+1}:-1:-]", true, "${+1}" };
        yield return new object?[] { "${name}", "[Group:name:-1:b]", false, "" };
        yield return new object?[] { "${one}", "[Group:one:-1:b]", false, "" };
        yield return new object?[] { "${two}", "[Group:two:-1:b]", false, "" };
        yield return new object?[] { "${three}", "[Group:three:-1:b]", false, "" };
        yield return new object?[] { "${a1_}", "[Group:a1_:-1:b]", false, "" };
        yield return new object?[] { "${_}", "[Group:_:-1:b]", false, "" };
        yield return new object?[] { "${9lives}", "[Group:9lives:-1:b]", false, "" };
        yield return new object?[] { "${name-with-dash}", "[Group:name-with-dash:-1:b]", false, "" };
        yield return new object?[] { "${na me}", "[Group:na me:-1:b]", false, "" };
        yield return new object?[] { "${XYZ}", "[Group:XYZ:-1:b]", false, "" };
        yield return new object?[] { "${One}", "[Group:One:-1:b]", false, "" };
        yield return new object?[] { "${ONE}", "[Group:ONE:-1:b]", false, "" };
        yield return new object?[] { "${one2}", "[Group:one2:-1:b]", false, "" };
        yield return new object?[] { "${2one}", "[Group:2one:-1:b]", false, "" };
        yield return new object?[] { "${1", "[Literal:${1:-1:-]", true, "${1" };
        yield return new object?[] { "${12", "[Literal:${12:-1:-]", true, "${12" };
        yield return new object?[] { "${", "[Literal:${:-1:-]", true, "${" };
        yield return new object?[] { "${name", "[Literal:${name:-1:-]", true, "${name" };
        yield return new object?[] { "${name}x", "[Group:name:-1:b;Literal:x:-1:-]", false, "" };
        yield return new object?[] { "$1${2}$", "[Group::1:-;Group::2:b;Literal:$:-1:-]", false, "" };
        yield return new object?[] { "a${9}b", "[Literal:a:-1:-;Group::9:b;Literal:b:-1:-]", false, "" };
        yield return new object?[] { "x$5y", "[Literal:x:-1:-;Group::5:-;Literal:y:-1:-]", false, "" };
        yield return new object?[] { "${5}${6}", "[Group::5:b;Group::6:b]", false, "" };
        yield return new object?[] { "$`$'", "[LeftPortion::-1:-;RightPortion::-1:-]", false, "" };
        yield return new object?[] { "$_$&", "[WholeString::-1:-;WholeMatch::-1:-]", false, "" };
        yield return new object?[] { "$$$1", "[Literal:$:-1:-;Group::1:-]", false, "" };
        yield return new object?[] { "$$${1}", "[Literal:$:-1:-;Group::1:b]", false, "" };
        yield return new object?[] { "${1}${2}${3}", "[Group::1:b;Group::2:b;Group::3:b]", false, "" };
        yield return new object?[] { "${{1}}", "[Literal:${{1}}:-1:-]", true, "${{1}}" };
        yield return new object?[] { "${}}", "[Literal:${}}:-1:-]", true, "${}}" };
        yield return new object?[] { "${a}b${c}", "[Group:a:-1:b;Literal:b:-1:-;Group:c:-1:b]", false, "" };
        yield return new object?[] { "$5${x}$6", "[Group::5:-;Group:x:-1:b;Group::6:-]", false, "" };
        yield return new object?[] { "${0}${0}", "[Group::0:b;Group::0:b]", false, "" };
        yield return new object?[] { "$01${02}", "[Group::1:-;Group::2:b]", false, "" };
        yield return new object?[] { "${0002}", "[Group::2:b]", false, "" };
        yield return new object?[] { "${00000000000000000000003}", "[Group::3:b]", false, "" };
        yield return new object?[] { "$00000000000000000000000", "[Group::0:-]", false, "" };
        yield return new object?[] { "${00}", "[Group::0:b]", false, "" };
        yield return new object?[] { "${007}", "[Group::7:b]", false, "" };
        yield return new object?[] { "$7$7", "[Group::7:-;Group::7:-]", false, "" };
        yield return new object?[] { "${a}${a}", "[Group:a:-1:b;Group:a:-1:b]", false, "" };
        yield return new object?[] { "$$x", "[Literal:$x:-1:-]", true, "$x" };
        yield return new object?[] { "$$$$$", "[Literal:$$$:-1:-]", true, "$$$" };
        yield return new object?[] { "${ }", "[Literal:${ }:-1:-]", true, "${ }" };
        yield return new object?[] { "${a}${b}${c}", "[Group:a:-1:b;Group:b:-1:b;Group:c:-1:b]", false, "" };
        yield return new object?[] { "$0$0", "[Group::0:-;Group::0:-]", false, "" };
        yield return new object?[] { "${one}${two}${three}", "[Group:one:-1:b;Group:two:-1:b;Group:three:-1:b]", false, "" };
        yield return new object?[] { "x${one}y${two}z", "[Literal:x:-1:-;Group:one:-1:b;Literal:y:-1:-;Group:two:-1:b;Literal:z:-1:-]", false, "" };
        yield return new object?[] { "caf\u00E9${one}end", "[Literal:caf\u00E9:-1:-;Group:one:-1:b;Literal:end:-1:-]", false, "" };
        yield return new object?[] { "${\u00E9}", "[Group:\u00E9:-1:b]", false, "" };
        yield return new object?[] { "${\u03B1}", "[Group:\u03B1:-1:b]", false, "" };
        yield return new object?[] { "${\u4EAC}", "[Group:\u4EAC:-1:b]", false, "" };
        yield return new object?[] { "${_\u0301x}", "[Group:_\u0301x:-1:b]", false, "" };
        yield return new object?[] { "${\u0301x}", "[Group:\u0301x:-1:b]", false, "" };
    }

    public static IEnumerable<object?[]> ResolveCases()
    {        yield return new object?[] { "", 0, "[]", true, "" };
        yield return new object?[] { "", 1, "[]", true, "" };
        yield return new object?[] { "", 2, "[]", true, "" };
        yield return new object?[] { "abc", 0, "[Literal:abc:-1:-]", true, "abc" };
        yield return new object?[] { "abc", 1, "[Literal:abc:-1:-]", true, "abc" };
        yield return new object?[] { "abc", 2, "[Literal:abc:-1:-]", true, "abc" };
        yield return new object?[] { "$", 0, "[Literal:$:-1:-]", true, "$" };
        yield return new object?[] { "$", 1, "[Literal:$:-1:-]", true, "$" };
        yield return new object?[] { "$", 2, "[Literal:$:-1:-]", true, "$" };
        yield return new object?[] { "$$", 0, "[Literal:$:-1:-]", true, "$" };
        yield return new object?[] { "$$", 1, "[Literal:$:-1:-]", true, "$" };
        yield return new object?[] { "$$", 2, "[Literal:$:-1:-]", true, "$" };
        yield return new object?[] { "$$$", 0, "[Literal:$$:-1:-]", true, "$$" };
        yield return new object?[] { "$$$", 1, "[Literal:$$:-1:-]", true, "$$" };
        yield return new object?[] { "$$$", 2, "[Literal:$$:-1:-]", true, "$$" };
        yield return new object?[] { "$$$$", 0, "[Literal:$$:-1:-]", true, "$$" };
        yield return new object?[] { "$$$$", 1, "[Literal:$$:-1:-]", true, "$$" };
        yield return new object?[] { "$$$$", 2, "[Literal:$$:-1:-]", true, "$$" };
        yield return new object?[] { "a$", 0, "[Literal:a$:-1:-]", true, "a$" };
        yield return new object?[] { "a$", 1, "[Literal:a$:-1:-]", true, "a$" };
        yield return new object?[] { "a$", 2, "[Literal:a$:-1:-]", true, "a$" };
        yield return new object?[] { "$a", 0, "[Literal:$a:-1:-]", true, "$a" };
        yield return new object?[] { "$a", 1, "[Literal:$a:-1:-]", true, "$a" };
        yield return new object?[] { "$a", 2, "[Literal:$a:-1:-]", true, "$a" };
        yield return new object?[] { "$ ", 0, "[Literal:$ :-1:-]", true, "$ " };
        yield return new object?[] { "$ ", 1, "[Literal:$ :-1:-]", true, "$ " };
        yield return new object?[] { "$ ", 2, "[Literal:$ :-1:-]", true, "$ " };
        yield return new object?[] { "$-", 0, "[Literal:$-:-1:-]", true, "$-" };
        yield return new object?[] { "$-", 1, "[Literal:$-:-1:-]", true, "$-" };
        yield return new object?[] { "$-", 2, "[Literal:$-:-1:-]", true, "$-" };
        yield return new object?[] { "$.", 0, "[Literal:$.:-1:-]", true, "$." };
        yield return new object?[] { "$.", 1, "[Literal:$.:-1:-]", true, "$." };
        yield return new object?[] { "$.", 2, "[Literal:$.:-1:-]", true, "$." };
        yield return new object?[] { "$:", 0, "[Literal:$::-1:-]", true, "$:" };
        yield return new object?[] { "$:", 1, "[Literal:$::-1:-]", true, "$:" };
        yield return new object?[] { "$:", 2, "[Literal:$::-1:-]", true, "$:" };
        yield return new object?[] { "$;", 0, "[Literal:$;:-1:-]", true, "$;" };
        yield return new object?[] { "$;", 1, "[Literal:$;:-1:-]", true, "$;" };
        yield return new object?[] { "$;", 2, "[Literal:$;:-1:-]", true, "$;" };
        yield return new object?[] { "$,", 0, "[Literal:$,:-1:-]", true, "$," };
        yield return new object?[] { "$,", 1, "[Literal:$,:-1:-]", true, "$," };
        yield return new object?[] { "$,", 2, "[Literal:$,:-1:-]", true, "$," };
        yield return new object?[] { "$@", 0, "[Literal:$@:-1:-]", true, "$@" };
        yield return new object?[] { "$@", 1, "[Literal:$@:-1:-]", true, "$@" };
        yield return new object?[] { "$@", 2, "[Literal:$@:-1:-]", true, "$@" };
        yield return new object?[] { "$&", 0, "[WholeMatch::-1:-]", false, "" };
        yield return new object?[] { "$&", 1, "[WholeMatch::-1:-]", false, "" };
        yield return new object?[] { "$&", 2, "[WholeMatch::-1:-]", false, "" };
        yield return new object?[] { "$`", 0, "[LeftPortion::-1:-]", false, "" };
        yield return new object?[] { "$`", 1, "[LeftPortion::-1:-]", false, "" };
        yield return new object?[] { "$`", 2, "[LeftPortion::-1:-]", false, "" };
        yield return new object?[] { "$'", 0, "[RightPortion::-1:-]", false, "" };
        yield return new object?[] { "$'", 1, "[RightPortion::-1:-]", false, "" };
        yield return new object?[] { "$'", 2, "[RightPortion::-1:-]", false, "" };
        yield return new object?[] { "$+", 0, "[LastGroup::-1:-]", false, "" };
        yield return new object?[] { "$+", 1, "[LastGroup::-1:-]", false, "" };
        yield return new object?[] { "$+", 2, "[LastGroup::-1:-]", false, "" };
        yield return new object?[] { "$_", 0, "[WholeString::-1:-]", false, "" };
        yield return new object?[] { "$_", 1, "[WholeString::-1:-]", false, "" };
        yield return new object?[] { "$_", 2, "[WholeString::-1:-]", false, "" };
        yield return new object?[] { "pre$&post", 0, "[Literal:pre:-1:-;WholeMatch::-1:-;Literal:post:-1:-]", false, "" };
        yield return new object?[] { "pre$&post", 1, "[Literal:pre:-1:-;WholeMatch::-1:-;Literal:post:-1:-]", false, "" };
        yield return new object?[] { "pre$&post", 2, "[Literal:pre:-1:-;WholeMatch::-1:-;Literal:post:-1:-]", false, "" };
        yield return new object?[] { "$&$&", 0, "[WholeMatch::-1:-;WholeMatch::-1:-]", false, "" };
        yield return new object?[] { "$&$&", 1, "[WholeMatch::-1:-;WholeMatch::-1:-]", false, "" };
        yield return new object?[] { "$&$&", 2, "[WholeMatch::-1:-;WholeMatch::-1:-]", false, "" };
        yield return new object?[] { "$+$1", 0, "[LastGroup::-1:-;Group::1:-]", false, "" };
        yield return new object?[] { "$+$1", 1, "[LastGroup::-1:-;Literal:$1:-1:-]", false, "" };
        yield return new object?[] { "$+$1", 2, "[LastGroup::-1:-;Literal:$1:-1:-]", false, "" };
        yield return new object?[] { "$0", 0, "[Group::0:-]", false, "" };
        yield return new object?[] { "$0", 1, "[Group::0:-]", false, "" };
        yield return new object?[] { "$0", 2, "[Literal:$0:-1:-]", true, "$0" };
        yield return new object?[] { "$1", 0, "[Group::1:-]", false, "" };
        yield return new object?[] { "$1", 1, "[Literal:$1:-1:-]", true, "$1" };
        yield return new object?[] { "$1", 2, "[Literal:$1:-1:-]", true, "$1" };
        yield return new object?[] { "$2", 0, "[Group::2:-]", false, "" };
        yield return new object?[] { "$2", 1, "[Literal:$2:-1:-]", true, "$2" };
        yield return new object?[] { "$2", 2, "[Literal:$2:-1:-]", true, "$2" };
        yield return new object?[] { "$9", 0, "[Literal:$9:-1:-]", true, "$9" };
        yield return new object?[] { "$9", 1, "[Literal:$9:-1:-]", true, "$9" };
        yield return new object?[] { "$9", 2, "[Literal:$9:-1:-]", true, "$9" };
        yield return new object?[] { "$12", 0, "[Literal:$12:-1:-]", true, "$12" };
        yield return new object?[] { "$12", 1, "[Literal:$12:-1:-]", true, "$12" };
        yield return new object?[] { "$12", 2, "[Literal:$12:-1:-]", true, "$12" };
        yield return new object?[] { "$123", 0, "[Literal:$123:-1:-]", true, "$123" };
        yield return new object?[] { "$123", 1, "[Literal:$123:-1:-]", true, "$123" };
        yield return new object?[] { "$123", 2, "[Literal:$123:-1:-]", true, "$123" };
        yield return new object?[] { "$01", 0, "[Group::1:-]", false, "" };
        yield return new object?[] { "$01", 1, "[Literal:$1:-1:-]", true, "$1" };
        yield return new object?[] { "$01", 2, "[Literal:$1:-1:-]", true, "$1" };
        yield return new object?[] { "$00", 0, "[Group::0:-]", false, "" };
        yield return new object?[] { "$00", 1, "[Group::0:-]", false, "" };
        yield return new object?[] { "$00", 2, "[Literal:$0:-1:-]", true, "$0" };
        yield return new object?[] { "$1x", 0, "[Group::1:-;Literal:x:-1:-]", false, "" };
        yield return new object?[] { "$1x", 1, "[Literal:$1x:-1:-]", true, "$1x" };
        yield return new object?[] { "$1x", 2, "[Literal:$1x:-1:-]", true, "$1x" };
        yield return new object?[] { "$12x", 0, "[Literal:$12x:-1:-]", true, "$12x" };
        yield return new object?[] { "$12x", 1, "[Literal:$12x:-1:-]", true, "$12x" };
        yield return new object?[] { "$12x", 2, "[Literal:$12x:-1:-]", true, "$12x" };
        yield return new object?[] { "a$1b", 0, "[Literal:a:-1:-;Group::1:-;Literal:b:-1:-]", false, "" };
        yield return new object?[] { "a$1b", 1, "[Literal:a$1b:-1:-]", true, "a$1b" };
        yield return new object?[] { "a$1b", 2, "[Literal:a$1b:-1:-]", true, "a$1b" };
        yield return new object?[] { "$1$2", 0, "[Group::1:-;Group::2:-]", false, "" };
        yield return new object?[] { "$1$2", 1, "[Literal:$1$2:-1:-]", true, "$1$2" };
        yield return new object?[] { "$1$2", 2, "[Literal:$1$2:-1:-]", true, "$1$2" };
        yield return new object?[] { "$2$1", 0, "[Group::2:-;Group::1:-]", false, "" };
        yield return new object?[] { "$2$1", 1, "[Literal:$2$1:-1:-]", true, "$2$1" };
        yield return new object?[] { "$2$1", 2, "[Literal:$2$1:-1:-]", true, "$2$1" };
        yield return new object?[] { "$2147483647", 0, "[Literal:$2147483647:-1:-]", true, "$2147483647" };
        yield return new object?[] { "$2147483647", 1, "[Literal:$2147483647:-1:-]", true, "$2147483647" };
        yield return new object?[] { "$2147483647", 2, "[Literal:$2147483647:-1:-]", true, "$2147483647" };
        yield return new object?[] { "$2147483648", 0, "[Literal:$2147483648:-1:-]", true, "$2147483648" };
        yield return new object?[] { "$2147483648", 1, "[Literal:$2147483648:-1:-]", true, "$2147483648" };
        yield return new object?[] { "$2147483648", 2, "[Literal:$2147483648:-1:-]", true, "$2147483648" };
        yield return new object?[] { "$99999999999999999999999", 0, "[Literal:$99999999999999999999999:-1:-]", true, "$99999999999999999999999" };
        yield return new object?[] { "$99999999999999999999999", 1, "[Literal:$99999999999999999999999:-1:-]", true, "$99999999999999999999999" };
        yield return new object?[] { "$99999999999999999999999", 2, "[Literal:$99999999999999999999999:-1:-]", true, "$99999999999999999999999" };
        yield return new object?[] { "$00000000000000000000001", 0, "[Group::1:-]", false, "" };
        yield return new object?[] { "$00000000000000000000001", 1, "[Literal:$1:-1:-]", true, "$1" };
        yield return new object?[] { "$00000000000000000000001", 2, "[Literal:$1:-1:-]", true, "$1" };
        yield return new object?[] { "${1}", 0, "[Group::1:b]", false, "" };
        yield return new object?[] { "${1}", 1, "[Literal:${1}:-1:b]", true, "${1}" };
        yield return new object?[] { "${1}", 2, "[Literal:${1}:-1:b]", true, "${1}" };
        yield return new object?[] { "${12}", 0, "[Literal:${12}:-1:b]", true, "${12}" };
        yield return new object?[] { "${12}", 1, "[Literal:${12}:-1:b]", true, "${12}" };
        yield return new object?[] { "${12}", 2, "[Literal:${12}:-1:b]", true, "${12}" };
        yield return new object?[] { "${0}", 0, "[Group::0:b]", false, "" };
        yield return new object?[] { "${0}", 1, "[Group::0:b]", false, "" };
        yield return new object?[] { "${0}", 2, "[Literal:${0}:-1:b]", true, "${0}" };
        yield return new object?[] { "${1}x", 0, "[Group::1:b;Literal:x:-1:-]", false, "" };
        yield return new object?[] { "${1}x", 1, "[Literal:${1}x:-1:-]", true, "${1}x" };
        yield return new object?[] { "${1}x", 2, "[Literal:${1}x:-1:-]", true, "${1}x" };
        yield return new object?[] { "${12}x", 0, "[Literal:${12}x:-1:-]", true, "${12}x" };
        yield return new object?[] { "${12}x", 1, "[Literal:${12}x:-1:-]", true, "${12}x" };
        yield return new object?[] { "${12}x", 2, "[Literal:${12}x:-1:-]", true, "${12}x" };
        yield return new object?[] { "${}", 0, "[Literal:${}:-1:-]", true, "${}" };
        yield return new object?[] { "${}", 1, "[Literal:${}:-1:-]", true, "${}" };
        yield return new object?[] { "${}", 2, "[Literal:${}:-1:-]", true, "${}" };
        yield return new object?[] { "${x}", 0, "[Literal:${x}:-1:b]", true, "${x}" };
        yield return new object?[] { "${x}", 1, "[Literal:${x}:-1:b]", true, "${x}" };
        yield return new object?[] { "${x}", 2, "[Literal:${x}:-1:b]", true, "${x}" };
        yield return new object?[] { "${1x}", 0, "[Literal:${1x}:-1:b]", true, "${1x}" };
        yield return new object?[] { "${1x}", 1, "[Literal:${1x}:-1:b]", true, "${1x}" };
        yield return new object?[] { "${1x}", 2, "[Literal:${1x}:-1:b]", true, "${1x}" };
        yield return new object?[] { "${12x34}", 0, "[Literal:${12x34}:-1:b]", true, "${12x34}" };
        yield return new object?[] { "${12x34}", 1, "[Literal:${12x34}:-1:b]", true, "${12x34}" };
        yield return new object?[] { "${12x34}", 2, "[Literal:${12x34}:-1:b]", true, "${12x34}" };
        yield return new object?[] { "${ 1}", 0, "[Literal:${ 1}:-1:-]", true, "${ 1}" };
        yield return new object?[] { "${ 1}", 1, "[Literal:${ 1}:-1:-]", true, "${ 1}" };
        yield return new object?[] { "${ 1}", 2, "[Literal:${ 1}:-1:-]", true, "${ 1}" };
        yield return new object?[] { "${1 }", 0, "[Literal:${1 }:-1:b]", true, "${1 }" };
        yield return new object?[] { "${1 }", 1, "[Literal:${1 }:-1:b]", true, "${1 }" };
        yield return new object?[] { "${1 }", 2, "[Literal:${1 }:-1:b]", true, "${1 }" };
        yield return new object?[] { "${-1}", 0, "[Literal:${-1}:-1:-]", true, "${-1}" };
        yield return new object?[] { "${-1}", 1, "[Literal:${-1}:-1:-]", true, "${-1}" };
        yield return new object?[] { "${-1}", 2, "[Literal:${-1}:-1:-]", true, "${-1}" };
        yield return new object?[] { "${+1}", 0, "[Literal:${+1}:-1:-]", true, "${+1}" };
        yield return new object?[] { "${+1}", 1, "[Literal:${+1}:-1:-]", true, "${+1}" };
        yield return new object?[] { "${+1}", 2, "[Literal:${+1}:-1:-]", true, "${+1}" };
        yield return new object?[] { "${name}", 0, "[Literal:${name}:-1:b]", true, "${name}" };
        yield return new object?[] { "${name}", 1, "[Literal:${name}:-1:b]", true, "${name}" };
        yield return new object?[] { "${name}", 2, "[Literal:${name}:-1:b]", true, "${name}" };
        yield return new object?[] { "${one}", 0, "[Group::0:b]", false, "" };
        yield return new object?[] { "${one}", 1, "[Literal:${one}:-1:b]", true, "${one}" };
        yield return new object?[] { "${one}", 2, "[Literal:${one}:-1:b]", true, "${one}" };
        yield return new object?[] { "${two}", 0, "[Group::1:b]", false, "" };
        yield return new object?[] { "${two}", 1, "[Literal:${two}:-1:b]", true, "${two}" };
        yield return new object?[] { "${two}", 2, "[Literal:${two}:-1:b]", true, "${two}" };
        yield return new object?[] { "${three}", 0, "[Literal:${three}:-1:b]", true, "${three}" };
        yield return new object?[] { "${three}", 1, "[Literal:${three}:-1:b]", true, "${three}" };
        yield return new object?[] { "${three}", 2, "[Literal:${three}:-1:b]", true, "${three}" };
        yield return new object?[] { "${a1_}", 0, "[Literal:${a1_}:-1:b]", true, "${a1_}" };
        yield return new object?[] { "${a1_}", 1, "[Literal:${a1_}:-1:b]", true, "${a1_}" };
        yield return new object?[] { "${a1_}", 2, "[Literal:${a1_}:-1:b]", true, "${a1_}" };
        yield return new object?[] { "${_}", 0, "[Literal:${_}:-1:b]", true, "${_}" };
        yield return new object?[] { "${_}", 1, "[Literal:${_}:-1:b]", true, "${_}" };
        yield return new object?[] { "${_}", 2, "[Literal:${_}:-1:b]", true, "${_}" };
        yield return new object?[] { "${9lives}", 0, "[Literal:${9lives}:-1:b]", true, "${9lives}" };
        yield return new object?[] { "${9lives}", 1, "[Literal:${9lives}:-1:b]", true, "${9lives}" };
        yield return new object?[] { "${9lives}", 2, "[Literal:${9lives}:-1:b]", true, "${9lives}" };
        yield return new object?[] { "${name-with-dash}", 0, "[Literal:${name-with-dash}:-1:b]", true, "${name-with-dash}" };
        yield return new object?[] { "${name-with-dash}", 1, "[Literal:${name-with-dash}:-1:b]", true, "${name-with-dash}" };
        yield return new object?[] { "${name-with-dash}", 2, "[Literal:${name-with-dash}:-1:b]", true, "${name-with-dash}" };
        yield return new object?[] { "${na me}", 0, "[Literal:${na me}:-1:b]", true, "${na me}" };
        yield return new object?[] { "${na me}", 1, "[Literal:${na me}:-1:b]", true, "${na me}" };
        yield return new object?[] { "${na me}", 2, "[Literal:${na me}:-1:b]", true, "${na me}" };
        yield return new object?[] { "${XYZ}", 0, "[Literal:${XYZ}:-1:b]", true, "${XYZ}" };
        yield return new object?[] { "${XYZ}", 1, "[Literal:${XYZ}:-1:b]", true, "${XYZ}" };
        yield return new object?[] { "${XYZ}", 2, "[Literal:${XYZ}:-1:b]", true, "${XYZ}" };
        yield return new object?[] { "${One}", 0, "[Literal:${One}:-1:b]", true, "${One}" };
        yield return new object?[] { "${One}", 1, "[Literal:${One}:-1:b]", true, "${One}" };
        yield return new object?[] { "${One}", 2, "[Literal:${One}:-1:b]", true, "${One}" };
        yield return new object?[] { "${ONE}", 0, "[Literal:${ONE}:-1:b]", true, "${ONE}" };
        yield return new object?[] { "${ONE}", 1, "[Literal:${ONE}:-1:b]", true, "${ONE}" };
        yield return new object?[] { "${ONE}", 2, "[Literal:${ONE}:-1:b]", true, "${ONE}" };
        yield return new object?[] { "${one2}", 0, "[Literal:${one2}:-1:b]", true, "${one2}" };
        yield return new object?[] { "${one2}", 1, "[Literal:${one2}:-1:b]", true, "${one2}" };
        yield return new object?[] { "${one2}", 2, "[Literal:${one2}:-1:b]", true, "${one2}" };
        yield return new object?[] { "${2one}", 0, "[Literal:${2one}:-1:b]", true, "${2one}" };
        yield return new object?[] { "${2one}", 1, "[Literal:${2one}:-1:b]", true, "${2one}" };
        yield return new object?[] { "${2one}", 2, "[Literal:${2one}:-1:b]", true, "${2one}" };
        yield return new object?[] { "${1", 0, "[Literal:${1:-1:-]", true, "${1" };
        yield return new object?[] { "${1", 1, "[Literal:${1:-1:-]", true, "${1" };
        yield return new object?[] { "${1", 2, "[Literal:${1:-1:-]", true, "${1" };
        yield return new object?[] { "${12", 0, "[Literal:${12:-1:-]", true, "${12" };
        yield return new object?[] { "${12", 1, "[Literal:${12:-1:-]", true, "${12" };
        yield return new object?[] { "${12", 2, "[Literal:${12:-1:-]", true, "${12" };
        yield return new object?[] { "${", 0, "[Literal:${:-1:-]", true, "${" };
        yield return new object?[] { "${", 1, "[Literal:${:-1:-]", true, "${" };
        yield return new object?[] { "${", 2, "[Literal:${:-1:-]", true, "${" };
        yield return new object?[] { "${name", 0, "[Literal:${name:-1:-]", true, "${name" };
        yield return new object?[] { "${name", 1, "[Literal:${name:-1:-]", true, "${name" };
        yield return new object?[] { "${name", 2, "[Literal:${name:-1:-]", true, "${name" };
        yield return new object?[] { "${name}x", 0, "[Literal:${name}x:-1:-]", true, "${name}x" };
        yield return new object?[] { "${name}x", 1, "[Literal:${name}x:-1:-]", true, "${name}x" };
        yield return new object?[] { "${name}x", 2, "[Literal:${name}x:-1:-]", true, "${name}x" };
        yield return new object?[] { "$1${2}$", 0, "[Group::1:-;Group::2:b;Literal:$:-1:-]", false, "" };
        yield return new object?[] { "$1${2}$", 1, "[Literal:$1${2}$:-1:-]", true, "$1${2}$" };
        yield return new object?[] { "$1${2}$", 2, "[Literal:$1${2}$:-1:-]", true, "$1${2}$" };
        yield return new object?[] { "a${9}b", 0, "[Literal:a${9}b:-1:-]", true, "a${9}b" };
        yield return new object?[] { "a${9}b", 1, "[Literal:a${9}b:-1:-]", true, "a${9}b" };
        yield return new object?[] { "a${9}b", 2, "[Literal:a${9}b:-1:-]", true, "a${9}b" };
        yield return new object?[] { "x$5y", 0, "[Literal:x$5y:-1:-]", true, "x$5y" };
        yield return new object?[] { "x$5y", 1, "[Literal:x$5y:-1:-]", true, "x$5y" };
        yield return new object?[] { "x$5y", 2, "[Literal:x$5y:-1:-]", true, "x$5y" };
        yield return new object?[] { "${5}${6}", 0, "[Literal:${5}${6}:-1:-]", true, "${5}${6}" };
        yield return new object?[] { "${5}${6}", 1, "[Literal:${5}${6}:-1:-]", true, "${5}${6}" };
        yield return new object?[] { "${5}${6}", 2, "[Literal:${5}${6}:-1:-]", true, "${5}${6}" };
        yield return new object?[] { "$`$'", 0, "[LeftPortion::-1:-;RightPortion::-1:-]", false, "" };
        yield return new object?[] { "$`$'", 1, "[LeftPortion::-1:-;RightPortion::-1:-]", false, "" };
        yield return new object?[] { "$`$'", 2, "[LeftPortion::-1:-;RightPortion::-1:-]", false, "" };
        yield return new object?[] { "$_$&", 0, "[WholeString::-1:-;WholeMatch::-1:-]", false, "" };
        yield return new object?[] { "$_$&", 1, "[WholeString::-1:-;WholeMatch::-1:-]", false, "" };
        yield return new object?[] { "$_$&", 2, "[WholeString::-1:-;WholeMatch::-1:-]", false, "" };
        yield return new object?[] { "$$$1", 0, "[Literal:$:-1:-;Group::1:-]", false, "" };
        yield return new object?[] { "$$$1", 1, "[Literal:$$1:-1:-]", true, "$$1" };
        yield return new object?[] { "$$$1", 2, "[Literal:$$1:-1:-]", true, "$$1" };
        yield return new object?[] { "$$${1}", 0, "[Literal:$:-1:-;Group::1:b]", false, "" };
        yield return new object?[] { "$$${1}", 1, "[Literal:$${1}:-1:-]", true, "$${1}" };
        yield return new object?[] { "$$${1}", 2, "[Literal:$${1}:-1:-]", true, "$${1}" };
        yield return new object?[] { "${1}${2}${3}", 0, "[Group::1:b;Group::2:b;Literal:${3}:-1:b]", false, "" };
        yield return new object?[] { "${1}${2}${3}", 1, "[Literal:${1}${2}${3}:-1:-]", true, "${1}${2}${3}" };
        yield return new object?[] { "${1}${2}${3}", 2, "[Literal:${1}${2}${3}:-1:-]", true, "${1}${2}${3}" };
        yield return new object?[] { "${{1}}", 0, "[Literal:${{1}}:-1:-]", true, "${{1}}" };
        yield return new object?[] { "${{1}}", 1, "[Literal:${{1}}:-1:-]", true, "${{1}}" };
        yield return new object?[] { "${{1}}", 2, "[Literal:${{1}}:-1:-]", true, "${{1}}" };
        yield return new object?[] { "${}}", 0, "[Literal:${}}:-1:-]", true, "${}}" };
        yield return new object?[] { "${}}", 1, "[Literal:${}}:-1:-]", true, "${}}" };
        yield return new object?[] { "${}}", 2, "[Literal:${}}:-1:-]", true, "${}}" };
        yield return new object?[] { "${a}b${c}", 0, "[Literal:${a}b${c}:-1:-]", true, "${a}b${c}" };
        yield return new object?[] { "${a}b${c}", 1, "[Literal:${a}b${c}:-1:-]", true, "${a}b${c}" };
        yield return new object?[] { "${a}b${c}", 2, "[Literal:${a}b${c}:-1:-]", true, "${a}b${c}" };
        yield return new object?[] { "$5${x}$6", 0, "[Literal:$5${x}$6:-1:-]", true, "$5${x}$6" };
        yield return new object?[] { "$5${x}$6", 1, "[Literal:$5${x}$6:-1:-]", true, "$5${x}$6" };
        yield return new object?[] { "$5${x}$6", 2, "[Literal:$5${x}$6:-1:-]", true, "$5${x}$6" };
        yield return new object?[] { "${0}${0}", 0, "[Group::0:b;Group::0:b]", false, "" };
        yield return new object?[] { "${0}${0}", 1, "[Group::0:b;Group::0:b]", false, "" };
        yield return new object?[] { "${0}${0}", 2, "[Literal:${0}${0}:-1:-]", true, "${0}${0}" };
        yield return new object?[] { "$01${02}", 0, "[Group::1:-;Group::2:b]", false, "" };
        yield return new object?[] { "$01${02}", 1, "[Literal:$1${2}:-1:-]", true, "$1${2}" };
        yield return new object?[] { "$01${02}", 2, "[Literal:$1${2}:-1:-]", true, "$1${2}" };
        yield return new object?[] { "${0002}", 0, "[Group::2:b]", false, "" };
        yield return new object?[] { "${0002}", 1, "[Literal:${2}:-1:b]", true, "${2}" };
        yield return new object?[] { "${0002}", 2, "[Literal:${2}:-1:b]", true, "${2}" };
        yield return new object?[] { "${00000000000000000000003}", 0, "[Literal:${3}:-1:b]", true, "${3}" };
        yield return new object?[] { "${00000000000000000000003}", 1, "[Literal:${3}:-1:b]", true, "${3}" };
        yield return new object?[] { "${00000000000000000000003}", 2, "[Literal:${3}:-1:b]", true, "${3}" };
        yield return new object?[] { "$00000000000000000000000", 0, "[Group::0:-]", false, "" };
        yield return new object?[] { "$00000000000000000000000", 1, "[Group::0:-]", false, "" };
        yield return new object?[] { "$00000000000000000000000", 2, "[Literal:$0:-1:-]", true, "$0" };
        yield return new object?[] { "${00}", 0, "[Group::0:b]", false, "" };
        yield return new object?[] { "${00}", 1, "[Group::0:b]", false, "" };
        yield return new object?[] { "${00}", 2, "[Literal:${0}:-1:b]", true, "${0}" };
        yield return new object?[] { "${007}", 0, "[Literal:${7}:-1:b]", true, "${7}" };
        yield return new object?[] { "${007}", 1, "[Literal:${7}:-1:b]", true, "${7}" };
        yield return new object?[] { "${007}", 2, "[Literal:${7}:-1:b]", true, "${7}" };
        yield return new object?[] { "$7$7", 0, "[Literal:$7$7:-1:-]", true, "$7$7" };
        yield return new object?[] { "$7$7", 1, "[Literal:$7$7:-1:-]", true, "$7$7" };
        yield return new object?[] { "$7$7", 2, "[Literal:$7$7:-1:-]", true, "$7$7" };
        yield return new object?[] { "${a}${a}", 0, "[Literal:${a}${a}:-1:-]", true, "${a}${a}" };
        yield return new object?[] { "${a}${a}", 1, "[Literal:${a}${a}:-1:-]", true, "${a}${a}" };
        yield return new object?[] { "${a}${a}", 2, "[Literal:${a}${a}:-1:-]", true, "${a}${a}" };
        yield return new object?[] { "$$x", 0, "[Literal:$x:-1:-]", true, "$x" };
        yield return new object?[] { "$$x", 1, "[Literal:$x:-1:-]", true, "$x" };
        yield return new object?[] { "$$x", 2, "[Literal:$x:-1:-]", true, "$x" };
        yield return new object?[] { "$$$$$", 0, "[Literal:$$$:-1:-]", true, "$$$" };
        yield return new object?[] { "$$$$$", 1, "[Literal:$$$:-1:-]", true, "$$$" };
        yield return new object?[] { "$$$$$", 2, "[Literal:$$$:-1:-]", true, "$$$" };
        yield return new object?[] { "${ }", 0, "[Literal:${ }:-1:-]", true, "${ }" };
        yield return new object?[] { "${ }", 1, "[Literal:${ }:-1:-]", true, "${ }" };
        yield return new object?[] { "${ }", 2, "[Literal:${ }:-1:-]", true, "${ }" };
        yield return new object?[] { "${a}${b}${c}", 0, "[Literal:${a}${b}${c}:-1:-]", true, "${a}${b}${c}" };
        yield return new object?[] { "${a}${b}${c}", 1, "[Literal:${a}${b}${c}:-1:-]", true, "${a}${b}${c}" };
        yield return new object?[] { "${a}${b}${c}", 2, "[Literal:${a}${b}${c}:-1:-]", true, "${a}${b}${c}" };
        yield return new object?[] { "$0$0", 0, "[Group::0:-;Group::0:-]", false, "" };
        yield return new object?[] { "$0$0", 1, "[Group::0:-;Group::0:-]", false, "" };
        yield return new object?[] { "$0$0", 2, "[Literal:$0$0:-1:-]", true, "$0$0" };
        yield return new object?[] { "${one}${two}${three}", 0, "[Group::0:b;Group::1:b;Literal:${three}:-1:b]", false, "" };
        yield return new object?[] { "${one}${two}${three}", 1, "[Literal:${one}${two}${three}:-1:-]", true, "${one}${two}${three}" };
        yield return new object?[] { "${one}${two}${three}", 2, "[Literal:${one}${two}${three}:-1:-]", true, "${one}${two}${three}" };
        yield return new object?[] { "x${one}y${two}z", 0, "[Literal:x:-1:-;Group::0:b;Literal:y:-1:-;Group::1:b;Literal:z:-1:-]", false, "" };
        yield return new object?[] { "x${one}y${two}z", 1, "[Literal:x${one}y${two}z:-1:-]", true, "x${one}y${two}z" };
        yield return new object?[] { "x${one}y${two}z", 2, "[Literal:x${one}y${two}z:-1:-]", true, "x${one}y${two}z" };
        yield return new object?[] { "caf\u00E9${one}end", 0, "[Literal:caf\u00E9:-1:-;Group::0:b;Literal:end:-1:-]", false, "" };
        yield return new object?[] { "caf\u00E9${one}end", 1, "[Literal:caf\u00E9${one}end:-1:-]", true, "caf\u00E9${one}end" };
        yield return new object?[] { "caf\u00E9${one}end", 2, "[Literal:caf\u00E9${one}end:-1:-]", true, "caf\u00E9${one}end" };
        yield return new object?[] { "${\u00E9}", 0, "[Literal:${\u00E9}:-1:b]", true, "${\u00E9}" };
        yield return new object?[] { "${\u00E9}", 1, "[Literal:${\u00E9}:-1:b]", true, "${\u00E9}" };
        yield return new object?[] { "${\u00E9}", 2, "[Literal:${\u00E9}:-1:b]", true, "${\u00E9}" };
        yield return new object?[] { "${\u03B1}", 0, "[Literal:${\u03B1}:-1:b]", true, "${\u03B1}" };
        yield return new object?[] { "${\u03B1}", 1, "[Literal:${\u03B1}:-1:b]", true, "${\u03B1}" };
        yield return new object?[] { "${\u03B1}", 2, "[Literal:${\u03B1}:-1:b]", true, "${\u03B1}" };
        yield return new object?[] { "${\u4EAC}", 0, "[Literal:${\u4EAC}:-1:b]", true, "${\u4EAC}" };
        yield return new object?[] { "${\u4EAC}", 1, "[Literal:${\u4EAC}:-1:b]", true, "${\u4EAC}" };
        yield return new object?[] { "${\u4EAC}", 2, "[Literal:${\u4EAC}:-1:b]", true, "${\u4EAC}" };
        yield return new object?[] { "${_\u0301x}", 0, "[Literal:${_\u0301x}:-1:b]", true, "${_\u0301x}" };
        yield return new object?[] { "${_\u0301x}", 1, "[Literal:${_\u0301x}:-1:b]", true, "${_\u0301x}" };
        yield return new object?[] { "${_\u0301x}", 2, "[Literal:${_\u0301x}:-1:b]", true, "${_\u0301x}" };
        yield return new object?[] { "${\u0301x}", 0, "[Literal:${\u0301x}:-1:b]", true, "${\u0301x}" };
        yield return new object?[] { "${\u0301x}", 1, "[Literal:${\u0301x}:-1:b]", true, "${\u0301x}" };
        yield return new object?[] { "${\u0301x}", 2, "[Literal:${\u0301x}:-1:b]", true, "${\u0301x}" };
    }

    [Theory]
    [MemberData(nameof(ParseCases))]
    public void OwnedReplacementParseMatchesLegacy(string template, string expectedTokens, bool expectedLiteralOk, string expectedLiteral)
    {
        var legacy = RuntimeFrontEnd.RegexParser.ParseReplacement(template);
        Assert.Equal(expectedTokens, DumpLegacyTokens(legacy));
        Assert.Equal(expectedLiteralOk, legacy.TryGetLiteralText(out var legacyLiteral));
        Assert.Equal(expectedLiteral, legacyLiteral ?? string.Empty);
        var owned = Utf8ReplacementPattern.Parse(template);
        Assert.Equal(expectedTokens, DumpOwnedTokens(owned));
        Assert.Equal(expectedLiteralOk, owned.TryGetLiteralText(out var ownedLiteral));
        Assert.Equal(expectedLiteral, ownedLiteral ?? string.Empty);
    }

    [Theory]
    [MemberData(nameof(ResolveCases))]
    public void OwnedReplacementResolveMatchesLegacy(string template, int context, string expectedTokens, bool expectedLiteralOk, string expectedLiteral)
    {
        var numbers = GroupNumbers[context];
        var names = GroupNames[context];
        var legacy = RuntimeFrontEnd.RegexParser.ParseReplacement(template);
        var legacyResolved = legacy.ResolveGroupReferences(numbers, names);
        Assert.Equal(expectedTokens, DumpLegacyTokens(legacyResolved));
        Assert.Equal(expectedLiteralOk, legacy.TryGetLiteralText(numbers, names, out var legacyLiteral));
        Assert.Equal(expectedLiteral, legacyLiteral ?? string.Empty);
        var owned = Utf8ReplacementPattern.Parse(template);
        var ownedResolved = owned.ResolveGroupReferences(numbers, names);
        Assert.Equal(expectedTokens, DumpOwnedTokens(ownedResolved));
        Assert.Equal(expectedLiteralOk, owned.TryGetLiteralText(numbers, names, out var ownedLiteral));
        Assert.Equal(expectedLiteral, ownedLiteral ?? string.Empty);
    }

    private static string DumpLegacyTokens(RuntimeFrontEnd.RegexReplacementPattern pattern)
    {
        var builder = new StringBuilder("[");
        var first = true;
        foreach (var token in pattern.Tokens)
        {
            if (!first)
            {
                builder.Append(';');
            }

            first = false;
            builder.Append(token.Kind.ToString()).Append(':').Append(token.Literal ?? string.Empty).Append(':').Append(token.GroupNumber).Append(':').Append(token.IsBraceEnclosed ? 'b' : '-');
        }

        return builder.Append(']').ToString();
    }

    private static string DumpOwnedTokens(Utf8ReplacementPattern pattern)
    {
        var builder = new StringBuilder("[");
        var first = true;
        foreach (var token in pattern.Tokens)
        {
            if (!first)
            {
                builder.Append(';');
            }

            first = false;
            builder.Append(token.Kind.ToString()).Append(':').Append(token.Literal ?? string.Empty).Append(':').Append(token.GroupNumber).Append(':').Append(token.IsBraceEnclosed ? 'b' : '-');
        }

        return builder.Append(']').ToString();
    }
}
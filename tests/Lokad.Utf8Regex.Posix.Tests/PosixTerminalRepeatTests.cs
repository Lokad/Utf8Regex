using System.Text;

namespace Lokad.Utf8Regex.Posix.Tests;

public sealed class PosixTerminalRepeatTests
{
    [Theory]
    [InlineData("Z(é+)()", PosixCompileOptions.None)]
    [InlineData("(é+)()", PosixCompileOptions.None)]
    [InlineData("Z(中+)()", PosixCompileOptions.None)]
    [InlineData("Z(😀+)()", PosixCompileOptions.None)]
    [InlineData("Z([é中😀]+)()", PosixCompileOptions.None)]
    [InlineData("Z(é{0,3})()", PosixCompileOptions.None)]
    [InlineData("Z(é{2,4})()", PosixCompileOptions.None)]
    [InlineData("^Z(.{0,4})()", PosixCompileOptions.None)]
    [InlineData("^Z(.{0,4})()", PosixCompileOptions.Newline)]
    [InlineData("Z([Kk]+)()", PosixCompileOptions.IgnoreCase)]
    [InlineData("Z(é+)()", PosixCompileOptions.IgnoreCase)]
    [InlineData("((Z))(é+)()", PosixCompileOptions.None)]
    [InlineData("Z(é+)é()", PosixCompileOptions.None)]
    [InlineData("Z(é+)$()", PosixCompileOptions.Newline)]
    [InlineData("Z((é)+)()", PosixCompileOptions.None)]
    [InlineData("Z(é|中)+()", PosixCompileOptions.None)]
    [InlineData("Z(a+)a()", PosixCompileOptions.None)]
    [InlineData("Z([é中😀]+)(.)()", PosixCompileOptions.None)]
    [InlineData("((Z))((é+)é)()", PosixCompileOptions.None)]
    [InlineData("Z(é*)é()", PosixCompileOptions.None)]
    [InlineData("Z(é{0,3})éé()", PosixCompileOptions.None)]
    [InlineData("Z(.{0,4})([é中😀])()", PosixCompileOptions.None)]
    [InlineData("Z(.{0,4})([é中😀])()", PosixCompileOptions.Newline)]
    [InlineData("^Z(é*)é$()", PosixCompileOptions.None)]
    [InlineData("^Z(é*)é$()", PosixCompileOptions.Newline)]
    [InlineData("Z([kK]+)k()", PosixCompileOptions.IgnoreCase)]
    [InlineData("Z(😀+)😀()", PosixCompileOptions.None)]
    [InlineData("Z([é中]*)中()", PosixCompileOptions.None)]
    public void ScalarTerminalSelectionAgreesWithGeneralExecutionAtEveryFloor(string pattern, PosixCompileOptions options)
    {
        var regex = new Utf8PosixRegex(pattern, options);
        // Consuming a cannot precede a beginning anchor, including Newline mode.
        // This impossible branch preserves language/capture numbering and uses
        // the production general selector as a contrast.
        var contrast = pattern + "|a^";
        Assert.False(PosixProgram.Compile(contrast, options).FirstAcceptanceIsLongest);
        var general = new Utf8PosixRegex(contrast, options);
        foreach (var text in new[] { "", "Z", "Zé", "Zééééé!", "Z中中", "Z😀😀!", "😀!Zé中😀Zé",
            "ZKKk!", "ZÉéÉ!", "Zé\né!", "Z😀\n中!", "prefix\nZéé\nZé!" })
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            var storage = Enumerable.Repeat((byte)255, bytes.Length + 12).ToArray();
            bytes.CopyTo(storage, 5);
            var input = storage.AsMemory(5, bytes.Length);
            for (var floor = 0; floor <= bytes.Length; floor++)
            {
                if (floor < bytes.Length && (bytes[floor] & 0xc0) == 0x80) continue;
                for (var width = 0; width <= regex.GroupCount + 2; width++)
                {
                    var expected = new Utf8ValueMatch[width];
                    var actual = new Utf8ValueMatch[width];
                    Assert.Equal(general.Match(input.Span, expected, floor), regex.Match(input.Span, actual, floor));
                    Assert.Equal(expected, actual);
                }
                Assert.Equal(general.Count(input.Span, floor), regex.Count(input.Span, floor));
                Assert.Equal(general.IsMatch(input.Span[floor..]), regex.IsMatch(input.Span[floor..]));
                using var cursor = regex.CreateMatchCursor(input, floor);
                using var expectedCursor = general.CreateMatchCursor(input, floor);
                var alias = cursor;
                var groups = new Utf8ValueMatch[regex.GroupCount + 1];
                var expectedGroups = new Utf8ValueMatch[groups.Length];
                Utf8ValueMatch[]? retained = null;
                Utf8ValueMatch[]? expectedRetained = null;
                while (expectedCursor.MoveNext(expectedGroups, out var expected))
                {
                    Assert.True(alias.MoveNext(groups, out var actual));
                    Assert.Equal(expected, actual);
                    Assert.Equal(expectedGroups, groups);
                    retained ??= groups.ToArray();
                    expectedRetained ??= expectedGroups.ToArray();
                }
                Assert.False(cursor.MoveNext(groups, out _));
                Assert.All(groups, value => Assert.False(value.Success));
                if (retained is not null) Assert.Equal(expectedRetained, retained);
            }
        }
    }

    [Theory]
    [InlineData("é")]
    [InlineData("中")]
    [InlineData("😀")]
    public void UnicodeTerminalRunFitsBoundedDepthAndWorkingAdmission(string scalar)
    {
        var text = "Z" + string.Concat(Enumerable.Repeat(scalar, 8192));
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8PosixRegex("Z(" + scalar + "+)()", resourcePolicy: new()
            { MaxWorkSteps = 100_000, MaxDepth = 1, MaxWorkingBytes = 4096, MaxResultBytes = 1 });
        var groups = new Utf8ValueMatch[3];
        var whole = regex.Match(input, groups);
        Assert.Equal((0, input.Length), (whole.IndexInBytes, whole.LengthInBytes));
        Assert.Equal((1, input.Length - 1, 1, text.Length - 1),
            (groups[1].IndexInBytes, groups[1].LengthInBytes, groups[1].IndexInUtf16, groups[1].LengthInUtf16));
        Assert.Equal((input.Length, 0, text.Length, 0),
            (groups[2].IndexInBytes, groups[2].LengthInBytes, groups[2].IndexInUtf16, groups[2].LengthInUtf16));
        var limited = new Utf8PosixRegex("Z(" + scalar + "+)()", resourcePolicy: new() { MaxWorkSteps = 1 });
        Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.Match(input));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("é")]
    [InlineData("😀")]
    public void RequiredSuffixKeepsEndpointRewindDepthAndFiniteAdmission(string scalar)
    {
        var text = "Z" + string.Concat(Enumerable.Repeat(scalar, 128));
        var input = Encoding.UTF8.GetBytes(text);
        var pattern = "Z(" + scalar + "+)" + scalar + "()";
        var regex = new Utf8PosixRegex(pattern, resourcePolicy: new()
            { MaxWorkSteps = 100_000, MaxDepth = 1024, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 });
        var groups = new Utf8ValueMatch[3];
        var whole = regex.Match(input, groups);
        Assert.Equal((0, input.Length), (whole.IndexInBytes, whole.LengthInBytes));
        var width = Encoding.UTF8.GetByteCount(scalar);
        Assert.Equal((1, input.Length - 1 - width, 1, text.Length - 1 - scalar.Length),
            (groups[1].IndexInBytes, groups[1].LengthInBytes, groups[1].IndexInUtf16, groups[1].LengthInUtf16));
        Assert.Equal((input.Length, 0), (groups[2].IndexInBytes, groups[2].LengthInBytes));
        var shallow = new Utf8PosixRegex(pattern, resourcePolicy: new() { MaxDepth = 1 });
        Assert.Throws<Utf8ResourceLimitExceededException>(() => shallow.Match(input, groups));
    }
}

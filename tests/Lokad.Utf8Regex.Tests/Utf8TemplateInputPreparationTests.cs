using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8TemplateInputPreparationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ReplacementValidatesSubjectAtMostOnceAcrossTemplateAndOutputForms(bool destination, bool utf8Template)
    {
        const string pattern = "^([0-9]{4})$";
        const string text = "1234";
        const string template = "<$1>";
        var input = Encoding.UTF8.GetBytes(text);
        var expected = Encoding.UTF8.GetBytes(new Regex(pattern, RegexOptions.CultureInvariant).Replace(text, template));
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var session = Utf8SearchDiagnosticsSession.Start();
        try
        {
            if (destination)
            {
                var output = new byte[expected.Length];
                var status = utf8Template
                    ? regex.TryReplace(input, "<$1>"u8, output, out var written)
                    : regex.TryReplace(input, template, output, out written);
                Assert.Equal(OperationStatus.Done, status);
                Assert.Equal(expected.Length, written);
                Assert.Equal(expected, output);
            }
            else
            {
                Assert.Equal(expected, utf8Template ? regex.Replace(input, "<$1>"u8) : regex.Replace(input, template));
            }
            Assert.InRange(session.SubjectValidations, 0, 1);
        }
        finally { session.Complete(); }
    }
}

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Lokad.Utf8Regex.Tests.Common;

// Linked into the profile fixtures, including detached package consumers.
internal static class AssemblyDependencyInventory
{
    internal static void AssertNoExternalMatchers(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        foreach (var handle in metadata.AssemblyReferences)
        {
            var name = metadata.GetString(metadata.GetAssemblyReference(handle).Name);
            Assert.True(name.StartsWith("System.", StringComparison.Ordinal) ||
                name is "netstandard" or "mscorlib" or "Lokad.Utf8Regex", $"{path} -> {name}");
        }
        Assert.Empty(FindBclMatcherReferences(metadata));
    }

    internal static string[] FindBclMatcherReferences(MetadataReader metadata)
    {
        var offenders = new List<string>();
        foreach (var handle in metadata.TypeReferences)
        {
            var (ns, name) = ReadType(handle);
            if (IsBclRegexNamespace(ns) && !(ns == "System.Text.RegularExpressions" &&
                name is "Regex" or "RegexOptions" or "RegexMatchTimeoutException"))
                offenders.Add(ns + "." + name);
        }
        foreach (var handle in metadata.MemberReferences)
        {
            var member = metadata.GetMemberReference(handle);
            if (member.Parent.Kind != HandleKind.TypeReference) continue;
            var (ns, type) = ReadType((TypeReferenceHandle)member.Parent);
            var name = metadata.GetString(member.Name);
            if (IsBclRegexNamespace(ns) && !(ns == "System.Text.RegularExpressions" &&
                (type == "Regex" && name == "InfiniteMatchTimeout" && member.GetKind() == MemberReferenceKind.Field ||
                 type == "RegexMatchTimeoutException" && name == ".ctor")))
                offenders.Add(ns + "." + type + "." + name);
        }
        return offenders.ToArray();

        (string Namespace, string Name) ReadType(TypeReferenceHandle handle)
        {
            var type = metadata.GetTypeReference(handle);
            var ns = metadata.GetString(type.Namespace);
            var name = metadata.GetString(type.Name);
            if (type.ResolutionScope.Kind == HandleKind.TypeReference)
            {
                var parent = ReadType((TypeReferenceHandle)type.ResolutionScope);
                return (parent.Namespace, parent.Name + "+" + name);
            }
            return (ns, name);
        }
        static bool IsBclRegexNamespace(string ns) => ns == "System.Text.RegularExpressions" ||
            ns.StartsWith("System.Text.RegularExpressions.", StringComparison.Ordinal);
    }
}

using System.Reflection;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2PublicApiSnapshotTests
{
    [Fact]
    public void PublicApiMatchesReviewedSnapshot()
    {
        var actual = FormatAssembly(typeof(Utf8Pcre2Regex).Assembly);
        var snapshotPath = FindSnapshotPath();
        if (!File.Exists(snapshotPath))
        {
            throw new InvalidOperationException($"Missing public API snapshot '{snapshotPath}'.{Environment.NewLine}{actual}");
        }

        var expected = File.ReadAllText(snapshotPath).ReplaceLineEndings("\n").TrimEnd();
        Assert.Equal(expected, actual);
    }

    private static string FormatAssembly(Assembly assembly)
    {
        var entries = new List<string>();
        foreach (var type in assembly.GetExportedTypes().OrderBy(static type => type.FullName, StringComparer.Ordinal))
        {
            if (type.IsEnum)
            {
                entries.Add($"enum {FormatType(type)} {{ {string.Join(", ", Enum.GetNames(type))} }}");
                continue;
            }

            if (type.BaseType == typeof(MulticastDelegate))
            {
                var invoke = type.GetMethod("Invoke") ?? throw new InvalidOperationException($"Delegate '{type}' has no Invoke method.");
                entries.Add($"delegate {FormatType(invoke.ReturnType)} {FormatType(type)}({FormatParameters(invoke.GetParameters())})");
                continue;
            }

            entries.Add(FormatTypeDeclaration(type));

            entries.AddRange(type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(field => $"  field {FormatType(field.FieldType)} {field.Name}")
                .OrderBy(static value => value, StringComparer.Ordinal));

            entries.AddRange(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(constructor => $"  ctor {type.Name.Split('`')[0]}({FormatParameters(constructor.GetParameters())})")
                .OrderBy(static value => value, StringComparer.Ordinal));

            entries.AddRange(type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(property => $"  property {FormatType(property.PropertyType)} {property.Name} {{ {(property.GetMethod is null ? string.Empty : "get; ")}{(property.SetMethod is null ? string.Empty : "set; ")}}}")
                .OrderBy(static value => value, StringComparer.Ordinal));

            entries.AddRange(type.GetEvents(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(@event => $"  event {FormatType(@event.EventHandlerType ?? typeof(void))} {@event.Name}")
                .OrderBy(static value => value, StringComparer.Ordinal));

            entries.AddRange(type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(static method => !method.IsSpecialName)
                .Select(method =>
                {
                    var genericArguments = method.IsGenericMethodDefinition
                        ? $"<{string.Join(", ", method.GetGenericArguments().Select(static argument => argument.Name))}>"
                        : string.Empty;
                    return $"  method {(method.IsStatic ? "static " : string.Empty)}{FormatType(method.ReturnType)} {method.Name}{genericArguments}({FormatParameters(method.GetParameters())})";
                })
                .OrderBy(static value => value, StringComparer.Ordinal));
        }

        return string.Join('\n', entries);
    }

    private static string FormatTypeDeclaration(Type type)
    {
        var kind = type.IsValueType ? "struct" : type.IsInterface ? "interface" : "class";
        var name = FormatType(type);
        if (!type.IsGenericTypeDefinition)
        {
            return $"{kind} {name}";
        }

        return $"{kind} {name}";
    }

    private static string FormatParameters(ParameterInfo[] parameters)
        => string.Join(", ", parameters.Select(static parameter =>
        {
            var modifier = parameter.IsOut ? "out " : parameter.ParameterType.IsByRef ? "ref " : string.Empty;
            var parameterType = parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType() ?? parameter.ParameterType : parameter.ParameterType;
            return $"{modifier}{FormatType(parameterType)} {parameter.Name}";
        }));

    private static string FormatType(Type type)
    {
        if (type.IsGenericParameter)
        {
            return type.Name;
        }

        if (type.IsArray)
        {
            return $"{FormatType(type.GetElementType() ?? typeof(void))}[]";
        }

        if (!type.IsGenericType)
        {
            return Shorten(type.FullName ?? type.Name);
        }

        var definitionName = type.GetGenericTypeDefinition().FullName ?? type.Name;
        definitionName = definitionName[..definitionName.IndexOf('`')];
        return $"{Shorten(definitionName)}<{string.Join(", ", type.GetGenericArguments().Select(FormatType))}>";
    }

    private static string Shorten(string typeName)
        => typeName
            .Replace("Lokad.Utf8Regex.Pcre2.", string.Empty, StringComparison.Ordinal)
            .Replace("Lokad.Utf8Regex.", string.Empty, StringComparison.Ordinal)
            .Replace("System.", string.Empty, StringComparison.Ordinal);

    private static string FindSnapshotPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "Lokad.Utf8Regex.Pcre2.Tests", "PublicApi.Shipped.txt");
            if (Directory.Exists(Path.GetDirectoryName(candidate)))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the PCRE2 public API snapshot directory.");
    }
}

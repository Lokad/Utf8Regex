using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Lokad.Utf8Regex.Tests.Common;

// Shared by the source and detached package fixtures. This is a readable contract
// snapshot, not a complete CLR compatibility analyzer or a package identity check.
internal static class PublicApiSnapshot
{
    private const BindingFlags PublicDeclared = BindingFlags.Public | BindingFlags.Static |
        BindingFlags.Instance | BindingFlags.DeclaredOnly;

    internal static void AssertMatches(Assembly assembly, string testProject)
    {
        var actual = FormatAssembly(assembly);
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, "tests", testProject, "PublicApi.Shipped.txt");
            if (Directory.Exists(Path.GetDirectoryName(path)))
            {
                Assert.True(File.Exists(path), $"Missing public API snapshot '{path}'.\n{actual}");
                Assert.Equal(File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(), actual);
                return;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException($"Could not locate the {testProject} public API snapshot.");
    }

    internal static string FormatAssembly(Assembly assembly)
    {
        var entries = new List<string>();
        foreach (var type in assembly.GetExportedTypes().OrderBy(static type => type.FullName, StringComparer.Ordinal))
        {
            if (type.IsEnum)
            {
                var values = type.GetFields(PublicDeclared).Where(static field => field.IsLiteral)
                    .OrderBy(static field => field.Name, StringComparer.Ordinal)
                    .Select(field => $"{field.Name} = {FormatConstant(field.GetRawConstantValue())}");
                entries.Add($"{(type.IsDefined(typeof(FlagsAttribute), false) ? "[Flags] " : "")}enum {FormatType(type)} : {FormatType(Enum.GetUnderlyingType(type))} {{ {string.Join(", ", values)} }}");
                continue;
            }
            if (type.BaseType == typeof(MulticastDelegate))
            {
                var invoke = type.GetMethod("Invoke")!;
                entries.Add($"delegate {FormatReturn(invoke)} {FormatType(type)}({FormatParameters(invoke.GetParameters())})");
                continue;
            }
            entries.Add(FormatDeclaration(type));
            entries.AddRange(type.GetFields(PublicDeclared).Select(field =>
                $"  field {(field.IsLiteral ? "const " : (field.IsStatic ? "static " : "") + (field.IsInitOnly ? "readonly " : ""))}{FormatType(field.FieldType)} {field.Name}" +
                (field.IsLiteral ? $" = {FormatConstant(field.GetRawConstantValue())}" : ""))
                .Order(StringComparer.Ordinal));
            entries.AddRange(type.GetConstructors(PublicDeclared).Select(constructor =>
                $"  ctor {type.Name.Split('`')[0]}({FormatParameters(constructor.GetParameters())})")
                .Order(StringComparer.Ordinal));
            entries.AddRange(type.GetProperties(PublicDeclared).Select(property =>
            {
                var getter = property.GetMethod;
                var setter = property.SetMethod;
                var init = setter?.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)) == true;
                var index = property.GetIndexParameters();
                var name = property.Name + (index.Length == 0 ? "" : $"[{FormatParameters(index)}]");
                return $"  property {((getter ?? setter)!.IsStatic ? "static " : "")}{FormatType(property.PropertyType)} {name} {{ " +
                    (getter?.IsPublic == true ? "get; " : "") +
                    (setter?.IsPublic == true ? init ? "init; " : "set; " : "") + "}";
            }).Order(StringComparer.Ordinal));
            entries.AddRange(type.GetEvents(PublicDeclared).Select(item =>
                $"  event {FormatType(item.EventHandlerType!)} {item.Name}").Order(StringComparer.Ordinal));
            entries.AddRange(type.GetMethods(PublicDeclared)
                .Where(static method => !method.IsSpecialName || method.Name.StartsWith("op_", StringComparison.Ordinal))
                .Select(method =>
                {
                    var generic = method.IsGenericMethodDefinition
                        ? $"<{string.Join(", ", method.GetGenericArguments().Select(static argument => argument.Name))}>" : "";
                    var modifiers = (method.IsStatic ? "static " : "") + (method.IsAbstract ? "abstract " :
                        method.IsVirtual && !method.IsFinal ? "virtual " : "");
                    return $"  method {modifiers}{FormatReturn(method)} {method.Name}{generic}({FormatParameters(method.GetParameters())})";
                }).Order(StringComparer.Ordinal));
        }
        return string.Join('\n', entries);
    }

    private static string FormatDeclaration(Type type)
    {
        var modifiers = type.IsValueType
            ? (type.IsDefined(typeof(IsReadOnlyAttribute), false) ? "readonly " : "") + (type.IsByRefLike ? "ref " : "")
            : type.IsInterface ? "" : type.IsAbstract && type.IsSealed ? "static " :
                (type.IsAbstract ? "abstract " : "") + (type.IsSealed ? "sealed " : "");
        var bases = new List<string>();
        if (!type.IsValueType && type.BaseType is { } parent && parent != typeof(object)) bases.Add(FormatType(parent));
        bases.AddRange(type.GetInterfaces().Select(FormatType).Order(StringComparer.Ordinal));
        return $"{modifiers}{(type.IsValueType ? "struct" : type.IsInterface ? "interface" : "class")} {FormatType(type)}" +
            (bases.Count == 0 ? "" : " : " + string.Join(", ", bases));
    }

    private static string FormatParameters(ParameterInfo[] parameters) => string.Join(", ", parameters.Select(parameter =>
    {
        var type = parameter.ParameterType;
        var modifier = parameter.IsOut ? "out " : type.IsByRef ? parameter.IsIn ? "in " : "ref " :
            parameter.IsDefined(typeof(ParamArrayAttribute), false) ? "params " : "";
        var value = parameter.HasDefaultValue ? parameter.RawDefaultValue : Missing.Value;
        var optional = !parameter.IsOptional ? "" : " = " +
            (value is null && type.IsValueType && Nullable.GetUnderlyingType(type) is null ? "default" : FormatConstant(value));
        return $"{modifier}{FormatType(type.IsByRef ? type.GetElementType()! : type)} {parameter.Name}{optional}";
    }));

    private static string FormatReturn(MethodInfo method)
    {
        var type = method.ReturnType;
        if (!type.IsByRef) return FormatType(type);
        var readOnly = method.ReturnParameter.GetRequiredCustomModifiers()
            .Any(static modifier => modifier == typeof(System.Runtime.InteropServices.InAttribute));
        return $"ref {(readOnly ? "readonly " : "")}{FormatType(type.GetElementType()!)}";
    }

    private static string FormatConstant(object? value) => value switch
    {
        null => "null",
        Missing => "Missing.Value",
        string or char => JsonSerializer.Serialize(value),
        bool flag => flag ? "true" : "false",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)!,
    };

    private static string FormatType(Type type)
    {
        if (type.IsGenericParameter) return type.Name;
        if (type.IsArray) return FormatType(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        if (type.IsByRef) return "ref " + FormatType(type.GetElementType()!);
        var name = type.IsGenericType ? type.GetGenericTypeDefinition().FullName! : type.FullName ?? type.Name;
        if (type.IsGenericType) name = name[..name.IndexOf('`')];
        name = name.Replace("Lokad.Utf8Regex.Pcre2.", "", StringComparison.Ordinal)
            .Replace("Lokad.Utf8Regex.", "", StringComparison.Ordinal).Replace("System.", "", StringComparison.Ordinal);
        return name + (type.IsGenericType ? $"<{string.Join(", ", type.GetGenericArguments().Select(FormatType))}>" : "");
    }
}

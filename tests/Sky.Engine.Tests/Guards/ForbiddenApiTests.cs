using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Sky.Engine.Tests.Guards;

/// <summary>
/// Keeps threads, clocks, IO, the network, unseeded randomness and process-seeded hashes out of the engine,
/// read from its compiled metadata.
/// </summary>
public sealed class ForbiddenApiTests
{
    private const string EnvironmentType = "System.Environment";
    private const string AllowedEnvironmentMember = "get_NewLine";

    private static readonly string[] ForbiddenNamespaces = ["System.Threading", "System.IO", "System.Net", "System.Security.Cryptography"];

    private static readonly HashSet<string> AllowedTypes = new(StringComparer.Ordinal)
    {
        "System.IO.TextWriter",
        "System.IO.TextReader",
        "System.IO.StringWriter",
        "System.IO.StringReader",
    };

    private static readonly HashSet<string> ForbiddenTypes = new(StringComparer.Ordinal)
    {
        "System.Random",
        "System.Diagnostics.Stopwatch",
        "System.Diagnostics.Process",
        "System.Console",
        "System.Linq.ParallelEnumerable",
        "System.TimeProvider",
        "System.HashCode",
    };

    private static readonly HashSet<string> ForbiddenMembers = new(StringComparer.Ordinal)
    {
        "System.DateTime::get_Now",
        "System.DateTime::get_UtcNow",
        "System.DateTime::get_Today",
        "System.DateTimeOffset::get_Now",
        "System.DateTimeOffset::get_UtcNow",
        "System.Guid::NewGuid",
        "System.Guid::CreateVersion7",
        "System.String::GetHashCode",
        "System.StringComparer::GetHashCode",
    };

    /// <summary>The engine assembly references none of the forbidden types or members.</summary>
    [Fact]
    public void EngineReferencesNoForbiddenApi() => Assert.Empty(ForbiddenReferences(typeof(AssemblyMarker).Assembly.Location));

    /// <summary>
    /// The scanner reaches a forbidden type, a forbidden member and a member of a generic instantiation
    /// in this test assembly, which uses all three.
    /// </summary>
    [Fact]
    public void ScannerFindsForbiddenApiInAnAssemblyThatUsesOne()
    {
        _ = RollWithRandom();
        _ = ReadWallClock();
        _ = TouchGenericTaskFactory();

        List<string> offending = ForbiddenReferences(typeof(ForbiddenApiTests).Assembly.Location);

        Assert.Contains("System.Random", offending);
        Assert.Contains("System.IO.File", offending);
        Assert.Contains("System.DateTime::get_Now", offending);
        Assert.Contains("System.Threading.Tasks.Task`1::get_Factory", offending);
    }

    private static int RollWithRandom() => new Random(0).Next();

    private static DateTime ReadWallClock() => DateTime.Now;

    private static TaskFactory<int> TouchGenericTaskFactory() => Task<int>.Factory;

    private static List<string> ForbiddenReferences(string assemblyPath)
    {
        using FileStream stream = File.OpenRead(assemblyPath);
        using PEReader peReader = new(stream);
        MetadataReader reader = peReader.GetMetadataReader();
        SortedSet<string> offending = new(StringComparer.Ordinal);

        foreach (TypeReferenceHandle handle in reader.TypeReferences)
        {
            string type = TypeName(reader, handle);
            if (IsForbiddenType(type))
            {
                offending.Add(type);
            }
        }

        foreach (MemberReferenceHandle handle in reader.MemberReferences)
        {
            MemberReference member = reader.GetMemberReference(handle);
            string? type = ParentName(reader, member.Parent);
            string name = reader.GetString(member.Name);
            if (type is not null && IsForbiddenMember(type, name))
            {
                offending.Add($"{type}::{name}");
            }
        }

        return [.. offending];
    }

    private static string? ParentName(MetadataReader reader, EntityHandle parent) =>
        parent.Kind switch
        {
            HandleKind.TypeReference => TypeName(reader, (TypeReferenceHandle)parent),
            HandleKind.TypeSpecification => reader
                .GetTypeSpecification((TypeSpecificationHandle)parent)
                .DecodeSignature(SignatureTypeNames.Instance, null),
            _ => null,
        };

    private static string TypeName(MetadataReader reader, TypeReferenceHandle handle)
    {
        TypeReference type = reader.GetTypeReference(handle);
        string name = reader.GetString(type.Name);
        if (type.ResolutionScope.Kind == HandleKind.TypeReference)
        {
            return $"{TypeName(reader, (TypeReferenceHandle)type.ResolutionScope)}+{name}";
        }

        return Qualify(reader.GetString(type.Namespace), name);
    }

    private static string DefinitionName(MetadataReader reader, TypeDefinitionHandle handle)
    {
        TypeDefinition type = reader.GetTypeDefinition(handle);
        string name = reader.GetString(type.Name);
        TypeDefinitionHandle declaring = type.GetDeclaringType();
        if (!declaring.IsNil)
        {
            return $"{DefinitionName(reader, declaring)}+{name}";
        }

        return Qualify(reader.GetString(type.Namespace), name);
    }

    private static string Qualify(string ns, string name) => ns.Length == 0 ? name : $"{ns}.{name}";

    private static bool IsForbiddenType(string type)
    {
        if (ForbiddenTypes.Contains(type))
        {
            return true;
        }

        return !AllowedTypes.Contains(type) && ForbiddenNamespaces.Any(ns => type.StartsWith($"{ns}.", StringComparison.Ordinal));
    }

    private static bool IsForbiddenMember(string type, string member) =>
        IsForbiddenType(type) || ForbiddenMembers.Contains($"{type}::{member}") || (type == EnvironmentType && member != AllowedEnvironmentMember);

    /// <summary>Names a decoded signature type; a generic instantiation is named by its generic type definition.</summary>
    private sealed class SignatureTypeNames : ISignatureTypeProvider<string, object?>
    {
        public static readonly SignatureTypeNames Instance = new();

        public string GetArrayType(string elementType, ArrayShape shape) => $"{elementType}[{new string(',', shape.Rank - 1)}]";

        public string GetByReferenceType(string elementType) => $"{elementType}&";

        public string GetFunctionPointerType(MethodSignature<string> signature) => "method*";

        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) => genericType;

        public string GetGenericMethodParameter(object? genericContext, int index) => $"!!{index}";

        public string GetGenericTypeParameter(object? genericContext, int index) => $"!{index}";

        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;

        public string GetPinnedType(string elementType) => elementType;

        public string GetPointerType(string elementType) => $"{elementType}*";

        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => $"System.{typeCode}";

        public string GetSZArrayType(string elementType) => $"{elementType}[]";

        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => DefinitionName(reader, handle);

        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => TypeName(reader, handle);

        public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) =>
            reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
    }
}

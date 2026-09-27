using System.Text.Json.Serialization;
using Sky.Engine.Cabin;
using Sky.Engine.Flight;
using Sky.Engine.Needs;
using Sky.Engine.Passengers;

namespace Sky.Content.Schema;

/// <summary>
/// The source-generated serializer context for every content file (R9): snake_case property names, enums as exactly their C#
/// member names (<see cref="StrictEnumConverter{T}"/>, one per enum the schema uses), and a strict read in which an unknown
/// field, a duplicate field, a missing required field or a null where the schema has no null each fail. An optional field
/// with a default other than its type's default is <c>[JsonInclude]</c> with an <c>internal set</c>, not <c>init</c>: the
/// generator assigns every <c>init</c> member, so an omitted one would lose its declared default.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    Converters = [
        typeof(StrictEnumConverter<Need>),
        typeof(StrictEnumConverter<CascadeCondition>),
        typeof(StrictEnumConverter<SourceClass>),
        typeof(StrictEnumConverter<FixtureKind>),
        typeof(StrictEnumConverter<FlightStage>),
        typeof(StrictEnumConverter<ActivitySite>),
        typeof(StrictEnumConverter<EffectTiming>),
        typeof(StrictEnumConverter<CabinLighting>),
        typeof(StrictEnumConverter<RoundKind>),
        typeof(StrictEnumConverter<ServiceDirection>),
        typeof(StrictEnumConverter<Valence>),
        typeof(StrictEnumConverter<TripPurpose>),
    ],
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectRequiredConstructorParameters = true,
    RespectNullableAnnotations = true,
    AllowDuplicateProperties = false
)]
[JsonSerializable(typeof(CabinLayout))]
[JsonSerializable(typeof(NeedsFile))]
[JsonSerializable(typeof(TraitsFile))]
[JsonSerializable(typeof(ActivitiesFile))]
[JsonSerializable(typeof(CrewFile))]
[JsonSerializable(typeof(ScenarioFile))]
[JsonSerializable(typeof(ThoughtsFile))]
[JsonSerializable(typeof(ManifestFile))]
internal sealed partial class ContentJsonContext : JsonSerializerContext;

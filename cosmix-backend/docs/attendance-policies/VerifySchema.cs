#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Text.Json;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Reflection;
using Cosmix.Api.OpenApi;
using Cosmix.Domain.HR.Policies;
using Cosmix.Application.HR.Policies;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.Attendance.PayrollInputs;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

var passed = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); passed++; }
var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
using var services = new ServiceCollection().BuildServiceProvider();
var transformer = new AttendancePolicySchemaTransformer();
OpenApiSchemaTransformerContext Context(Type type, JsonPropertyInfo? property = null) => new()
{
    DocumentName = "v1", ParameterDescription = null, JsonTypeInfo = options.GetTypeInfo(type),
    JsonPropertyInfo = property, ApplicationServices = services
};
// The actual untransformed .NET exporter demonstrates the defect: custom converter schemas are unconstrained.
var raw = options.GetJsonSchemaAsNode(typeof(AttendancePolicyRules));
Check(raw["properties"]!["paidTime"]!["properties"]!["basis"]!.ToJsonString() == "true", "Reproduce opaque policy enum exporter metadata");
var enumTypes = new[] { typeof(AttendancePaidTimeBasis), typeof(AttendanceBreakTreatment), typeof(AttendanceEarlyExitTreatment),
    typeof(AttendanceNonScheduledDayTreatment), typeof(AttendanceLateResetPeriod), typeof(AttendanceDeductionSalaryBasis),
    typeof(AttendanceSalaryDivisor), typeof(AttendanceDeductionRounding), typeof(AttendancePartialLeaveHalfDayTreatment), typeof(AttendancePolicyTarget),
    typeof(AttendanceHourlyShortfallCalculation), typeof(AttendanceOrdinaryPaidDayTreatment),
    typeof(AttendanceLatePolicyChangeCountingStrategy), typeof(AttendanceLateSalaryEffectiveDateBasis),
    typeof(AttendanceLateDeductionKind), typeof(AttendanceLateWeekStartDay), typeof(AttendanceLateOverlapTreatment) };
foreach (var type in enumTypes)
foreach (var nullable in new[] { false, true })
{
    var declared = nullable ? typeof(Nullable<>).MakeGenericType(type) : type;
    var schema = new OpenApiSchema();
    await transformer.TransformAsync(schema, Context(declared), default);
    Check(schema.Type == JsonSchemaType.String, "Named enum component excludes null from ASP.NET's nullable wrapper: " + declared);
    var expected = Enum.GetValues(type).Cast<object>().Select(value => JsonSerializer.Serialize(value, type, options)).ToArray();
    Check(schema.Enum!.Where(value => value is not null).Select(value => value.ToJsonString()).SequenceEqual(expected), "OpenAPI names equal exact runtime converter outputs: " + declared);
    Check(schema.Enum!.All(value => value is not null), "The referenced enum component cannot duplicate the wrapper's null branch: " + declared);
    // The real HTTP document extracts Nullable<T> as oneOf(null, enum component).
    // Prove semantic exclusivity, rather than merely flattening that wrapper into a union.
    int MatchingBranches(System.Text.Json.Nodes.JsonNode? value)
    {
        var componentTypeMatches = value is null ? (schema.Type & JsonSchemaType.Null) != 0 :
            value is System.Text.Json.Nodes.JsonValue scalar && scalar.TryGetValue<string>(out _) && (schema.Type & JsonSchemaType.String) != 0;
        var componentMatches = componentTypeMatches && schema.Enum!.Any(option => System.Text.Json.Nodes.JsonNode.DeepEquals(option, value));
        return (nullable && value is null ? 1 : 0) + (componentMatches ? 1 : 0);
    }
    Check(MatchingBranches(null) == (nullable ? 1 : 0), "Null matches exactly its optional wrapper branch: " + declared);
    Check(MatchingBranches(schema.Enum![0]) == 1, "Named value matches exactly the component: " + declared);
    Check(MatchingBranches(System.Text.Json.Nodes.JsonValue.Create(1)) == 0 &&
        MatchingBranches(System.Text.Json.Nodes.JsonValue.Create("unsupported")) == 0, "Numbers and unknown choices match no branch: " + declared);
}
foreach (var owner in new[] { typeof(AttendancePolicyRules), typeof(AttendanceArrivalRules), typeof(AttendancePaidTimeRules),
    typeof(AttendanceLateDeductionRules), typeof(AttendanceOrdinaryPayRules), typeof(SaveAttendancePayrollInputsRequest) })
foreach (var property in options.GetTypeInfo(owner).Properties.Where(p => p.PropertyType == typeof(int?) || p.PropertyType == typeof(decimal?) || p.PropertyType == typeof(int)))
{
    var schema = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Number | JsonSchemaType.Null, Pattern = "quoted-number" };
    await transformer.TransformAsync(schema, Context(property.PropertyType, property), default);
    var nullable = Nullable.GetUnderlyingType(property.PropertyType) is not null;
    var integer = property.PropertyType == typeof(int?) || property.PropertyType == typeof(int);
    Check(schema.Type == ((integer ? JsonSchemaType.Integer : JsonSchemaType.Number) | (nullable ? JsonSchemaType.Null : 0)), "Policy numbers never advertise quoted values: " + property.Name);
    Check(schema.Pattern is null, "No string-number pattern remains: " + property.Name);
}
var unrelated = options.GetTypeInfo(typeof(UnrelatedForm)).Properties.Single();
foreach (var owner in new[] { typeof(AttendancePaidTimeRules), typeof(AttendanceLateDeductionRules) })
{
    var containingSchema = new OpenApiSchema { Properties = new Dictionary<string, IOpenApiSchema>() };
    await transformer.TransformAsync(containingSchema, Context(owner), default);
    foreach (var property in options.GetTypeInfo(owner).Properties.Where(p => Nullable.GetUnderlyingType(p.PropertyType)?.IsEnum == true &&
        p.AttributeProvider is MemberInfo m && m.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition == JsonIgnoreCondition.WhenWritingNull))
    {
        var optionalChoice = (OpenApiSchema)containingSchema.Properties[property.Name];
        // ASP.NET subsequently visits the inline child. The enum transform must not erase the
        // legacy null accepted by the property while keeping shared enum components non-nullable.
        await transformer.TransformAsync(optionalChoice, Context(property.PropertyType, property), default);
        var optionType = Nullable.GetUnderlyingType(property.PropertyType)!;
        var expected = Enum.GetValues(optionType).Cast<object>().Select(value => JsonSerializer.Serialize(value, optionType, options)).ToArray();
        Check(optionalChoice.Type == (JsonSchemaType.String | JsonSchemaType.Null), "Optional inline choice includes legacy null: " + property.Name);
        Check(optionalChoice.Enum!.Count == expected.Length + 1 && optionalChoice.Enum.Count(value => value is null) == 1 &&
            optionalChoice.Enum.Where(value => value is not null).Select(value => value.ToJsonString()).SequenceEqual(expected),
            "Optional inline choice matches exact runtime values once plus null: " + property.Name);
        Check(optionalChoice.OneOf is null || optionalChoice.OneOf.Count == 0, "Inline optional choice has no competing nullable wrapper branches: " + property.Name);
        var standaloneChoice = new OpenApiSchema();
        await transformer.TransformAsync(standaloneChoice, Context(property.PropertyType, property), default);
        Check(standaloneChoice.Type == optionalChoice.Type && standaloneChoice.Enum!.Count == optionalChoice.Enum!.Count &&
            standaloneChoice.Enum.Zip(optionalChoice.Enum).All(pair => System.Text.Json.Nodes.JsonNode.DeepEquals(pair.First, pair.Second)),
            "Optional choice remains correct independent of parent/child transformer order: " + property.Name);
    }
}
var unchanged = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Integer | JsonSchemaType.Null, Pattern = "existing" };
await transformer.TransformAsync(unchanged, Context(unrelated.PropertyType, unrelated), default);
Check(unchanged.Type == (JsonSchemaType.String | JsonSchemaType.Integer | JsonSchemaType.Null) && unchanged.Pattern == "existing", "Other API contracts retain their original number behavior");
Console.WriteLine($"PASS: {passed} policy OpenAPI transformer/exporter checks; no HTTP, SQL, accounts or private IO.");

public sealed record UnrelatedForm(int? Number);

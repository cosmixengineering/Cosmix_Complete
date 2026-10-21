using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Cosmix.Domain.HR.Policies;
using Cosmix.Application.HR.Policies;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.Attendance.PayrollInputs;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Cosmix.Api.OpenApi;

/// <summary>Describe the strict policy-form converters without changing unrelated API schemas.</summary>
public sealed class AttendancePolicySchemaTransformer : IOpenApiSchemaTransformer
{
    private static readonly HashSet<Type> EnumTypes = [
        typeof(AttendancePaidTimeBasis), typeof(AttendanceBreakTreatment), typeof(AttendanceEarlyExitTreatment),
        typeof(AttendanceNonScheduledDayTreatment), typeof(AttendanceLateResetPeriod),
        typeof(AttendanceDeductionSalaryBasis), typeof(AttendanceSalaryDivisor), typeof(AttendanceDeductionRounding),
        typeof(AttendancePartialLeaveHalfDayTreatment), typeof(AttendancePolicyTarget),
        typeof(AttendanceHourlyShortfallCalculation), typeof(AttendanceOrdinaryPaidDayTreatment),
        typeof(AttendanceLatePolicyChangeCountingStrategy), typeof(AttendanceLateSalaryEffectiveDateBasis),
        typeof(AttendanceLateDeductionKind), typeof(AttendanceLateWeekStartDay), typeof(AttendanceLateOverlapTreatment)
    ];
    private static readonly HashSet<Type> FormTypes = [
        typeof(AttendancePolicyRules), typeof(AttendanceArrivalRules),
        typeof(AttendancePaidTimeRules), typeof(AttendanceLateDeductionRules), typeof(AttendanceOrdinaryPayRules), typeof(LeavePolicySettings),
        typeof(PreviewAttendancePolicyRequest), typeof(PublishAttendancePolicyRequest),
        typeof(AssignAttendancePolicyRequest), typeof(EndAttendancePolicyAssignmentRequest),
        typeof(ReplaceAttendancePolicyAssignmentRequest), typeof(SaveAttendancePayrollInputsRequest)
    ];

    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var declared = context.JsonTypeInfo.Type;
        var underlying = Nullable.GetUnderlyingType(declared);
        var type = underlying ?? declared;
        var nullable = underlying is not null;
        if (EnumTypes.Contains(type))
        {
            // Required nullable choices use oneOf(null, non-nullable enum component). Optional
            // versioned choices are inline, so their property schema must itself accept null.
            // This child visit can follow the containing-form visit below; preserve that null.
            var inlineNullable = context.JsonPropertyInfo is { } property &&
                Nullable.GetUnderlyingType(property.PropertyType) == type &&
                property.AttributeProvider is MemberInfo { DeclaringType: { } owner } member &&
                FormTypes.Contains(owner) &&
                member.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition == JsonIgnoreCondition.WhenWritingNull;
            schema.Type = JsonSchemaType.String | (inlineNullable ? JsonSchemaType.Null : 0);
            schema.Format = null;
            schema.Pattern = null;
            schema.Enum = Enum.GetValues(type).Cast<object>()
                .Select(value => (JsonNode)JsonValue.Create(JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString()!))!).ToList();
            if (inlineNullable) schema.Enum.Add(null!);
        }
        else if (context.JsonPropertyInfo?.AttributeProvider is MemberInfo { DeclaringType: { } owner } &&
            FormTypes.Contains(owner) && (type == typeof(int) || type == typeof(decimal)))
        {
            // Web defaults advertise quoted numbers even though the policy records explicitly reject them.
            schema.Type = (type == typeof(int) ? JsonSchemaType.Integer : JsonSchemaType.Number) |
                (nullable ? JsonSchemaType.Null : 0);
            schema.Format = type == typeof(int) ? "int32" : null;
            schema.Pattern = null;
        }
        if (FormTypes.Contains(type) && schema.Properties is not null)
        {
            // Optional versioned fields omit null to preserve historical canonical bytes. ASP.NET
            // emits these inline without the wrapper used for required nullable enums.
            foreach (var property in context.JsonTypeInfo.Properties)
            {
                var optionType = Nullable.GetUnderlyingType(property.PropertyType);
                if (optionType is null || !EnumTypes.Contains(optionType) ||
                    property.AttributeProvider is not MemberInfo member ||
                    member.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition != JsonIgnoreCondition.WhenWritingNull) continue;
                schema.Properties[property.Name] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String | JsonSchemaType.Null,
                    Enum = [.. Enum.GetValues(optionType).Cast<object>().Select(value =>
                        (JsonNode)JsonValue.Create(JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString()!))!), null!]
                };
            }
        }
        return Task.CompletedTask;
    }
}

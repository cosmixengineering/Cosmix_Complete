import { explicitNumber, requiredId, requiredText } from './vehicle-management';

type Option = { value: string; label: string };
export type VehiclePolicyField =
  | { kind: 'constant'; value: number }
  | { kind: 'text' | 'date' | 'id'; max?: number }
  | { kind: 'number'; scale: number; max: number }
  | { kind: 'choice' | 'set'; options: Option[]; numeric?: boolean }
  | { kind: 'nullable'; field: VehiclePolicyField }
  | { kind: 'object'; fields: Record<string, VehiclePolicyField> }
  | { kind: 'list'; field: VehiclePolicyField; max: number };
const object = (fields: Record<string, VehiclePolicyField>): VehiclePolicyField => ({
  kind: 'object',
  fields,
});
const nullable = (field: VehiclePolicyField): VehiclePolicyField => ({ kind: 'nullable', field });
const text = (max = 500): VehiclePolicyField => ({ kind: 'text', max });
const list = (field: VehiclePolicyField, max: number): VehiclePolicyField => ({
  kind: 'list',
  field,
  max,
});
const choice = (values: string[]): VehiclePolicyField => ({
  kind: 'choice',
  options: values.map((value) => ({ value, label: value })),
});
const roles = list(text(100), 100);
const authority = { ReviewerRoleCodes: roles, AuthorityReference: text() };
const serviceOptions = [
  { value: '1', label: 'Tuning' },
  { value: '2', label: 'Engine overhaul' },
];
const outcomes = [
  { value: '1', label: 'Returned for evidence' },
  { value: '2', label: 'Rejected' },
  { value: '3', label: 'No liability proposed' },
  { value: '4', label: 'Proposed liability recorded' },
  { value: '5', label: 'Insurance review required' },
];
export const vehicleIncidentOutcomeOptions = outcomes;
const period = { EffectiveFrom: { kind: 'date' } as const, EffectiveTo: { kind: 'date' } as const };
export const vehiclePolicyFields: VehiclePolicyField = object({
  SchemaVersion: { kind: 'constant', value: 3 },
  StandardFuelKmPerLiter: nullable({ kind: 'number', scale: 6, max: 1000000 }),
  ClaimFuelPricePerLiter: nullable({ kind: 'number', scale: 6, max: 10000000 }),
  TuningKm: nullable({ kind: 'number', scale: 3, max: 1000000 }),
  OverhaulKm: nullable({ kind: 'number', scale: 3, max: 1000000 }),
  Workflow: object({
    SchemaVersion: { kind: 'constant', value: 1 },
    StaffAssignments: list(
      object({
        EmployeeId: { kind: 'id' },
        ...period,
        Duties: {
          kind: 'set',
          options: [
            { value: 'ComplaintResolution', label: 'Complaint resolution' },
            { value: 'FieldVisits', label: 'Field visits' },
          ],
        },
        EvidenceReference: text(),
      }),
      1000,
    ),
    Workshops: list(
      object({
        Reference: text(200),
        ...period,
        ServiceKinds: { kind: 'set', options: serviceOptions, numeric: true },
        AuthorityReference: text(),
      }),
      500,
    ),
    Maintenance: list(
      object({
        Kind: { kind: 'choice', options: serviceOptions, numeric: true },
        CycleMode: choice(['EveryThreshold', 'Once']),
        AmountMode: choice(['Fixed', 'Maximum']),
        Amount: { kind: 'number', scale: 2, max: 10000000 },
        Currency: text(3),
        VehicleTypes: list(text(60), 100),
      }),
      2,
    ),
    OtherWorkDistanceBasis: nullable(choice(['ApprovedMapDistance', 'Odometer'])),
    IncidentReview: nullable(object({ RoleCodes: roles, AuthorityReference: text() })),
    FuelCurrency: nullable(text(3)),
  }),
  Adjustments: nullable(
    object({
      SchemaVersion: { kind: 'constant', value: 2 },
      Monthly: nullable(
        object({
          AssignmentSelection: choice(['SingleEligibleTripPerDay']),
          OffsetScope: choice([
            'SameEmployeeVehicleMonthAllApprovedWork',
            'SameEmployeeVehicleMonthPolicyApprovedWork',
          ]),
          ExcessTreatment: choice(['CapAtMonthlyEligible', 'RetainSignedDeficit']),
          MaintenanceEffect: choice(['None', 'ReduceOutstation']),
          EligiblePurposes: {
            kind: 'set',
            numeric: true,
            options: [
              { value: '2', label: 'Complaint visit' },
              { value: '3', label: 'Field visit' },
              { value: '4', label: 'Outstation' },
            ],
          },
          ...authority,
        }),
      ),
      MaintenanceTransitions: list(
        object({
          Kind: { kind: 'choice', options: serviceOptions, numeric: true },
          PreviousPolicyVersionId: { kind: 'id' },
          Mode: choice(['CarryConsumedKilometers']),
          AuthorityReference: text(),
        }),
        100,
      ),
      IncidentAmendments: nullable(
        object({
          AllowedPriorOutcomes: { kind: 'set', numeric: true, options: outcomes },
          ...authority,
        }),
      ),
      TripAttribution: nullable(
        object({
          Mode: choice(['ExplicitDailySegments']),
          CommuteTreatment: choice(['OncePerTripDay']),
          FuelRounding: choice(['PerSegmentTwoDecimalsToEven']),
          ...authority,
        }),
      ),
      MonthlyPeriods: nullable(
        object({ Mode: choice(['SeparatePolicyPartitions']), AuthorityReference: text() }),
      ),
    }),
  ),
  Succession: nullable(
    object({ PreviousPolicyVersionId: { kind: 'id' }, AuthorityReference: text() }),
  ),
});

export function policyDraft(
  field: VehiclePolicyField = vehiclePolicyFields,
  retained?: unknown,
): unknown {
  if (field.kind === 'constant') return field.value;
  if (field.kind === 'nullable')
    return retained === null
      ? null
      : retained === undefined
        ? undefined
        : policyDraft(field.field, retained);
  if (field.kind === 'object') {
    const values =
      retained && typeof retained === 'object' && !Array.isArray(retained)
        ? (retained as Record<string, unknown>)
        : {};
    return Object.fromEntries(
      Object.entries(field.fields).map(([key, child]) => [key, policyDraft(child, values[key])]),
    );
  }
  if (field.kind === 'list')
    return Array.isArray(retained) ? retained.map((row) => policyDraft(field.field, row)) : [];
  if (field.kind === 'set') return Array.isArray(retained) ? retained.map(String) : [];
  return retained === null || retained === undefined ? '' : String(retained);
}

export function serializePolicyDraft(
  value: unknown,
  field: VehiclePolicyField = vehiclePolicyFields,
  path = 'Rules',
): unknown {
  if (field.kind === 'constant') return field.value;
  if (field.kind === 'nullable') {
    if (value === undefined)
      throw new Error(path + ': explicitly choose configured or unconfigured.');
    return value === null ? null : serializePolicyDraft(value, field.field, path);
  }
  if (field.kind === 'object') {
    if (!value || typeof value !== 'object' || Array.isArray(value))
      throw new Error(path + ': configuration required.');
    const values = value as Record<string, unknown>;
    return Object.fromEntries(
      Object.entries(field.fields).map(([key, child]) => [
        key,
        serializePolicyDraft(values[key], child, path + '.' + key),
      ]),
    );
  }
  if (field.kind === 'list') {
    if (!Array.isArray(value) || value.length > field.max)
      throw new Error(path + ': invalid number of entries.');
    return value.map((row, index) =>
      serializePolicyDraft(row, field.field, path + '[' + index + ']'),
    );
  }
  if (field.kind === 'set') {
    if (
      !Array.isArray(value) ||
      !value.length ||
      value.some((item) => !field.options.some((option) => option.value === item))
    )
      throw new Error(path + ': choose the configured values.');
    return field.numeric ? value.map(Number) : value;
  }
  if (typeof value !== 'string') throw new Error(path + ': explicit value required.');
  if (field.kind === 'number') {
    const number = explicitNumber(value, path, field.scale, 0, field.max);
    if (number <= 0) throw new Error(path + ': must be positive.');
    return number;
  }
  if (field.kind === 'choice') {
    if (!field.options.some((option) => option.value === value))
      throw new Error(path + ': choose a value.');
    return field.numeric ? Number(value) : value;
  }
  if (field.kind === 'id') return requiredId(value, path);
  if (field.kind === 'date') {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(value) || !Number.isFinite(Date.parse(value)))
      throw new Error(path + ': date required.');
    return value;
  }
  return requiredText(value, path, field.kind === 'text' ? field.max : undefined);
}

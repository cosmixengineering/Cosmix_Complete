import { rulesSchema, type FoundationPolicyKind, type FoundationPolicyRules } from './contracts';

export type Choice = '' | 'true' | 'false';
export type ValueRow = { id: string; value: string };
export type DocumentRow = {
  id: string;
  staffCategoryCode: string;
  global: boolean;
  types: string[];
};
export type PolicyDraft = {
  genderValues: ValueRow[];
  maritalStatusValues: ValueRow[];
  simStatusValues: ValueRow[];
  simCreditTierValues: ValueRow[];
  requireGender: Choice;
  requireMaritalStatus: Choice;
  requireFatherOrSpouseName: Choice;
  requireProbationEnd: Choice;
  minimumAgeAtJoining: string;
  maximumAgeAtJoining: string;
  maximumProbationDays: string;
  requireExpiryForIdentityTypes: string[];
  requiredDocuments: DocumentRow[];
  allowRetirement: Choice;
  allowSuspensionRelease: Choice;
  approvedReviewMode: '' | 'FixedAdminReview';
};
export function blankPolicyDraft(): PolicyDraft {
  return {
    genderValues: [],
    maritalStatusValues: [],
    simStatusValues: [],
    simCreditTierValues: [],
    requireGender: '',
    requireMaritalStatus: '',
    requireFatherOrSpouseName: '',
    requireProbationEnd: '',
    minimumAgeAtJoining: '',
    maximumAgeAtJoining: '',
    maximumProbationDays: '',
    requireExpiryForIdentityTypes: [],
    requiredDocuments: [],
    allowRetirement: '',
    allowSuspensionRelease: '',
    approvedReviewMode: '',
  };
}
export function draftFromRules(rules: FoundationPolicyRules): PolicyDraft {
  const draft = blankPolicyDraft();
  const values = (list: string[]) => list.map((value) => ({ id: crypto.randomUUID(), value }));
  if (rules.fields) {
    const fields = rules.fields;
    draft.genderValues = values(fields.genderValues);
    draft.maritalStatusValues = values(fields.maritalStatusValues);
    draft.simStatusValues = values(fields.simStatusValues);
    draft.simCreditTierValues = values(fields.simCreditTierValues);
    draft.requireGender = fields.requireGender ? 'true' : 'false';
    draft.requireMaritalStatus = fields.requireMaritalStatus ? 'true' : 'false';
    draft.requireFatherOrSpouseName = fields.requireFatherOrSpouseName ? 'true' : 'false';
    draft.requireProbationEnd = fields.requireProbationEnd ? 'true' : 'false';
    draft.minimumAgeAtJoining = fields.minimumAgeAtJoining?.toString() ?? '';
    draft.maximumAgeAtJoining = fields.maximumAgeAtJoining?.toString() ?? '';
    draft.maximumProbationDays = fields.maximumProbationDays?.toString() ?? '';
    draft.requireExpiryForIdentityTypes = [...fields.requireExpiryForIdentityTypes];
    draft.requiredDocuments = fields.requiredDocuments.map((row) => ({
      id: crypto.randomUUID(),
      global: row.staffCategoryCode === null,
      staffCategoryCode: row.staffCategoryCode ?? '',
      types: [...row.types],
    }));
  }
  if (rules.lifecycle) {
    draft.allowRetirement = rules.lifecycle.allowRetirement ? 'true' : 'false';
    draft.allowSuspensionRelease = rules.lifecycle.allowSuspensionRelease ? 'true' : 'false';
  }
  if (rules.records) draft.approvedReviewMode = rules.records.approvedReviewMode;
  return draft;
}
function explicitBoolean(choice: Choice) {
  if (choice === '') throw new Error('Select an explicit value for each required policy flag.');
  return choice === 'true';
}
function optionalInteger(value: string) {
  if (value === '') return null;
  if (!/^\d+$/.test(value)) throw new Error('Optional limits must be whole nonnegative numbers.');
  return Number(value);
}
export function rulesFromDraft(
  kind: FoundationPolicyKind,
  draft: PolicyDraft,
): FoundationPolicyRules {
  const input =
    kind === 'Fields'
      ? {
          fields: {
            genderValues: draft.genderValues.map((row) => row.value.trim()),
            maritalStatusValues: draft.maritalStatusValues.map((row) => row.value.trim()),
            simStatusValues: draft.simStatusValues.map((row) => row.value.trim()),
            simCreditTierValues: draft.simCreditTierValues.map((row) => row.value.trim()),
            requireGender: explicitBoolean(draft.requireGender),
            requireMaritalStatus: explicitBoolean(draft.requireMaritalStatus),
            requireFatherOrSpouseName: explicitBoolean(draft.requireFatherOrSpouseName),
            requireProbationEnd: explicitBoolean(draft.requireProbationEnd),
            minimumAgeAtJoining: optionalInteger(draft.minimumAgeAtJoining),
            maximumAgeAtJoining: optionalInteger(draft.maximumAgeAtJoining),
            maximumProbationDays: optionalInteger(draft.maximumProbationDays),
            requireExpiryForIdentityTypes: draft.requireExpiryForIdentityTypes,
            requiredDocuments: draft.requiredDocuments.map((row) => ({
              staffCategoryCode: row.global ? null : row.staffCategoryCode.trim(),
              types: row.types,
            })),
          },
        }
      : kind === 'Lifecycle'
        ? {
            lifecycle: {
              allowRetirement: explicitBoolean(draft.allowRetirement),
              allowSuspensionRelease: explicitBoolean(draft.allowSuspensionRelease),
            },
          }
        : { records: { approvedReviewMode: draft.approvedReviewMode } };
  return rulesSchema(kind).parse(input);
}

import { policyUses, rulesSchema, type Mapping, type Rules } from './contracts';

export type MappingDraft = {
  key: string;
  code: string;
  name: string;
  workLocation: number;
  scheduleBasis: number;
  requireSiteBeforeActivation: boolean | null;
  outsideOvertimeInheritance: boolean | null;
  policyReferences: { use: 1 | 2 | 3 | 4 | 5; applicability: number; policyVersionId: string }[];
};
export function mappingDraft(mapping?: Mapping): MappingDraft {
  return {
    key: crypto.randomUUID(),
    code: mapping?.code ?? '',
    name: mapping?.name ?? '',
    workLocation: mapping?.workLocation ?? 0,
    scheduleBasis: mapping?.scheduleBasis ?? 0,
    requireSiteBeforeActivation: mapping?.requireSiteBeforeActivation ?? null,
    outsideOvertimeInheritance: mapping?.outsideOvertimeInheritance ?? null,
    policyReferences: policyUses.map(({ value }) => {
      const r = mapping?.policyReferences.find((r) => r.use === value);
      return {
        use: value,
        applicability: r?.applicability ?? 0,
        policyVersionId: r?.policyVersionId ?? '',
      };
    }),
  };
}
export function initialMappings(source?: Rules): MappingDraft[] {
  if (source) return source.categories.map(mappingDraft);
  return ['OFFICE', 'SITE'].map((code) =>
    mappingDraft({
      code,
      name: code === 'OFFICE' ? 'Office' : 'Site',
      workLocation: code === 'OFFICE' ? 1 : 2,
      scheduleBasis: code === 'OFFICE' ? 1 : 2,
      requireSiteBeforeActivation: false,
      outsideOvertimeInheritance: code === 'SITE',
      policyReferences: policyUses.map(({ value }) => ({
        use: value,
        applicability: 0,
        policyVersionId: null,
      })),
    }),
  );
}
export function draftRules(drafts: MappingDraft[]) {
  return rulesSchema.parse({
    schemaVersion: 1,
    categories: drafts.map((d) => ({
      code: d.code.trim().toUpperCase(),
      name: d.name.trim(),
      workLocation: d.workLocation,
      scheduleBasis: d.scheduleBasis,
      requireSiteBeforeActivation: d.requireSiteBeforeActivation,
      outsideOvertimeInheritance: d.outsideOvertimeInheritance,
      policyReferences: d.policyReferences.map((r) => ({
        ...r,
        policyVersionId: r.applicability === 2 ? r.policyVersionId.trim() : null,
      })),
    })),
  });
}

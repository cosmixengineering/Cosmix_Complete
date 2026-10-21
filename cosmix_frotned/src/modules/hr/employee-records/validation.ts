import {
  commandSchema,
  sameId,
  type RecordAction,
  type RecordCommand,
  type RecordKind,
  type OperationalRecord,
  type CertificationRecord,
  type EvidenceDocument,
} from './contracts';

export const actionLabels: Record<RecordAction, string> = {
  1: 'Issue',
  2: 'Return',
  3: 'Transfer',
  4: 'Renew',
  5: 'Suspend',
  6: 'Revoke',
  7: 'Replace evidence',
};
export const requestStates = ['Pending', 'Approved', 'Returned', 'Rejected', 'Cancelled'];
export function supportedActions(kind: RecordKind, record?: OperationalRecord): RecordAction[] {
  if (!record) return [1];
  if (kind === 'Asset') return 'returnedAt' in record && record.returnedAt === null ? [2, 3] : [];
  if (kind === 'Sim')
    return 'simReference' in record
      ? record.status === 1
        ? [2, 3]
        : record.status === 2
          ? [2]
          : []
      : [];
  if (!('isExpired' in record) || record.status === 3) return [];
  return record.status === 2 ? [6] : [...(record.expiresOn ? [4 as const] : []), 7, 5, 6];
}
export function availableEvidence(document: EvidenceDocument, metadata: boolean) {
  return (
    document.privacy === 0 &&
    document.lifecycleStatus === 0 &&
    document.isAvailable &&
    !document.isScanInProgress &&
    document.scanStatus === 1 &&
    document.verificationStatus === 1 &&
    (metadata ? [5, 6] : [5, 6, 10]).includes(document.type)
  );
}
export function validateRecordCommand(
  kind: RecordKind,
  employeeId: string,
  input: unknown,
  record?: OperationalRecord,
): RecordCommand {
  const command = commandSchema(kind, employeeId).parse(input);
  if (
    !supportedActions(kind, record).includes(command.action) ||
    (record &&
      (!command.expectedCurrentRecordId ||
        !sameId(record.id, command.expectedCurrentRecordId) ||
        !sameId(record.employeeId, employeeId)))
  )
    throw new Error('This action is unavailable for the selected current record.');
  if (Date.parse(command.effectiveAt) > Date.now())
    throw new Error('Effective time cannot be in the future.');
  if (kind === 'Certification' && record && [4, 7].includes(command.action)) {
    const prior = record as CertificationRecord;
    const next = command.certification!;
    if (
      next.type !== prior.type ||
      next.issuer !== prior.issuer ||
      (prior.employeeDocumentId !== null &&
        sameId(next.evidenceDocumentId, prior.employeeDocumentId))
    )
      throw new Error('Use the same certificate type and issuer with new evidence.');
    if (
      command.action === 4 &&
      (!prior.expiresOn ||
        !next.expiresOn ||
        next.expiresOn <= prior.expiresOn ||
        next.issuedOn! < prior.issuedOn)
    )
      throw new Error(
        'Renewal requires later validity and an issue date no earlier than the original.',
      );
    if (
      command.action === 7 &&
      (next.number !== prior.number ||
        next.issuedOn !== prior.issuedOn ||
        (next.expiresOn ?? null) !== prior.expiresOn)
    )
      throw new Error('Replacement must preserve the number and all validity dates.');
  }
  return command;
}
export type RecordDraft = {
  effectiveAt: string;
  reason: string;
  evidenceDocumentId: string;
  reference: string;
  condition: string;
  receiverAccountId: string;
  targetEmployeeId: string;
  targetEvidenceDocumentId: string;
  targetCondition: string;
  phoneNumber: string;
  creditTierCode: string;
  type: string;
  number: string;
  issuer: string;
  issuedOn: string;
  expiresOn: string;
};
export function initialRecordDraft(record?: OperationalRecord): RecordDraft {
  return {
    effectiveAt: new Date().toISOString(),
    reason: '',
    evidenceDocumentId: '',
    reference: '',
    condition: '',
    receiverAccountId: '',
    targetEmployeeId: '',
    targetEvidenceDocumentId: '',
    targetCondition: '',
    phoneNumber: '',
    creditTierCode: '',
    type: record && 'isExpired' in record ? record.type : '',
    number: record && 'isExpired' in record ? record.number : '',
    issuer: record && 'isExpired' in record ? record.issuer : '',
    issuedOn: record && 'isExpired' in record ? record.issuedOn : '',
    expiresOn: record && 'isExpired' in record ? (record.expiresOn ?? '') : '',
  };
}
export function buildRecordCommand(
  kind: RecordKind,
  action: RecordAction,
  employeeId: string,
  employeeVersion: string,
  draft: RecordDraft,
  record?: OperationalRecord,
  targetVersion?: string,
) {
  const issue = action === 1;
  const transfer = action === 3;
  const input: Record<string, unknown> = {
    expectedEmployeeVersion: employeeVersion,
    action,
    expectedCurrentRecordId: issue ? null : record?.id,
    effectiveAt: draft.effectiveAt,
    reason: draft.reason,
  };
  const common = {
    evidenceDocumentId: draft.evidenceDocumentId,
    ...(!issue ? { receiverAccountId: draft.receiverAccountId } : {}),
    ...(transfer
      ? {
          targetEmployeeId: draft.targetEmployeeId,
          expectedTargetVersion: targetVersion,
          targetEvidenceDocumentId: draft.targetEvidenceDocumentId,
        }
      : {}),
  };
  if (kind === 'Asset')
    input.asset = {
      ...common,
      condition: draft.condition,
      ...(issue ? { assetReference: draft.reference } : {}),
      ...(transfer ? { targetCondition: draft.targetCondition } : {}),
    };
  else if (kind === 'Sim')
    input.sim = {
      ...common,
      ...(issue ? { simReference: draft.reference, phoneNumber: draft.phoneNumber } : {}),
      ...(issue || transfer ? { creditTierCode: draft.creditTierCode } : {}),
    };
  else
    input.certification = {
      evidenceDocumentId: draft.evidenceDocumentId,
      ...([1, 4, 7].includes(action)
        ? {
            type: draft.type,
            number: draft.number,
            issuer: draft.issuer,
            issuedOn: draft.issuedOn,
            expiresOn: draft.expiresOn || null,
          }
        : {}),
    };
  return validateRecordCommand(kind, employeeId, input, record);
}

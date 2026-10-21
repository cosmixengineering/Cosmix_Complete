import { z } from 'zod';
import { currentUpdateSchema } from '../schemas/updates';
import { documentSchema } from '../schemas/hiring';

export const recordId = z
  .uuid()
  .refine((value) => value !== '00000000-0000-0000-0000-000000000000');
export const sameId = (left: string, right: string) => left.toLowerCase() === right.toLowerCase();
const day = z.iso.date();
const instant = z.iso.datetime({ offset: true });
export const rowVersion = z.string().regex(/^[A-Za-z0-9+/]{10}[AEIMQUYcgkosw048]=$/);
const printable = (max: number) =>
  z
    .string()
    .trim()
    .min(1)
    .max(max)
    .refine(
      (value) =>
        ![...value].some(
          (c) => c.charCodeAt(0) < 32 || (c.charCodeAt(0) >= 127 && c.charCodeAt(0) <= 159),
        ),
      'Use printable text without line breaks or control characters.',
    );
export const reasonSchema = printable(1000);
export type RecordKind = 'Asset' | 'Sim' | 'Certification';
export type RecordAction = 1 | 2 | 3 | 4 | 5 | 6 | 7;
const optionalId = recordId.nullish();
const assetInput = z.strictObject({
  assetReference: printable(100).nullish(),
  condition: printable(1000),
  evidenceDocumentId: recordId,
  receiverAccountId: optionalId,
  targetEmployeeId: optionalId,
  expectedTargetVersion: rowVersion.nullish(),
  targetEvidenceDocumentId: optionalId,
  targetCondition: printable(1000).nullish(),
});
const simInput = z.strictObject({
  evidenceDocumentId: recordId,
  simReference: printable(100).nullish(),
  phoneNumber: z
    .string()
    .regex(/^\+[1-9][0-9]{1,14}$/)
    .nullish(),
  creditTierCode: printable(40).nullish(),
  receiverAccountId: optionalId,
  targetEmployeeId: optionalId,
  expectedTargetVersion: rowVersion.nullish(),
  targetEvidenceDocumentId: optionalId,
});
const certificationInput = z.strictObject({
  evidenceDocumentId: recordId,
  type: printable(100).nullish(),
  number: printable(100).nullish(),
  issuer: printable(200).nullish(),
  issuedOn: day.nullish(),
  expiresOn: day.nullish(),
});
export const commandBase = z.strictObject({
  expectedEmployeeVersion: rowVersion,
  action: z.union([
    z.literal(1),
    z.literal(2),
    z.literal(3),
    z.literal(4),
    z.literal(5),
    z.literal(6),
    z.literal(7),
  ]),
  expectedCurrentRecordId: recordId.nullable(),
  effectiveAt: instant.refine(
    (value) => value.endsWith('Z') || value.endsWith('+00:00'),
    'Effective time must be UTC.',
  ),
  reason: reasonSchema,
  asset: assetInput.nullish(),
  sim: simInput.nullish(),
  certification: certificationInput.nullish(),
});
export function commandSchema(kind: RecordKind, employeeId: string) {
  return commandBase.superRefine((value, context) => {
    const issue = value.action === 1;
    const transfer = value.action === 3;
    const fail = (path: string, message: string) =>
      context.addIssue({ code: 'custom', path: [path], message });
    if (issue !== (value.expectedCurrentRecordId === null))
      fail('expectedCurrentRecordId', 'Select the exact current record for this action.');
    if (kind === 'Asset' || kind === 'Sim') {
      const input = kind === 'Asset' ? value.asset : value.sim;
      if (
        !input ||
        (kind === 'Asset' ? value.sim : value.asset) ||
        value.certification ||
        ![1, 2, 3].includes(value.action)
      ) {
        fail('action', 'This action or input does not belong to the selected record family.');
        return;
      }
      if (!issue !== Boolean(input.receiverAccountId))
        fail('receiverAccountId', 'Return and transfer require a receiving account.');
      if (
        transfer !== Boolean(input.targetEmployeeId) ||
        transfer !== Boolean(input.expectedTargetVersion) ||
        transfer !== Boolean(input.targetEvidenceDocumentId)
      )
        fail(
          'targetEmployeeId',
          'Transfers require a receiving employee, current version and separate evidence.',
        );
      if (input.targetEmployeeId && sameId(input.targetEmployeeId, employeeId))
        fail('targetEmployeeId', 'Select a different receiving employee.');
      if (kind === 'Asset' && value.asset) {
        if (
          issue !== Boolean(value.asset.assetReference) ||
          transfer !== Boolean(value.asset.targetCondition)
        )
          fail('asset', 'Asset metadata does not match this action.');
      } else if (value.sim) {
        if (
          issue !== Boolean(value.sim.simReference) ||
          issue !== Boolean(value.sim.phoneNumber) ||
          (issue || transfer) !== Boolean(value.sim.creditTierCode)
        )
          fail('sim', 'SIM metadata does not match this action.');
      }
    } else {
      const input = value.certification;
      if (!input || value.asset || value.sim || ![1, 4, 5, 6, 7].includes(value.action)) {
        fail('action', 'Unsupported certification action.');
        return;
      }
      const metadata = [1, 4, 7].includes(value.action);
      if (
        metadata !== Boolean(input.type) ||
        metadata !== Boolean(input.number) ||
        metadata !== Boolean(input.issuer) ||
        metadata !== Boolean(input.issuedOn) ||
        (!metadata && input.expiresOn)
      )
        fail('certification', 'Certificate metadata does not match this action.');
      if (input.issuedOn && input.expiresOn && input.expiresOn < input.issuedOn)
        fail('expiresOn', 'Expiry cannot precede issue date.');
    }
  });
}
export type RecordCommand = z.infer<typeof commandBase>;
export const assetRecordSchema = z.strictObject({
  id: recordId,
  employeeId: recordId,
  assetReference: z.string().min(1).max(100),
  issuedAt: instant,
  returnedAt: instant.nullable(),
  issueCondition: z.string().max(1000),
  returnCondition: z.string().max(1000).nullable(),
  returnedToAccountId: z.string().nullable(),
  actorId: z.string(),
  version: rowVersion,
});
export const simRecordSchema = z.strictObject({
  id: recordId,
  employeeId: recordId,
  revision: z.number().int().positive(),
  supersedesId: recordId.nullable(),
  transferredFromId: recordId.nullable(),
  simReference: z.string().min(1).max(100),
  phoneNumber: z.string().min(1).max(16),
  creditTierCode: z.string().max(50),
  status: z.union([z.literal(1), z.literal(2), z.literal(3)]),
  issuedOn: day,
  effectiveOn: day,
  returnedOn: day.nullable(),
  issueDocumentId: recordId.nullable(),
  returnDocumentId: recordId.nullable(),
  returnedToAccountId: recordId.nullable(),
  actorId: z.string(),
  recordedAt: instant,
  approvalRequestId: recordId.nullable(),
  reason: z.string().nullable(),
});
export const certificationRecordSchema = z.strictObject({
  id: recordId,
  employeeId: recordId,
  revision: z.number().int().positive(),
  supersedesId: recordId.nullable(),
  type: z.string().max(100),
  number: z.string().max(100),
  issuer: z.string().max(200),
  issuedOn: day,
  expiresOn: day.nullable(),
  effectiveOn: day,
  status: z.union([z.literal(1), z.literal(2), z.literal(3)]),
  isExpired: z.boolean(),
  employeeDocumentId: recordId.nullable(),
  actionDocumentId: recordId.nullable(),
  actorId: z.string(),
  recordedAt: instant,
  approvalRequestId: recordId.nullable(),
  reason: z.string().nullable(),
});
export type AssetRecord = z.infer<typeof assetRecordSchema>;
export type SimRecord = z.infer<typeof simRecordSchema>;
export type CertificationRecord = z.infer<typeof certificationRecordSchema>;
export type OperationalRecord = AssetRecord | SimRecord | CertificationRecord;
const summary = z
  .strictObject({
    id: recordId,
    employeeId: recordId,
    state: z.number().int().min(0).max(4),
    requesterId: recordId,
    requestedAt: instant,
    closedAt: instant.nullable(),
    version: rowVersion,
  })
  .refine(
    (value) =>
      (value.state === 0) === (value.closedAt === null) &&
      (value.closedAt === null || Date.parse(value.closedAt) >= Date.parse(value.requestedAt)),
    'Request closure does not match its state or submission time.',
  );
export type RecordRequest = z.infer<typeof summary>;
export function boundPage<T extends z.ZodType<{ id: string; employeeId: string }>>(
  schema: T,
  employeeId: string,
  page: number,
  pageSize = 25,
) {
  return z
    .strictObject({
      items: z.array(schema).max(pageSize),
      totalCount: z.number().int().nonnegative(),
      page: z.literal(page),
      pageSize: z.literal(pageSize),
    })
    .refine(
      (value) =>
        value.items.every((row) => sameId(row.employeeId, employeeId)) &&
        new Set(value.items.map((row) => row.id.toLowerCase())).size === value.items.length &&
        value.totalCount >= value.items.length,
      'Record response does not match the employee or page.',
    );
}
export const requestPageSchema = (employeeId: string, page: number, state?: number) =>
  boundPage(summary, employeeId, page).refine(
    (value) => state === undefined || value.items.every((row) => row.state === state),
    'Request state does not match the selected filter.',
  );
export function operationalPageSchema(
  kind: RecordKind,
  employeeId: string,
  page: number,
  history: boolean,
  asOf = '',
) {
  const schema: z.ZodType<OperationalRecord> =
    kind === 'Asset'
      ? assetRecordSchema
      : kind === 'Sim'
        ? simRecordSchema
        : certificationRecordSchema;
  return boundPage(schema, employeeId, page).refine(
    (result) =>
      history ||
      result.items.every((record) => {
        if ('returnedAt' in record)
          return asOf
            ? Date.parse(record.issuedAt) <= Date.parse(asOf) &&
                (record.returnedAt === null || Date.parse(record.returnedAt) > Date.parse(asOf))
            : record.returnedAt === null;
        if ('simReference' in record)
          return record.status !== 3 && (!asOf || record.effectiveOn <= asOf);
        return (
          !asOf ||
          (record.effectiveOn <= asOf &&
            record.isExpired === (record.expiresOn !== null && record.expiresOn < asOf))
        );
      }),
    'Record state or date does not match the requested view.',
  );
}
export const requestDetailSchema = (employeeId: string, kind: RecordKind, requestId: string) =>
  z
    .strictObject({
      request: summary,
      kind: z.literal({ Asset: 1, Sim: 2, Certification: 3 }[kind]),
      command: commandSchema(kind, employeeId),
      approvedPolicyVersion: z.string().min(1).max(100),
      decisions: z.array(
        z.strictObject({
          id: recordId,
          decision: z.number().int().min(1).max(3),
          actorId: z.string().min(1),
          roleCode: z.string().min(1),
          reason: z.string().nullable(),
          decidedAt: instant,
        }),
      ),
    })
    .refine(
      (value) =>
        sameId(value.request.employeeId, employeeId) && sameId(value.request.id, requestId),
      'Request does not match the selected employee and resource.',
    )
    .refine(
      (value) =>
        value.request.state === 0 || value.request.state === 4
          ? value.decisions.length === 0
          : value.decisions.length === 1 &&
            value.decisions[0]!.decision === value.request.state &&
            !sameId(value.decisions[0]!.actorId, value.request.requesterId) &&
            Date.parse(value.decisions[0]!.decidedAt) >= Date.parse(value.request.requestedAt) &&
            Date.parse(value.decisions[0]!.decidedAt) === Date.parse(value.request.closedAt!),
      'Decisions do not match the request state.',
    );
export type RecordRequestDetail = z.infer<ReturnType<typeof requestDetailSchema>>;
export const assetEventsSchema = (custodyId: string) =>
  z
    .array(
      z.strictObject({
        id: recordId,
        custodyId: recordId,
        revision: z.number().int().positive(),
        action: z.union([z.literal(1), z.literal(2)]),
        effectiveAt: instant,
        condition: z.string().max(1000),
        evidenceDocumentId: recordId,
        receiverAccountId: recordId.nullable(),
        transferredFromCustodyId: recordId.nullable(),
        approvalRequestId: recordId,
        actorId: z.string(),
        recordedAt: instant,
        reason: z.string(),
      }),
    )
    .refine(
      (rows) =>
        rows.every((row) => sameId(row.custodyId, custodyId)) &&
        new Set(rows.map((row) => row.id)).size === rows.length,
      'History does not match this custody.',
    );
export const employeeVersionSchema = (employeeId: string) =>
  currentUpdateSchema
    .strict()
    .extend({ version: rowVersion, section: z.literal(1) })
    .refine(
      (value) => sameId(value.employeeId, employeeId),
      'Employee version response does not match the selected employee.',
    )
    .transform(({ employeeId, version, asOf }) => ({ employeeId, version, asOf }));
export const evidenceSchema = z.array(documentSchema.strict().extend({ version: rowVersion }));
export type EvidenceDocument = z.infer<typeof evidenceSchema>[number];
export const decisionSchema = z.strictObject({
  expectedEmployeeVersion: rowVersion,
  expectedRequestVersion: rowVersion,
  decision: z.union([z.literal(1), z.literal(2), z.literal(3)]),
  reason: reasonSchema,
});
export const cancelSchema = z.strictObject({ expectedRequestVersion: rowVersion });
export function recordWriteSchema(employeeId: string, requestId?: string, decision?: number) {
  const optional = recordId.nullish();
  return z
    .strictObject({
      employeeId: recordId,
      version: rowVersion,
      status: z.number().int().min(0).max(4),
      recordRequestId: recordId,
      employeeRecordId: optional,
      decisionId: optional,
      documentId: optional,
      submissionId: optional,
      accountId: optional,
      lifecycleRequestId: optional,
      statusChangeId: optional,
      compensationRequestId: optional,
      compensationRecordId: optional,
      changeRequestId: optional,
      employeeChangeId: optional,
      bankVerificationId: optional,
      archiveChangeId: optional,
    })
    .refine(
      (value) =>
        sameId(value.employeeId, employeeId) &&
        (!requestId || sameId(value.recordRequestId, requestId)) &&
        (decision === undefined
          ? !value.decisionId && !value.employeeRecordId
          : Boolean(value.decisionId) &&
            (decision === 1 ? Boolean(value.employeeRecordId) : !value.employeeRecordId)) &&
        !value.documentId &&
        !value.submissionId &&
        !value.accountId &&
        !value.lifecycleRequestId &&
        !value.statusChangeId &&
        !value.compensationRequestId &&
        !value.compensationRecordId &&
        !value.changeRequestId &&
        !value.employeeChangeId &&
        !value.bankVerificationId &&
        !value.archiveChangeId,
      'Mutation response does not match this employee or operation.',
    );
}

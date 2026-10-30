import assert from 'node:assert/strict';
import test from 'node:test';
import {
  comparisonReport,
  parallelRun,
  parallelRunFor,
  reconciliationColumns,
  reconciliationUpload,
  reviewExplanations,
} from '../src/modules/hr/schemas/payroll-reconciliation.ts';

const id = '11111111-1111-4111-8111-111111111111';
const otherId = '22222222-2222-4222-8222-222222222222';
const fingerprint = 'a'.repeat(64);
const timestamp = '2026-10-30T08:00:00Z';
const report = () => ({
  reportId: id,
  erpBatchId: id,
  sourceFingerprint: fingerprint,
  sourceFileName: 'evidence.csv',
  sourceSha256: fingerprint,
  targetYear: 2026,
  targetMonth: 10,
  generatedAt: timestamp,
  generatedByUserId: id,
  auditReason: 'Authorized test evidence',
  summary: {
    totalEmployeesInSheet: 0,
    matchedEmployeesCount: 0,
    missingInErpCount: 0,
    missingInSheetCount: 0,
    duplicateInSheetCount: 0,
    exactMatchCount: 0,
    roundingDifferenceCount: 0,
    varianceCount: 0,
    totalSheetGross: 0,
    totalErpGross: 0,
    totalGrossVariance: 0,
    totalSheetNet: 0,
    totalErpNet: 0,
    totalNetVariance: 0,
  },
  variances: [],
});
const variance = (component, erpAmount, legacyAmount) => ({
  employeeId: id,
  employeeCode: 'EMP-0001',
  component,
  erpAmount,
  legacyAmount,
  variance: legacyAmount - erpAmount,
  rootCause: 9,
  justification: '',
});
const run = () => ({
  id,
  year: 2026,
  month: 10,
  state: 1,
  version: 1,
  preparedByUserId: id,
  preparedAt: timestamp,
  hrReviewerUserId: null,
  hrReviewedAt: null,
  hrNotes: null,
  financeReviewerUserId: null,
  financeReviewedAt: null,
  financeNotes: null,
  managementApproverUserId: null,
  managementApprovedAt: null,
  managementNotes: null,
  approvalFingerprint: null,
  erpBatchId: id,
  sourceFingerprint: fingerprint,
  sourceSha256: fingerprint,
  report: report(),
  preparedByEmployeeId: id,
  hrReviewerEmployeeId: null,
  financeReviewerEmployeeId: null,
  managementApproverEmployeeId: null,
  financeFingerprint: null,
  summary: {
    totalEmployees: 0,
    matchedEmployees: 0,
    discrepancyCount: 0,
    erpTotalGross: 0,
    legacyTotalGross: 0,
    grossVariance: 0,
    erpTotalNet: 0,
    legacyTotalNet: 0,
    netVariance: 0,
    allVariancesExplained: false,
  },
  variances: [variance('Eobi', 100, 110), variance('IncomeTax', 20, 10)],
});

test('comparison upload requires every explicit statutory mapping and selected frozen batch', () => {
  const input = {
    year: 2026,
    month: 10,
    erpBatchId: id,
    reason: 'Compare authorized source',
    fileName: 'evidence.csv',
    contentType: 'text/csv',
    base64: 'YWJj',
    columnMapping: Object.fromEntries(reconciliationColumns.map((column) => [column, column])),
  };
  assert.equal(reconciliationUpload.safeParse(input).success, true);
  for (const field of ['Eobi', 'Sessi', 'IncomeTax']) {
    const copy = JSON.parse(JSON.stringify(input));
    delete copy.columnMapping[field];
    assert.equal(reconciliationUpload.safeParse(copy).success, false);
  }
  assert.equal(reconciliationUpload.safeParse({ ...input, erpBatchId: '' }).success, false);
  assert.equal(
    reconciliationUpload.safeParse({
      ...input,
      columnMapping: { ...input.columnMapping, Eobi: 'IncomeTax' },
    }).success,
    false,
  );
});

test('saved comparisons reject missing evidence and mismatched period, batch or fingerprints', () => {
  assert.equal(parallelRun.safeParse(run()).success, true);
  for (const change of [
    { report: null },
    { month: 9 },
    { erpBatchId: otherId },
    { sourceFingerprint: 'b'.repeat(64) },
    { sourceSha256: 'b'.repeat(64) },
    { state: 0 },
  ]) {
    assert.equal(parallelRun.safeParse({ ...run(), ...change }).success, false);
  }
  assert.equal(parallelRunFor(otherId).safeParse(run()).success, false);
  assert.equal(comparisonReport.safeParse({ ...report(), sourceFingerprint: '' }).success, false);
});

test('HR explanations preserve distinct offsetting statutory amounts and immutable row identity', () => {
  const source = run();
  const original = JSON.parse(JSON.stringify(source));
  const result = reviewExplanations(source, [
    {
      rootCause: 6,
      justification: 'EOBI source evidence reviewed',
      employeeCode: 'EMP-9999',
      erpAmount: 999,
    },
    { rootCause: 6, justification: 'Tax source evidence reviewed' },
  ]);
  assert.equal(result.length, 2);
  for (let i = 0; i < result.length; i++) {
    for (const key of [
      'employeeId',
      'employeeCode',
      'component',
      'erpAmount',
      'legacyAmount',
      'variance',
    ]) {
      assert.equal(result[i][key], original.variances[i][key]);
    }
  }
  assert.deepEqual(source, original);
});

test('HR review requires all retained rows, evidence and valid nonzero-variance classifications', () => {
  const source = run();
  assert.throws(() => reviewExplanations(source, []));
  for (const edit of [
    { rootCause: 1, justification: 'Exact match' },
    { rootCause: 6, justification: '' },
    { rootCause: 6, justification: 'First line\nsecond line' },
    { rootCause: 9, justification: 'Unknown classification' },
  ]) {
    assert.throws(() =>
      reviewExplanations(source, [edit, { rootCause: 6, justification: 'Reviewed' }]),
    );
  }
});

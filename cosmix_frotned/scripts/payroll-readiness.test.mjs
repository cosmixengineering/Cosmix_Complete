import assert from 'node:assert/strict';
import test from 'node:test';
import {
  companyPayrollReadinessSchema,
  currentReadinessReport,
} from '../src/modules/hr/schemas/payroll-readiness.ts';

const report = () => ({
  isReady: true,
  evaluationPeriod: '2026-10-01',
  evaluatedAtUtc: '2026-10-30T08:00:00Z',
  totalActiveEmployees: 1,
  readyEmployeesCount: 1,
  blockingIssues: [],
  warnings: [],
  coverage: {
    employeesWithSalaryCount: 1,
    employeesWithShiftCount: 1,
    employeesWithBankCount: 1,
    employeesWithStatutoryCount: 1,
    activePostingProfilesCount: 1,
    activeCostCentersCount: 3,
    pendingPolicyApprovalsCount: 0,
  },
});
test('readiness validates real response counts and never accepts contradictory ready status', () => {
  assert.equal(companyPayrollReadinessSchema.parse(report()).isReady, true);
  for (const change of [
    { isReady: 'true' },
    { readyEmployeesCount: 2 },
    { totalActiveEmployees: -1 },
    {
      blockingIssues: [
        { category: 'Salary', code: 'missing_base_salary', message: 'Missing', isBlocking: true },
      ],
    },
  ]) {
    assert.equal(
      companyPayrollReadinessSchema.safeParse({ ...report(), ...change }).success,
      false,
    );
  }
});
test('failed refresh, fetching, unready access and another month cannot display cached ready data', () => {
  const data = companyPayrollReadinessSchema.parse(report());
  const state = { data, isSuccess: true, isFetching: false, isError: false };
  assert.equal(currentReadinessReport(state, '2026-10-01', true), data);
  assert.equal(currentReadinessReport(state, '2026-09-01', true), null);
  assert.equal(currentReadinessReport(state, '2026-10-01', false), null);
  for (const change of [
    { isFetching: true },
    { isError: true },
    { isSuccess: false },
    { data: undefined },
  ]) {
    assert.equal(currentReadinessReport({ ...state, ...change }, '2026-10-01', true), null);
  }
});

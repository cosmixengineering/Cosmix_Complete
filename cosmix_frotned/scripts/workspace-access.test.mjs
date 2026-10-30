import assert from 'node:assert/strict';
import test from 'node:test';
import { canOpenWorkspace, workspaceHome, workspaceLabel } from '../src/auth/workspace-access.ts';

const employeeId = '11111111-1111-4111-8111-111111111111';
const siteId = '22222222-2222-4222-8222-222222222222';
const user = (...grants) => ({ account: { employeeId }, grants });
const grant = (permission, scopeKind = 7, targetId = null) => ({ permission, scopeKind, targetId });

test('HR and administrative accounts retain their supported landing pages', () => {
  assert.equal(workspaceHome(user(grant('hr.employees.read'))), '/hr/dashboard');
  for (const permission of ['access.roles.read', 'access.accounts.read']) {
    assert.equal(workspaceHome(user(grant(permission))), '/admin/dashboard');
  }
});

test('Finance enters permitted accounting or payroll source workspace without HR records', () => {
  const finance = user(grant('finance.ledger.read'));
  assert.equal(workspaceHome(finance), '/hr/payroll-accounting');
  assert.equal(workspaceLabel(finance), 'Payroll & Finance');
  assert.equal(canOpenWorkspace(finance, '/hr/employees'), false);
  assert.equal(canOpenWorkspace(finance, '/hr/requests'), false);
  assert.equal(workspaceHome(user(grant('attendance.payroll-salary.read'))), '/hr/payroll-handoff');
});

test('assigned supervisors enter attendance or their review queue using the matching scope', () => {
  const supervisor = user(grant('attendance.summaries.read', 3, siteId));
  assert.equal(workspaceHome(supervisor), '/hr/attendance');
  assert.equal(canOpenWorkspace(supervisor, '/hr/attendance/tracking'), false);
  assert.equal(
    workspaceHome(user(grant('attendance.overtime.review', 3, siteId))),
    '/hr/overtime/review',
  );
  assert.equal(
    workspaceHome(user(grant('attendance.corrections.review', 3, siteId))),
    '/hr/attendance/corrections/review',
  );
});

test('employee Self grants do not expose management navigation or a restricted HR landing', () => {
  const employee = user(
    grant('attendance.summaries.read', 1, employeeId),
    grant('hr.employees.read', 1, employeeId),
  );
  assert.equal(workspaceHome(employee), '/account');
  assert.equal(workspaceLabel(employee), 'My account');
  for (const path of ['/hr/dashboard', '/hr/employees', '/hr/attendance', '/hr/payroll-handoff']) {
    assert.equal(canOpenWorkspace(employee, path), false);
  }
  assert.equal(canOpenWorkspace(employee, '/account'), true);
});

test('scope and field prerequisites cannot be borrowed from unrelated grants', () => {
  assert.equal(
    canOpenWorkspace(
      user(grant('hr.employees.read'), grant('hr.employees.documents.read', 2, siteId)),
      '/hr/reports/document-expiry',
    ),
    false,
  );
  assert.equal(
    canOpenWorkspace(
      user(grant('hr.employees.read'), grant('hr.employees.documents.read')),
      '/hr/reports/document-expiry',
    ),
    true,
  );
  for (const target of [null, 'invalid', '00000000-0000-0000-0000-000000000000']) {
    assert.equal(
      canOpenWorkspace(user(grant('attendance.summaries.read', 3, target)), '/hr/attendance'),
      false,
    );
  }
  assert.equal(
    canOpenWorkspace(user(grant('hr.employees.read', 7, employeeId)), '/hr/employees'),
    false,
  );
  assert.equal(canOpenWorkspace(null, '/account'), false);
});

import assert from 'node:assert/strict';
import test from 'node:test';
import { initialAccessTab } from '../src/modules/administration/utils/route-tabs.ts';

test('Users & Accounts enters employee access setup, including a trailing slash', () => {
  assert.equal(initialAccessTab('/admin/users'), 'assignments');
  assert.equal(initialAccessTab('/admin/users/'), 'assignments');
});

test('Roles & Permissions retains the role directory as its initial view', () => {
  assert.equal(initialAccessTab('/admin/roles'), 'roles');
  assert.equal(initialAccessTab('/admin/roles/'), 'roles');
});

import assert from 'node:assert/strict';
import test from 'node:test';
import { hiringSummaryNotice } from '../src/modules/hr/utils/hiring-summary.ts';

test('unsubmitted draft notice is identical for Admin reviewers and other viewers', () => {
  assert.deepEqual(hiringSummaryNotice(true, true), hiringSummaryNotice(true, false));
  const notice = hiringSummaryNotice(true, true);
  assert.match(notice.label, /Draft not submitted/);
  assert.match(notice.message, /Approval decisions are unavailable for a draft/);
  assert.doesNotMatch(notice.message, /As HR Desk|handled by|Admin Review/);
});

test('submitted reviewer summary points to existing Admin Review action without granting a decision', () => {
  const notice = hiringSummaryNotice(false, true);
  assert.equal(notice.label, 'Read-only summary');
  assert.match(notice.message, /Use Admin Review/);
  assert.doesNotMatch(notice.message, /As HR Desk|not submitted/);
});

test('submitted non-reviewer summary describes review authority without assuming viewer role', () => {
  const notice = hiringSummaryNotice(false, false);
  assert.equal(notice.label, 'Read-only summary');
  assert.match(notice.message, /require authorized Admin review/);
  assert.doesNotMatch(notice.message, /As HR Desk|Use Admin Review/);
});

import assert from 'node:assert/strict';
import test from 'node:test';
import { evidenceRecord, sectionEvidence } from '../src/modules/hr/utils/evidence-record.ts';

test('flat and wrapped evidence retain false, zero and null values', () => {
  const fields = { eligible: false, balance: 0, note: null };
  assert.equal(sectionEvidence(fields, 'basic'), fields);
  assert.equal(sectionEvidence({ basic: fields }, 'basic'), fields);
});

test('nested field transitions tolerate a missing or scalar prior value', () => {
  for (const prior of [null, undefined, false, 0, 'old address', ['old address']]) {
    assert.deepEqual(Object.keys(evidenceRecord(prior)), []);
    const next = { city: 'Karachi' };
    assert.deepEqual(Object.keys(evidenceRecord(next)), ['city']);
  }
});

test('a scalar matching a section name does not become a character-indexed record', () => {
  const flat = { bank: 'Legacy bank', accountTitle: 'Synthetic employee' };
  assert.equal(sectionEvidence(flat, 'bank'), flat);
  assert.deepEqual(sectionEvidence(null, 'bank'), {});
});

test('null or missing section in an employee wrapper does not leak other sections', () => {
  const wrapper = { basic: { name: 'Ali' }, identity: { cnic: '123' }, bank: null };
  assert.deepEqual(sectionEvidence(wrapper, 'bank'), {});
});

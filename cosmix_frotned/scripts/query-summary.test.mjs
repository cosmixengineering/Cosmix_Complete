import assert from 'node:assert/strict';
import test from 'node:test';
import { sourceCount, sourceReady } from '../src/utils/query-summary.ts';

const loaded = { isError: false, isPending: false, isFetching: false, data: [] };

test('a real empty response is zero but initial or refreshing reads are loading', () => {
  assert.equal(sourceCount(loaded, 0), '0');
  assert.equal(sourceReady(loaded), true);
  for (const query of [
    { ...loaded, isPending: true, data: undefined },
    { ...loaded, isFetching: true, data: [{ id: 1 }] },
  ]) {
    assert.equal(sourceCount(query, 0), 'Loading…');
    assert.equal(sourceReady(query), false);
  }
});

test('failed or missing sources cannot present cached counts as current evidence', () => {
  for (const query of [
    { ...loaded, isError: true },
    { ...loaded, isError: true, data: [{ id: 1 }] },
    { ...loaded, data: undefined },
  ]) {
    assert.equal(sourceCount(query, 1), 'Unavailable');
    assert.equal(sourceReady(query), false);
  }
});

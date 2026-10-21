import { z } from 'zod';
import { ApiError } from '@/services/api/api-error';
import { financeError } from '../api/finance-messages';

export type OutstationIntent = { path: string; body: unknown; schema: z.ZodType };
export type OutstationWriteState = {
  pending: boolean;
  uncertain: boolean;
  reloadRequired: boolean;
  denied: boolean;
  error: string | null;
};
export const initialWriteState: OutstationWriteState = {
  pending: false,
  uncertain: false,
  reloadRequired: false,
  denied: false,
  error: null,
};
export function outstationError(error: unknown) {
  if (error instanceof z.ZodError)
    return 'The service returned mismatched or incomplete deployment evidence. Reload before proceeding.';
  if (error instanceof ApiError && error.status === 503)
    return 'Deployment data or protected evidence is unavailable. No result is confirmed.';
  const message = financeError(error);
  return error instanceof ApiError && error.code?.startsWith('deployment_')
    ? `${message} (${error.code})`
    : message;
}

// One in-memory intent owns its immutable serialized payload and key until resolved or discarded.
export class OutstationOperation {
  state = { ...initialWriteState };
  private retained: { intent: OutstationIntent; json: string; key: string } | null = null;
  constructor(
    private send: (
      path: string,
      body: unknown,
      key: string,
      signal: AbortSignal,
    ) => Promise<unknown>,
    private changed: (state: OutstationWriteState) => void,
  ) {}
  private update(state: OutstationWriteState) {
    this.state = state;
    this.changed(state);
  }
  async run(intent: OutstationIntent, signal: AbortSignal) {
    if (signal.aborted || this.state.pending || this.state.reloadRequired || this.state.denied)
      return;
    const json = JSON.stringify(intent.body);
    if (
      this.retained &&
      (this.retained.json !== json || this.retained.intent.path !== intent.path)
    ) {
      this.update({
        ...this.state,
        error: 'The result is uncertain. Retry the unchanged action or reload and check history.',
      });
      return;
    }
    this.retained ??= { intent, json, key: crypto.randomUUID() };
    const held = this.retained;
    this.update({ ...initialWriteState, pending: true });
    try {
      const response = await this.send(held.intent.path, JSON.parse(held.json), held.key, signal);
      const result = held.intent.schema.parse(response);
      if (signal.aborted) return;
      this.retained = null;
      this.update({ ...initialWriteState, reloadRequired: true });
      return result;
    } catch (error) {
      if (signal.aborted) return;
      const definitive = error instanceof ApiError && error.status >= 400 && error.status < 500;
      if (definitive) this.retained = null;
      this.update({
        pending: false,
        uncertain: !definitive,
        reloadRequired: definitive,
        denied: error instanceof ApiError && [401, 403].includes(error.status),
        error: outstationError(error),
      });
    }
  }
  retry(signal: AbortSignal) {
    return this.retained ? this.run(this.retained.intent, signal) : Promise.resolve(undefined);
  }
  dispose() {
    this.retained = null;
  }
}

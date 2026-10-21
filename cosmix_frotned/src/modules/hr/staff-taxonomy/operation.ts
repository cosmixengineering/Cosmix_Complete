import { ApiError } from '@/services/api/api-error';
import {
  cancelSchema,
  decisionSchema,
  proposalSchema,
  sameId,
  writeResultSchema,
  type WriteExpectation,
} from './contracts';
import { captureTaxonomyIntent, type TaxonomyIntent } from './intent';
import { definitiveTaxonomyFailure, taxonomyError } from './messages';

export type OperationState = {
  pending: boolean;
  uncertain: boolean;
  reloadRequired: boolean;
  denied: boolean;
  error: string | null;
};
const initial = (): OperationState => ({
  pending: false,
  uncertain: false,
  reloadRequired: false,
  denied: false,
  error: null,
});
function command(body: unknown, expected: WriteExpectation) {
  if (expected.action === 'request') return proposalSchema(expected.policy).parse(body);
  const parsed =
    expected.action === 'decision' ? decisionSchema.parse(body) : cancelSchema.parse(body);
  if (
    !sameId(expected.request.policyId, expected.policy.id) ||
    expected.request.state !== 'Pending' ||
    parsed.expectedPolicyVersion !== expected.policy.version ||
    parsed.expectedRequestVersion !== expected.request.version ||
    ('decision' in parsed && parsed.decision !== expected.decision)
  )
    throw new Error('Reload and review the exact policy and request versions.');
  return parsed;
}
export class TaxonomyOperation {
  state = initial();
  private intent: TaxonomyIntent | null = null;
  private expectation: WriteExpectation | null = null;
  private disposed = false;
  constructor(
    private send: (intent: TaxonomyIntent, signal: AbortSignal) => Promise<unknown>,
    private changed: (state: OperationState) => void = () => {},
  ) {}
  private update(state: Partial<OperationState>) {
    this.state = { ...this.state, ...state };
    this.changed(this.state);
  }
  dispose() {
    this.disposed = true;
    this.intent = null;
    this.expectation = null;
    this.state = initial();
  }
  async run(body: unknown, expected: WriteExpectation, signal: AbortSignal) {
    if (this.disposed || signal.aborted || this.state.pending || this.state.reloadRequired) return;
    const path =
      '/hr/staff-taxonomy/requests' +
      (expected.action === 'request'
        ? ''
        : `/${expected.request.id}/${expected.action === 'decision' ? 'decision' : 'cancel'}`);
    if (this.expectation && JSON.stringify(this.expectation) !== JSON.stringify(expected))
      throw new Error('Retry the exact original resource versions or reload.');
    const parsed = this.intent ? body : command(body, expected);
    this.intent = captureTaxonomyIntent(path, parsed, this.intent);
    this.expectation ??= structuredClone(expected);
    const intent = this.intent;
    const expectation = this.expectation;
    this.update({ pending: true, error: null });
    try {
      const response = await this.send(intent, signal);
      if (this.disposed || signal.aborted) return;
      const result = writeResultSchema(expectation).parse(response);
      this.intent = null;
      this.expectation = null;
      this.update({ uncertain: false });
      return result;
    } catch (error) {
      if (this.disposed || signal.aborted) return;
      const definitive = definitiveTaxonomyFailure(error);
      if (definitive) {
        this.intent = null;
        this.expectation = null;
      }
      this.update({
        uncertain: !definitive,
        error: taxonomyError(error),
        denied: error instanceof ApiError && [401, 403].includes(error.status),
        reloadRequired:
          definitive &&
          error instanceof ApiError &&
          [401, 403, 404, 409, 503].includes(error.status),
      });
    } finally {
      if (!this.disposed && !signal.aborted) this.update({ pending: false });
    }
  }
}

import { useRef, useState, type FormEvent } from 'react';
import { z } from 'zod';
import { request } from '@/services/api/http-client';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { useAuth } from './use-auth';
import { authErrorMessage } from './messages';

const responseSchema = z.object({
  approvalId: z.uuid(),
  nextStep: z.literal('sign_in_with_sms_and_enroll_authenticator'),
});

export function AuthenticatorReset({
  accountId,
  employeeId,
  version,
  enabled,
  onSaved,
}: {
  accountId: string;
  employeeId: string;
  version: string;
  enabled: boolean;
  onSaved: () => void;
}) {
  const { user, status } = useAuth();
  const canReview = ['access.accounts.web.manage', 'access.roles.permissions.manage'].every(
    (permission) =>
      user?.grants.some((grant) => grant.permission === permission && grant.scopeKind === 1),
  );
  if (
    !enabled ||
    !canReview ||
    status !== 'authenticated' ||
    !user?.account ||
    user.account.accountId === accountId ||
    user.account.employeeId === employeeId
  )
    return null;
  return (
    <ResetForm
      key={`${user.account.accountId}:${accountId}:${version}`}
      accountId={accountId}
      version={version}
      onSaved={onSaved}
    />
  );
}

function ResetForm({
  accountId,
  version,
  onSaved,
}: {
  accountId: string;
  version: string;
  onSaved: () => void;
}) {
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const pending = useRef(false);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (pending.current || message) return;
    const data = new FormData(event.currentTarget);
    pending.current = true;
    setBusy(true);
    setError('');
    try {
      responseSchema.parse(
        await request(`/auth/web/authenticator/accounts/${accountId}/reset`, {
          method: 'POST',
          body: {
            expectedAccountVersion: version,
            reason: String(data.get('reason')).trim(),
            approvalReference: String(data.get('approvalReference')).trim(),
          },
        }),
      );
      setMessage(
        'Reset recorded. Existing sessions and recovery codes were revoked. The employee must sign in with SMS and enroll a new authenticator.',
      );
      onSaved();
    } catch (failure) {
      setError(authErrorMessage(failure));
    } finally {
      pending.current = false;
      setBusy(false);
    }
  }
  return (
    <section className="hr-card">
      <h3>Independent authenticator recovery</h3>
      <p>
        Review the employee’s identity before resetting their authenticator. This revokes all
        sessions and recovery codes. SMS delivery must be available for the next sign-in.
      </p>
      {error && (
        <p role="alert" className="hr-message hr-message--error">
          {error}
        </p>
      )}
      {message ? (
        <p role="status">{message}</p>
      ) : (
        <form onSubmit={(event) => void submit(event)}>
          <fieldset disabled={busy} className="hr-form-grid">
            <TextInput
              name="reason"
              label="Identity review and reset reason"
              minLength={10}
              maxLength={1000}
              required
            />
            <TextInput
              name="approvalReference"
              label="Approved identity-review reference"
              maxLength={500}
              required
            />
            <Button type="submit">
              {busy ? 'Recording review…' : 'Approve authenticator reset'}
            </Button>
          </fieldset>
        </form>
      )}
    </section>
  );
}

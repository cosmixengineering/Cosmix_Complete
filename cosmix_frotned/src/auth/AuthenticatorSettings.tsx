import { useEffect, useRef, useState, type FormEvent } from 'react';
import { z } from 'zod';
import { request, blockSessionRefresh } from '@/services/api/http-client';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { PasswordInput } from '@/components/forms/PasswordInput';
import { useAuth } from './use-auth';
import { authErrorMessage } from './messages';

const statusSchema = z.object({
  authenticatorRequired: z.boolean(),
  remainingRecoveryCodes: z.number().int().min(0),
});
const setupSchema = z.object({
  credentialId: z.uuid(),
  base32Secret: z.string().regex(/^[A-Z2-7]+$/),
  uri: z.string().startsWith('otpauth://totp/'),
  expiresAt: z.iso.datetime({ offset: true }),
});
const codesSchema = z.object({
  recoveryCodes: z.array(z.string().regex(/^[A-F0-9]{4}(?:-[A-F0-9]{4}){7}$/)).length(10),
  nextStep: z.enum(['sign_in_again', 'saved']),
});
const root = '/auth/web/authenticator';

export function AuthenticatorSettings() {
  const { user, status } = useAuth();
  if (status !== 'authenticated' || !user?.account) return null;
  return <AuthenticatorPanel key={user.account.accountId} />;
}

function AuthenticatorPanel() {
  const { passwordResetCompleted } = useAuth();
  const [state, setState] = useState<z.infer<typeof statusSchema> | null>(null);
  const [setup, setSetup] = useState<z.infer<typeof setupSchema> | null>(null);
  const [codes, setCodes] = useState<z.infer<typeof codesSchema> | null>(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [revision, setRevision] = useState(0);
  const pending = useRef(false);
  const lifetime = useRef<AbortController | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    lifetime.current = controller;
    void request(root, { signal: controller.signal })
      .then((result) => {
        if (!controller.signal.aborted) setState(statusSchema.parse(result));
      })
      .catch((failure: unknown) => {
        if (!controller.signal.aborted) setError(authErrorMessage(failure));
      });
    return () => controller.abort();
  }, [revision]);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (pending.current || !state) return;
    const form = event.currentTarget;
    const data = new FormData(form);
    pending.current = true;
    setBusy(true);
    setError('');
    const signal = lifetime.current?.signal;
    try {
      if (setup) {
        const result = codesSchema.parse(
          await request(root + '/confirm', {
            method: 'POST',
            anonymous: true,
            signal,
            body: { credentialId: setup.credentialId, code: String(data.get('code')) },
          }),
        );
        if (signal?.aborted) return;
        blockSessionRefresh();
        setCodes(result);
        setSetup(null);
      } else if (state.authenticatorRequired) {
        const result = codesSchema.parse(
          await request(root + '/recovery-codes', {
            method: 'POST',
            anonymous: true,
            signal,
            body: { password: String(data.get('password')), code: String(data.get('code')) },
          }),
        );
        if (!signal?.aborted) setCodes(result);
      } else {
        const result = setupSchema.parse(
          await request(root + '/enroll', {
            method: 'POST',
            anonymous: true,
            signal,
            body: { password: String(data.get('password')) },
          }),
        );
        if (!signal?.aborted) setSetup(result);
      }
    } catch (failure) {
      if (!signal?.aborted) setError(authErrorMessage(failure));
    } finally {
      form.querySelectorAll<HTMLInputElement>('input').forEach((input) => {
        input.value = '';
      });
      pending.current = false;
      if (!signal?.aborted) setBusy(false);
    }
  }

  return (
    <section className="hr-card" aria-labelledby="authenticator-heading">
      <h2 id="authenticator-heading">Authenticator & recovery codes</h2>
      {error && (
        <p className="hr-message hr-message--error" role="alert">
          {error}
        </p>
      )}
      {codes ? (
        <>
          <p>
            Save these recovery codes somewhere private. Each code works once. They cannot be
            displayed again.
          </p>
          <ul>
            {codes.recoveryCodes.map((code) => (
              <li key={code}>
                <code>{code}</code>
              </li>
            ))}
          </ul>
          <Button
            onClick={() => {
              const signIn = codes.nextStep === 'sign_in_again';
              setCodes(null);
              if (signIn) void passwordResetCompleted();
              else setRevision((value) => value + 1);
            }}
          >
            {codes.nextStep === 'sign_in_again'
              ? 'I saved my codes — sign in again'
              : 'I saved my new codes'}
          </Button>
        </>
      ) : !state ? (
        <Button
          disabled={busy}
          onClick={() => {
            setError('');
            setRevision((value) => value + 1);
          }}
        >
          Reload authenticator status
        </Button>
      ) : (
        <form onSubmit={(event) => void submit(event)}>
          <fieldset disabled={busy} className="hr-form-grid">
            {setup ? (
              <>
                <p>
                  Add a time-based account named Cosmix in your authenticator using this setup key.
                  Then enter its six-digit code before{' '}
                  {new Date(setup.expiresAt).toLocaleTimeString()}.
                </p>
                <p>
                  <code style={{ overflowWrap: 'anywhere' }}>{setup.base32Secret}</code>
                </p>
                <p>
                  Confirmation signs out your existing sessions. Your recovery codes will appear
                  once.
                </p>
              </>
            ) : (
              <>
                <p>
                  {state.authenticatorRequired
                    ? `Authenticator enabled. ${state.remainingRecoveryCodes} unused recovery codes remain. Replacing codes invalidates all previous codes.`
                    : 'Protect web sign-in with an authenticator. Once enabled, SMS alone cannot sign you in.'}
                </p>
                <PasswordInput
                  label="Current password"
                  name="password"
                  autoComplete="current-password"
                  maxLength={1024}
                  required
                />
              </>
            )}
            {(setup || state.authenticatorRequired) && (
              <TextInput
                label="Fresh authenticator code"
                name="code"
                inputMode="numeric"
                autoComplete="one-time-code"
                pattern="[0-9]{6}"
                maxLength={6}
                required
              />
            )}
            <Button type="submit">
              {busy
                ? 'Please wait…'
                : setup
                  ? 'Confirm authenticator'
                  : state.authenticatorRequired
                    ? 'Replace recovery codes'
                    : 'Set up authenticator'}
            </Button>
          </fieldset>
        </form>
      )}
      {!codes && (
        <p>
          If you lose both your authenticator and codes, an authorized administrator must
          independently review your identity. Reset does not create a session.
        </p>
      )}
    </section>
  );
}

import { useEffect, useRef, useState, type FormEvent } from 'react';
import { z } from 'zod';
import {
  KeyRound,
  ShieldCheck,
  ShieldAlert,
  AlertTriangle,
  Copy,
  Check,
  RefreshCw,
} from 'lucide-react';
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
  const [copiedKey, setCopiedKey] = useState(false);
  const [copiedCodes, setCopiedCodes] = useState(false);
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

  const isEnrolled = state?.authenticatorRequired ?? false;

  return (
    <section className="account-security-card" aria-labelledby="authenticator-heading">
      <div className="sec-header">
        <div className="sec-header-left">
          <div className="sec-icon-badge">
            <KeyRound size={17} />
          </div>
          <div>
            <h2 id="authenticator-heading" className="sec-title">
              Authenticator & recovery codes
            </h2>
            <p className="sec-subtitle">
              Time-based one-time passwords (TOTP) and offline recovery backup
            </p>
          </div>
        </div>

        {state && (
          <div>
            {isEnrolled ? (
              <span className="sec-status-badge sec-status-badge--active">
                <ShieldCheck size={13} />
                <span>Enrolled & Active</span>
              </span>
            ) : (
              <span className="sec-status-badge sec-status-badge--inactive">
                <ShieldAlert size={13} />
                <span>Not Enabled</span>
              </span>
            )}
          </div>
        )}
      </div>

      {error && (
        <p className="hr-message hr-message--error" role="alert" style={{ margin: 0 }}>
          {error}
        </p>
      )}

      {codes ? (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
          <div
            style={{
              background: '#fffbeb',
              border: '1px solid #fde68a',
              borderRadius: '6px',
              padding: '12px 14px',
              display: 'flex',
              alignItems: 'flex-start',
              gap: '10px',
            }}
          >
            <AlertTriangle
              size={16}
              style={{ color: '#d97706', flexShrink: 0, marginTop: '2px' }}
            />
            <div>
              <strong style={{ fontSize: '12.5px', color: '#92400e', display: 'block' }}>
                Save these recovery codes somewhere private
              </strong>
              <span style={{ fontSize: '12px', color: '#b45309' }}>
                Each code works once. Keep them in a safe place. They cannot be displayed again.
              </span>
            </div>
          </div>

          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fill, minmax(200px, 1fr))',
              gap: '8px',
              background: '#f8fafc',
              border: '1px solid #e2e8f0',
              borderRadius: '6px',
              padding: '12px',
            }}
          >
            {codes.recoveryCodes.map((code) => (
              <div
                key={code}
                style={{
                  padding: '6px 10px',
                  background: '#ffffff',
                  border: '1px solid #e2e8f0',
                  borderRadius: '4px',
                  fontFamily: 'monospace',
                  fontSize: '11.5px',
                  fontWeight: 600,
                  color: '#0f172a',
                  textAlign: 'center',
                  letterSpacing: '0.5px',
                }}
              >
                {code}
              </div>
            ))}
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap' }}>
            <Button
              variant="secondary"
              className="sec-action-btn"
              onClick={() => {
                if (navigator.clipboard?.writeText) {
                  void navigator.clipboard.writeText(codes.recoveryCodes.join('\n'));
                  setCopiedCodes(true);
                  setTimeout(() => setCopiedCodes(false), 2000);
                }
              }}
            >
              {copiedCodes ? <Check size={13} style={{ color: '#16a34a' }} /> : <Copy size={13} />}
              <span>{copiedCodes ? 'Codes copied' : 'Copy all codes'}</span>
            </Button>
            <Button
              variant="primary"
              className="sec-action-btn"
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
          </div>
        </div>
      ) : !state ? (
        <div style={{ padding: '8px 0' }}>
          <Button
            variant="secondary"
            disabled={busy}
            className="sec-action-btn"
            onClick={() => {
              setError('');
              setRevision((value) => value + 1);
            }}
          >
            <RefreshCw size={13} />
            <span>Reload authenticator status</span>
          </Button>
        </div>
      ) : (
        <form onSubmit={(event) => void submit(event)}>
          <fieldset
            disabled={busy}
            style={{
              border: 'none',
              padding: 0,
              margin: 0,
              minWidth: 0,
              display: 'flex',
              flexDirection: 'column',
              gap: '12px',
            }}
          >
            {setup ? (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                <div
                  style={{
                    background: '#f8fafc',
                    border: '1px solid #e2e8f0',
                    borderRadius: '6px',
                    padding: '12px 14px',
                  }}
                >
                  <p
                    style={{
                      margin: '0 0 6px 0',
                      fontSize: '12.5px',
                      color: '#0f172a',
                      fontWeight: 600,
                    }}
                  >
                    Step 1: Add account in your authenticator app
                  </p>
                  <p style={{ margin: '0 0 10px 0', fontSize: '12px', color: '#64748b' }}>
                    Open Google Authenticator, Microsoft Authenticator, or 1Password and add an
                    account using this setup key:
                  </p>
                  <div
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: '8px',
                      flexWrap: 'wrap',
                    }}
                  >
                    <code
                      style={{
                        padding: '6px 12px',
                        background: '#ffffff',
                        border: '1px solid #cbd5e1',
                        borderRadius: '6px',
                        fontFamily: 'monospace',
                        fontSize: '13px',
                        fontWeight: 700,
                        letterSpacing: '1px',
                        color: '#0f172a',
                        overflowWrap: 'anywhere',
                      }}
                    >
                      {setup.base32Secret}
                    </code>
                    <Button
                      type="button"
                      variant="secondary"
                      className="sec-action-btn"
                      onClick={() => {
                        if (navigator.clipboard?.writeText) {
                          void navigator.clipboard.writeText(setup.base32Secret);
                          setCopiedKey(true);
                          setTimeout(() => setCopiedKey(false), 2000);
                        }
                      }}
                    >
                      {copiedKey ? (
                        <Check size={13} style={{ color: '#16a34a' }} />
                      ) : (
                        <Copy size={13} />
                      )}
                      <span>{copiedKey ? 'Copied' : 'Copy key'}</span>
                    </Button>
                  </div>
                  <p style={{ margin: '8px 0 0 0', fontSize: '11px', color: '#94a3b8' }}>
                    Setup key expires at {new Date(setup.expiresAt).toLocaleTimeString()}. Existing
                    web sessions will be signed out upon confirmation.
                  </p>
                </div>

                <div>
                  <p
                    style={{
                      margin: '0 0 8px 0',
                      fontSize: '12.5px',
                      color: '#0f172a',
                      fontWeight: 600,
                    }}
                  >
                    Step 2: Enter the 6-digit confirmation code
                  </p>
                  <div className="sec-form-row">
                    <div style={{ width: '160px' }}>
                      <TextInput
                        label="Authenticator code"
                        name="code"
                        inputMode="numeric"
                        autoComplete="one-time-code"
                        pattern="[0-9]{6}"
                        maxLength={6}
                        required
                        placeholder="000000"
                      />
                    </div>
                    <Button
                      type="submit"
                      disabled={busy}
                      variant="primary"
                      className="sec-action-btn"
                    >
                      <span>{busy ? 'Verifying…' : 'Confirm authenticator'}</span>
                    </Button>
                    <Button
                      type="button"
                      variant="secondary"
                      disabled={busy}
                      className="sec-action-btn"
                      onClick={() => setSetup(null)}
                    >
                      <span>Cancel</span>
                    </Button>
                  </div>
                </div>
              </div>
            ) : (
              <>
                <p className="sec-notice">
                  {state.authenticatorRequired
                    ? `Authenticator is enabled. ${state.remainingRecoveryCodes} unused recovery codes remain. Replacing codes invalidates all previous codes.`
                    : 'Protect web sign-in with an authenticator app. Once enabled, SMS alone cannot sign you in.'}
                </p>

                <div className="sec-form-row">
                  <div style={{ flex: '1 1 240px', maxWidth: '300px' }}>
                    <PasswordInput
                      label="Current password"
                      name="password"
                      autoComplete="current-password"
                      maxLength={1024}
                      required
                      placeholder="Enter current password"
                    />
                  </div>
                  {state.authenticatorRequired && (
                    <div style={{ flex: '0 0 160px', maxWidth: '160px' }}>
                      <TextInput
                        label="Fresh code"
                        name="code"
                        inputMode="numeric"
                        autoComplete="one-time-code"
                        pattern="[0-9]{6}"
                        maxLength={6}
                        required
                        placeholder="6-digit code"
                      />
                    </div>
                  )}
                  <Button
                    type="submit"
                    disabled={busy}
                    variant="primary"
                    className="sec-action-btn"
                  >
                    <KeyRound size={13} />
                    <span>
                      {busy
                        ? 'Please wait…'
                        : state.authenticatorRequired
                          ? 'Replace recovery codes'
                          : 'Set up authenticator'}
                    </span>
                  </Button>
                </div>
              </>
            )}
          </fieldset>
        </form>
      )}

      {!codes && (
        <div className="sec-footer-note">
          <ShieldAlert size={14} style={{ color: '#94a3b8', flexShrink: 0, marginTop: '2px' }} />
          <span>
            If you lose both your authenticator and recovery codes, an authorized administrator must
            independently review your identity. Reset does not create a session.
          </span>
        </div>
      )}
    </section>
  );
}

import { useRef, useState, type FormEvent } from 'react';
import { ArrowLeft, ArrowRight, CheckCircle2, LoaderCircle } from 'lucide-react';
import { Link, Navigate } from 'react-router-dom';
import { AuthLayout } from '@/layouts/AuthLayout';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { PasswordInput } from '@/components/forms/PasswordInput';
import { authAdapter } from '@/auth/adapter';
import { authErrorMessage } from '@/auth/messages';
import { useAuth } from '@/auth/use-auth';
import { useChallengeTime } from '@/auth/ChallengeStatus';
import { type OtpChallenge, isSystemAdmin } from '@/auth/contracts';

export function ForgotPasswordPage() {
  const { user, status, passwordResetCompleted } = useAuth();
  const [email, setEmail] = useState('');
  const [challenge, setChallenge] = useState<OtpChallenge | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState(false);
  const [busy, setBusy] = useState(false);
  const pending = useRef(false);
  const time = useChallengeTime(challenge);

  async function run(action: () => Promise<void>) {
    if (pending.current) return;
    pending.current = true;
    setBusy(true);
    setError(null);
    try {
      await action();
    } catch (failure) {
      setError(authErrorMessage(failure));
    } finally {
      pending.current = false;
      setBusy(false);
    }
  }
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    await run(async () => {
      if (!challenge) {
        const address = String(data.get('email')).trim();
        setChallenge(await authAdapter.forgotPassword(address));
        setEmail(address);
      } else {
        if (data.get('password') !== data.get('confirmPassword')) {
          setError('The passwords do not match.');
          return;
        }
        await authAdapter.resetPassword(
          challenge.challengeId,
          String(data.get('code')),
          String(data.get('password')),
        );
        setDone(true);
        await passwordResetCompleted();
      }
    });
    form
      .querySelectorAll<HTMLInputElement>(
        'input[name="code"], input[name="password"], input[name="confirmPassword"]',
      )
      .forEach((input) => {
        input.value = '';
      });
  }
  const isAdmin = isSystemAdmin(user);
  if (status === 'authenticated') {
    return <Navigate to={isAdmin ? '/admin/dashboard' : '/hr/dashboard'} replace />;
  }
  return (
    <AuthLayout>
      <section className="auth-content" aria-labelledby="recovery-title">
        <div className="auth-eyebrow">ACCOUNT RECOVERY</div>
        <h1 id="recovery-title">
          {done ? 'Password reset' : challenge ? 'Set a new password' : 'Forgot password?'}
        </h1>
        <p className="auth-subtitle">
          {done
            ? 'Your password has been changed. Sign in again with your new password.'
            : challenge
              ? 'If your account is eligible, a code will arrive on your registered mobile number.'
              : 'Enter your work email to request a recovery code.'}
        </p>
        {error && (
          <div className="auth-message auth-message--error" role="alert">
            {error}
          </div>
        )}
        {done ? (
          <div className="auth-success">
            <CheckCircle2 size={28} aria-hidden="true" />
            <Link className="text-link" to="/sign-in">
              Return to sign in
            </Link>
          </div>
        ) : (
          <>
            <form
              key={challenge?.challengeId ?? 'request'}
              onSubmit={(event) => void submit(event)}
              className="auth-form"
              aria-busy={busy}
            >
              <fieldset disabled={busy || status === 'loading'}>
                {!challenge ? (
                  <TextInput
                    label="Work email"
                    name="email"
                    type="email"
                    autoComplete="username"
                    required
                    maxLength={320}
                    placeholder="name@company.com"
                  />
                ) : (
                  <>
                    <p className="auth-account-email">{email}</p>
                    <TextInput
                      label="SMS verification code"
                      name="code"
                      className="otp-input"
                      inputMode="numeric"
                      autoComplete="one-time-code"
                      pattern="[0-9]{6}"
                      minLength={6}
                      maxLength={6}
                      required
                    />
                    <PasswordInput
                      label="New password"
                      name="password"
                      autoComplete="new-password"
                      maxLength={1024}
                      required
                    />
                    <PasswordInput
                      label="Confirm new password"
                      name="confirmPassword"
                      autoComplete="new-password"
                      maxLength={1024}
                      required
                    />
                    {time.expired && (
                      <p className="auth-message auth-message--warning" role="status">
                        This code has expired. Request a new code.
                      </p>
                    )}
                  </>
                )}
                <Button className="auth-submit" type="submit" disabled={time.expired}>
                  {busy ? <LoaderCircle size={18} className="spin" /> : <ArrowRight size={18} />}
                  {busy ? 'Please wait...' : challenge ? 'Reset password' : 'Request recovery code'}
                </Button>
                {challenge && (
                  <Button
                    variant="ghost"
                    className="auth-submit"
                    disabled={time.resendIn > 0}
                    onClick={() =>
                      void run(async () => {
                        setChallenge(await authAdapter.resendReset(challenge.challengeId, email));
                      })
                    }
                  >
                    {time.resendIn > 0 ? `Resend code in ${time.resendIn}s` : 'Resend code'}
                  </Button>
                )}
              </fieldset>
            </form>
            <Link className="auth-back text-link" to="/sign-in">
              <ArrowLeft size={15} /> Back to sign in
            </Link>
          </>
        )}
      </section>
    </AuthLayout>
  );
}

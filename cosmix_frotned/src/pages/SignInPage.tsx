import { useRef, useState, type FormEvent } from 'react';
import { Link, Navigate } from 'react-router-dom';
import { ArrowLeft, ArrowRight, LoaderCircle, ShieldCheck } from 'lucide-react';
import { AuthLayout } from '@/layouts/AuthLayout';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { PasswordInput } from '@/components/forms/PasswordInput';
import { useAuth } from '@/auth/use-auth';
import { authAdapter } from '@/auth/adapter';
import { authErrorMessage } from '@/auth/messages';
import { useChallengeTime } from '@/auth/ChallengeStatus';
import { type OtpChallenge, isSystemAdmin } from '@/auth/contracts';
import { LocalTestOtp } from '@/auth/LocalTestOtp';
import logo from '@/assets/images/cosmix-logo.png';

export function SignInPage() {
  const { user, status, sessionError, completeSignIn, retrySession } = useAuth();
  const [email, setEmail] = useState('');
  const [challenge, setChallenge] = useState<OtpChallenge | null>(null);
  const [resending, setResending] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [verified, setVerified] = useState(false);
  const pending = useRef(false);
  const time = useChallengeTime(challenge);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (pending.current) return;
    pending.current = true;
    const form = event.currentTarget;
    const data = new FormData(form);
    setBusy(true);
    setError(null);
    try {
      if (!challenge) {
        const address = String(data.get('email')).trim();
        const result = await authAdapter.login(address, String(data.get('password')));
        setEmail(address);
        setChallenge(result);
      } else if (resending) {
        const result = await authAdapter.resend(
          challenge.challengeId,
          email,
          String(data.get('password')),
        );
        setChallenge(result);
        setResending(false);
      } else {
        await authAdapter.verify(challenge.challengeId, String(data.get('code')));
        setVerified(true);
        setChallenge(null);
        await completeSignIn();
      }
    } catch (failure) {
      setError(authErrorMessage(failure));
    } finally {
      // Clear credentials on success and failure; they are never persisted in React/query storage.
      form
        .querySelectorAll<HTMLInputElement>(
          'input[type="password"], input[name="password"], input[name="code"]',
        )
        .forEach((input) => {
          input.value = '';
        });
      pending.current = false;
      setBusy(false);
    }
  }

  const isAdmin = isSystemAdmin(user);
  if (status === 'authenticated') {
    return <Navigate to={isAdmin ? '/admin/dashboard' : '/hr/dashboard'} replace />;
  }
  const loading = status === 'loading';
  return (
    <AuthLayout variant="split">
      <section className="auth-content" aria-labelledby="sign-in-title">
        <div className="auth-form-brand">
          <img src={logo} alt="Cosmix Engineering" width="192" height="80" />
        </div>
        <h1 id="sign-in-title">
          {verified
            ? loading
              ? 'Finishing sign in'
              : 'Sign-in interrupted'
            : challenge
              ? 'Verify your identity'
              : 'Sign in'}
        </h1>
        <p className="auth-subtitle">
          {verified
            ? loading
              ? 'Loading your account.'
              : 'Your account session could not be confirmed.'
            : challenge
              ? 'If your details are eligible, a code will arrive on your registered mobile number.'
              : 'Welcome back to Cosmix Engineering.'}
        </p>
        {(error || (!challenge && sessionError)) && (
          <div className="auth-message auth-message--error" role="alert">
            {error || sessionError}
          </div>
        )}
        {verified ? (
          <>
            <Button className="auth-submit" disabled={loading} onClick={() => void retrySession()}>
              <LoaderCircle size={17} className={loading ? 'spin' : undefined} />
              {loading ? 'Loading account...' : 'Retry account access'}
            </Button>
            <Button
              variant="ghost"
              className="auth-back"
              disabled={loading}
              onClick={() => {
                setVerified(false);
                setChallenge(null);
                setResending(false);
                setError(null);
              }}
            >
              <ArrowLeft size={15} /> Back to sign in
            </Button>
          </>
        ) : (
          <form
            onSubmit={(event) => void submit(event)}
            className="auth-form"
            aria-busy={busy || loading}
          >
            <fieldset disabled={busy || loading}>
              {!challenge ? (
                <>
                  <TextInput
                    label="Work email"
                    name="email"
                    type="email"
                    autoComplete="username"
                    required
                    maxLength={320}
                    defaultValue={email}
                    placeholder="name@company.com"
                  />
                  <PasswordInput
                    label="Password"
                    name="password"
                    autoComplete="current-password"
                    required
                    maxLength={1024}
                  />
                  <div className="auth-form-link">
                    <Link className="text-link" to="/forgot-password">
                      Forgot password?
                    </Link>
                  </div>
                </>
              ) : resending ? (
                <>
                  <p className="auth-account-email">{email}</p>
                  <PasswordInput
                    label="Confirm password"
                    name="password"
                    autoComplete="current-password"
                    required
                    maxLength={1024}
                  />
                </>
              ) : (
                <>
                  <p className="auth-account-email">{email}</p>
                  <TextInput
                    key={`otp-input:${challenge.challengeId}`}
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
                  {time.expired && (
                    <p className="auth-message auth-message--warning" role="status">
                      This code has expired. Request a new code.
                    </p>
                  )}
                  {import.meta.env.DEV && !time.expired && (
                    <LocalTestOtp
                      key={`local-otp:${challenge.challengeId}`}
                      challengeId={challenge.challengeId}
                    />
                  )}
                </>
              )}
              <Button
                type="submit"
                className="auth-submit"
                disabled={Boolean(challenge && !resending && time.expired)}
              >
                {busy || loading ? (
                  <LoaderCircle size={18} className="spin" />
                ) : (
                  <ArrowRight size={18} />
                )}
                {loading
                  ? 'Checking session...'
                  : busy
                    ? 'Please wait...'
                    : !challenge
                      ? 'Continue'
                      : resending
                        ? 'Send new code'
                        : 'Verify and sign in'}
              </Button>
              {challenge && (
                <div className="auth-secondary-actions">
                  {!resending && (
                    <Button
                      variant="ghost"
                      disabled={time.resendIn > 0}
                      onClick={() => {
                        setResending(true);
                        setError(null);
                      }}
                    >
                      {time.resendIn > 0 ? `Resend code in ${time.resendIn}s` : 'Resend code'}
                    </Button>
                  )}
                  <Button
                    variant="ghost"
                    onClick={() => {
                      setChallenge(null);
                      setResending(false);
                      setError(null);
                    }}
                  >
                    <ArrowLeft size={15} /> Back to sign in
                  </Button>
                </div>
              )}
            </fieldset>
          </form>
        )}
        <div className="auth-footnote">
          <ShieldCheck size={17} aria-hidden="true" />
          <span>Company account access</span>
        </div>
      </section>
    </AuthLayout>
  );
}

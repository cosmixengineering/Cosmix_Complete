import { useCallback, useEffect, useRef, useState } from 'react';
import { z } from 'zod';
import { Eye, LoaderCircle, CheckCircle2, RotateCw } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { request } from '@/services/api/http-client';
import { ApiError } from '@/services/api/api-error';

function otpError(error: unknown) {
  if (error instanceof DOMException && error.name === 'TimeoutError')
    return 'The local API did not return the test OTP within 30 seconds. Check the API debugger and OTP delivery worker before retrying.';
  if (error instanceof z.ZodError)
    return 'The local OTP service returned an unexpected response. Check that the frontend and API versions match.';
  if (error instanceof ApiError) {
    const reference = error.correlationId ? ' Reference: ' + error.correlationId : '';
    if (error.status === 404)
      return (
        'No test OTP is available for this login request. Check your credentials and request a fresh code after the resend countdown.' +
        reference
      );
    if (error.status === 429)
      return 'Too many requests. Wait before fetching or requesting another code.' + reference;
    if (error.status === 400 || error.status === 403)
      return (
        'The test OTP request was rejected. Restart sign-in to renew the browser context.' +
        reference
      );
    if (error.status >= 500)
      return (
        'The local OTP service is unavailable. Check the running API and try again.' + reference
      );
  }
  return 'Could not fetch the test OTP. Check that the local API is running, then try again.';
}

export function LocalTestOtp({
  challengeId,
  onAutoVerify,
  onReset,
  onCode,
  expired = false,
  resendIn = 0,
}: {
  challengeId: string;
  onAutoVerify?: (code: string) => void;
  onReset?: () => void;
  onCode?: (code: string) => void;
  expired?: boolean;
  resendIn?: number;
}) {
  const [available, setAvailable] = useState(false);
  const [code, setCode] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const pending = useRef<AbortController | null>(null);
  const codeHandler = useRef(onCode);
  useEffect(() => {
    codeHandler.current = onCode;
  }, [onCode]);

  useEffect(() => {
    if (!import.meta.env.DEV) return;
    const controller = new AbortController();
    void request('/auth/web/local-testing', { anonymous: true, signal: controller.signal })
      .then((value) => {
        if (!controller.signal.aborted)
          setAvailable(
            z
              .object({ enabled: z.literal(true), database: z.literal('Cosmix_Testing') })
              .safeParse(value).success,
          );
      })
      .catch(() => {});
    return () => controller.abort();
  }, []);

  const reveal = useCallback(async () => {
    if (!available || expired || pending.current) return;
    const controller = new AbortController();
    pending.current = controller;
    setBusy(true);
    setError(null);
    setCode(null);
    codeHandler.current?.('');
    try {
      const result = await request('/auth/web/local-testing/otp', {
        method: 'POST',
        anonymous: true,
        body: { challengeId },
        signal: controller.signal,
      });
      const parsed = z.object({ code: z.string().regex(/^\d{6}$/) }).parse(result);
      if (!controller.signal.aborted) {
        setCode(parsed.code);
        codeHandler.current?.(parsed.code);
      }
    } catch (failure) {
      if (!controller.signal.aborted) setError(otpError(failure));
    } finally {
      if (pending.current === controller) pending.current = null;
      if (!controller.signal.aborted) setBusy(false);
    }
  }, [available, challengeId, expired]);

  useEffect(() => {
    if (expired) {
      setBusy(false);
      setCode(null);
      codeHandler.current?.('');
    }
    void reveal();
    return () => {
      pending.current?.abort();
      pending.current = null;
    };
  }, [reveal, expired]);

  if (!import.meta.env.DEV || !available) return null;
  return (
    <section className="auth-message auth-message--warning" aria-label="Local testing OTP">
      <strong>Local testing OTP</strong>
      <p>This code is from the local test inbox; no SMS provider is used.</p>
      <Button variant="ghost" disabled={busy || expired} onClick={() => void reveal()}>
        {busy ? <LoaderCircle size={14} className="spin" /> : <Eye size={14} />}
        {busy ? 'Fetching test code...' : 'Fetch test OTP'}
      </Button>
      {code && !expired && (
        <div>
          <p role="status">
            <CheckCircle2 size={14} /> Test code: <strong>{code}</strong>
          </p>
          {onAutoVerify && (
            <Button onClick={() => onAutoVerify(code)}>Verify test code and sign in</Button>
          )}
        </div>
      )}
      {(error || expired) && (
        <div>
          <p role="alert">
            {expired ? 'This login request has expired. Request a fresh code.' : error}
          </p>
          {onReset && (
            <Button variant="secondary" disabled={busy || resendIn > 0} onClick={onReset}>
              <RotateCw size={13} />{' '}
              {resendIn > 0 ? 'Request a new code in ' + resendIn + 's' : 'Request a fresh code'}
            </Button>
          )}
        </div>
      )}
    </section>
  );
}

import { useEffect, useState } from 'react';
import { z } from 'zod';
import { Eye, LoaderCircle, CheckCircle2, RotateCw } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { request } from '@/services/api/http-client';

export function LocalTestOtp({
  challengeId,
  onAutoVerify,
  onReset,
}: {
  challengeId: string;
  onAutoVerify?: (code: string) => void;
  onReset?: () => void;
}) {
  const [available, setAvailable] = useState(false);
  const [code, setCode] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(false);

  useEffect(() => {
    if (!import.meta.env.DEV) return;
    const controller = new AbortController();
    void request('/auth/web/local-testing', { anonymous: true, signal: controller.signal })
      .then((value) => {
        if (!controller.signal.aborted) {
          const isDev = z
            .object({ enabled: z.literal(true), database: z.literal('Cosmix_Testing') })
            .safeParse(value).success;
          setAvailable(isDev);
        }
      })
      .catch(() => {});
    return () => controller.abort();
  }, []);

  async function reveal() {
    if (busy || !challengeId) return;
    setBusy(true);
    setError(false);
    setCode(null);
    for (let attempt = 0; attempt < 5; attempt++) {
      try {
        const result = await request('/auth/web/local-testing/otp', {
          method: 'POST',
          anonymous: true,
          body: { challengeId },
        });
        const parsed = z.object({ code: z.string().regex(/^\d{6}$/) }).parse(result);
        setCode(parsed.code);
        const codeInput = document.querySelector<HTMLInputElement>('input[name="code"]');
        if (codeInput) {
          codeInput.value = parsed.code;
          codeInput.dispatchEvent(new Event('input', { bubbles: true }));
          codeInput.dispatchEvent(new Event('change', { bubbles: true }));
        }
        setBusy(false);
        return parsed.code;
      } catch {
        if (attempt < 4) {
          await new Promise((r) => setTimeout(r, 400));
        }
      }
    }
    setError(true);
    setBusy(false);
    return null;
  }

  // Auto-fetch test OTP code as soon as endpoint is confirmed available
  useEffect(() => {
    if (available && challengeId && !code && !busy && !error) {
      void reveal();
    }
  }, [available, challengeId]);

  if (!import.meta.env.DEV || !available) return null;

  return (
    <div
      className="auth-message auth-message--warning"
      style={{
        border: '1px solid #fde047',
        background: '#fefce8',
        padding: '0.625rem 0.75rem',
        borderRadius: '6px',
        margin: '0.75rem 0',
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '0.5rem' }}>
        <strong style={{ fontSize: '0.8125rem', color: '#854d0e' }}>Local Testing OTP</strong>
        <Button
          variant="ghost"
          disabled={busy}
          onClick={() => void reveal()}
          style={{ fontSize: '0.75rem', padding: '0.2rem 0.5rem', height: 'auto' }}
        >
          {busy ? <LoaderCircle size={14} className="spin" /> : <Eye size={14} />}
          <span>{busy ? 'Fetching OTP...' : code ? 'Re-check OTP' : 'Fetch test OTP'}</span>
        </Button>
      </div>

      {code && (
        <div style={{ marginTop: '0.4rem', display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '0.4rem' }}>
          <p role="status" style={{ margin: 0, fontSize: '0.8125rem', color: '#166534' }}>
            <CheckCircle2 size={15} style={{ display: 'inline', verticalAlign: '-2px', marginRight: 4 }} />
            OTP Auto-filled: <strong style={{ fontSize: '0.95rem', letterSpacing: '0.1em' }}>{code}</strong>
          </p>
          {onAutoVerify && (
            <Button
              type="button"
              variant="primary"
              onClick={() => onAutoVerify(code)}
              style={{ fontSize: '0.75rem', padding: '0.25rem 0.6rem', height: 'auto' }}
            >
              ⚡ Instant Verify & Sign In
            </Button>
          )}
        </div>
      )}

      {error && (
        <div style={{ marginTop: '0.4rem' }}>
          <p role="status" style={{ margin: '0 0 0.4rem 0', fontSize: '0.75rem', color: '#b91c1c' }}>
            No valid OTP found for this challenge (it may have expired or timed out).
          </p>
          {onReset && (
            <Button
              type="button"
              variant="secondary"
              onClick={onReset}
              style={{
                fontSize: '0.75rem',
                padding: '0.25rem 0.6rem',
                height: 'auto',
                background: '#fff',
                borderColor: '#cbd5e1',
                color: '#1e293b',
              }}
            >
              <RotateCw size={13} style={{ marginRight: 4 }} />
              Request Fresh Code / 1-Click Sign In
            </Button>
          )}
        </div>
      )}
    </div>
  );
}

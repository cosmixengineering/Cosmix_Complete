import { useEffect, useState } from 'react';
import { z } from 'zod';
import { Eye, LoaderCircle } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { request } from '@/services/api/http-client';

export function LocalTestOtp({ challengeId }: { challengeId: string }) {
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
          setAvailable(
            z
              .object({ enabled: z.literal(true), database: z.literal('Cosmix_Testing') })
              .safeParse(value).success,
          );
        }
      })
      .catch(() => {});
    return () => controller.abort();
  }, []);

  async function reveal() {
    if (busy) return;
    setBusy(true);
    setError(false);
    setCode(null);
    try {
      const result = await request('/auth/web/local-testing/otp', {
        method: 'POST',
        anonymous: true,
        body: { challengeId },
      });
      setCode(z.object({ code: z.string().regex(/^\d{6}$/) }).parse(result).code);
    } catch {
      setError(true);
    } finally {
      setBusy(false);
    }
  }

  if (!import.meta.env.DEV || !available) return null;
  return (
    <div className="auth-message auth-message--warning">
      <strong>Local testing only</strong>
      <Button variant="ghost" disabled={busy} onClick={() => void reveal()}>
        {busy ? <LoaderCircle size={16} className="spin" /> : <Eye size={16} />}
        Show test OTP
      </Button>
      {code && (
        <p role="status">
          Test code: <strong>{code}</strong>
        </p>
      )}
      {error && (
        <p role="status">
          No test code is available for this sign-in. Check your dummy-account email and password,
          or request a fresh code after the cooldown.
        </p>
      )}
    </div>
  );
}

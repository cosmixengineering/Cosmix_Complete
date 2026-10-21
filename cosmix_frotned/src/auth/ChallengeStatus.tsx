import { useEffect, useState } from 'react';
import type { OtpChallenge } from './contracts';

export function useChallengeTime(challenge: OtpChallenge | null) {
  const [now, setNow] = useState(Date.now);
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, []);
  return {
    expired: Boolean(challenge && Date.parse(challenge.expiresAt) <= now),
    resendIn: challenge
      ? Math.max(0, Math.ceil((Date.parse(challenge.resendAvailableAt) - now) / 1000))
      : 0,
  };
}

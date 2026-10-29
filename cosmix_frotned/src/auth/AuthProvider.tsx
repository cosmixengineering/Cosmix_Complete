import { useCallback, useEffect, useRef, useState, type PropsWithChildren } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { AuthContext } from './context';
import { authAdapter } from './adapter';
import { authErrorMessage } from './messages';
import { allowSessionRefresh, blockSessionRefresh, getCsrf } from '@/services/api/http-client';
import type { SessionStatus, SessionUser } from './types';
import { SESSION_EXPIRED_EVENT, SESSION_UNAVAILABLE_EVENT } from '@/constants/events';

const MIN_PRELOADER_MS = 2000;

export function AuthProvider({ children }: PropsWithChildren) {
  const queryClient = useQueryClient();
  const [user, setUser] = useState<SessionUser | null>(null);
  const [status, setStatus] = useState<SessionStatus>('loading');
  const [sessionError, setSessionError] = useState<string | null>(null);
  const revision = useRef(0);
  const hadSession = useRef(false);
  const channel = useRef<BroadcastChannel | null>(null);
  const clearCache = useCallback(async () => {
    await queryClient.cancelQueries();
    queryClient.clear();
  }, [queryClient]);
  const loadSession = useCallback(async () => {
    const version = ++revision.current;
    const startTime = Date.now();
    setUser(null);
    setStatus('loading');
    setSessionError(null);
    await clearCache();
    try {
      const session = await authAdapter.restoreSession(true);
      const elapsed = Date.now() - startTime;
      const remaining = Math.max(0, MIN_PRELOADER_MS - elapsed);
      if (remaining > 0) {
        await new Promise((resolve) => setTimeout(resolve, remaining));
      }
      if (version !== revision.current) return;
      setUser(session);
      hadSession.current = session !== null;
      setStatus(session ? 'authenticated' : 'anonymous');
    } catch (error) {
      const elapsed = Date.now() - startTime;
      const remaining = Math.max(0, MIN_PRELOADER_MS - elapsed);
      if (remaining > 0) {
        await new Promise((resolve) => setTimeout(resolve, remaining));
      }
      if (version !== revision.current) return;
      setStatus('unavailable');
      setSessionError(authErrorMessage(error));
    }
  }, [clearCache]);
  useEffect(() => {
    let active = true;
    const version = ++revision.current;
    const startTime = Date.now();
    void authAdapter
      .restoreSession()
      .then(async (session) => {
        const elapsed = Date.now() - startTime;
        const remaining = Math.max(0, MIN_PRELOADER_MS - elapsed);
        if (remaining > 0) {
          await new Promise((resolve) => setTimeout(resolve, remaining));
        }
        if (!active || version !== revision.current) return;
        setUser(session);
        hadSession.current = session !== null;
        setStatus(session ? 'authenticated' : 'anonymous');
      })
      .catch(async (error) => {
        const elapsed = Date.now() - startTime;
        const remaining = Math.max(0, MIN_PRELOADER_MS - elapsed);
        if (remaining > 0) {
          await new Promise((resolve) => setTimeout(resolve, remaining));
        }
        if (!active || version !== revision.current) return;
        setStatus('unavailable');
        setSessionError(authErrorMessage(error));
      });
    const expire = () => {
      ++revision.current;
      setUser(null);
      setStatus('anonymous');
      setSessionError(hadSession.current ? 'Your session has ended. Please sign in again.' : null);
      hadSession.current = false;
      void clearCache();
    };
    const unavailable = () => {
      ++revision.current;
      setUser(null);
      setStatus('unavailable');
      setSessionError('Sign-in is temporarily unavailable. Please try again later.');
      hadSession.current = false;
      void clearCache();
    };
    const bus =
      typeof BroadcastChannel !== 'undefined' ? new BroadcastChannel('cosmix-web-auth') : null;
    channel.current = bus;
    if (bus)
      bus.onmessage = (event: MessageEvent<unknown>) => {
        if (event.data === 'signed-out') {
          blockSessionRefresh();
          expire();
        }
        if (event.data === 'signed-in') {
          allowSessionRefresh();
          void loadSession();
        }
      };
    window.addEventListener(SESSION_EXPIRED_EVENT, expire);
    window.addEventListener(SESSION_UNAVAILABLE_EVENT, unavailable);
    return () => {
      active = false;
      window.removeEventListener(SESSION_EXPIRED_EVENT, expire);
      window.removeEventListener(SESSION_UNAVAILABLE_EVENT, unavailable);
      bus?.close();
      channel.current = null;
    };
  }, [loadSession, clearCache]);
  async function completeSignIn() {
    channel.current?.postMessage('signed-in');
    // OTP is already consumed. A subsequent session-read failure must not replay it.
    await getCsrf().catch(() => undefined);
    await loadSession();
  }
  async function passwordResetCompleted() {
    hadSession.current = false;
    ++revision.current;
    setUser(null);
    setStatus('anonymous');
    setSessionError(null);
    await clearCache();
    channel.current?.postMessage('signed-out');
  }
  async function signOut() {
    await authAdapter.signOut();
    hadSession.current = false;
    ++revision.current;
    setUser(null);
    setStatus('anonymous');
    setSessionError(null);
    await clearCache();
    channel.current?.postMessage('signed-out');
  }
  return (
    <AuthContext.Provider
      value={{
        user,
        status,
        sessionError,
        completeSignIn,
        passwordResetCompleted,
        retrySession: loadSession,
        signOut,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

import { createContext } from 'react';
import type { SessionStatus, SessionUser } from './types';
export interface AuthContextValue {
  status: SessionStatus;
  user: SessionUser | null;
  sessionError: string | null;
  completeSignIn: () => Promise<void>;
  passwordResetCompleted: () => Promise<void>;
  retrySession: () => Promise<void>;
  signOut: () => Promise<void>;
}
export const AuthContext = createContext<AuthContextValue | null>(null);

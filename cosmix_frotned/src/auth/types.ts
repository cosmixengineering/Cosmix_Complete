import type { CurrentAccount, PermissionGrant } from './contracts';

export interface SessionUser {
  account: CurrentAccount | null;
  grants: readonly PermissionGrant[];
}
export type SessionStatus = 'loading' | 'anonymous' | 'authenticated' | 'unavailable';

import { z } from 'zod';
import { request } from '@/services/api/http-client';
import {
  accessRoleSchema,
  accessRolePageSchema,
  accessPermissionSchema,
  accessAccountSchema,
  accessAssignmentPageSchema,
  initialAccessResultSchema,
  type AccessRole,
  type AccessPermission,
  type AccessAccount,
  type AdditionalRoleAssignmentRequest,
} from '@/modules/hr/schemas/access-setup';

export interface CreateRolePayload {
  code: string;
  name: string;
}

export interface GrantPermissionsPayload {
  permissions: string[];
  expectedRoleVersion: string;
  reason: string;
}

export interface RevokeAssignmentPayload {
  reason: string;
  expectedAccountVersion: string;
  expectedAssignmentVersion: string;
}

export async function fetchRoles(page = 1, pageSize = 50) {
  const res = await request(`/access/roles?page=${page}&pageSize=${pageSize}`);
  return accessRolePageSchema.parse(res);
}

export async function fetchAllRoles() {
  const all: AccessRole[] = [];
  let page = 1;
  const pageSize = 100;
  while (page <= 20) {
    const res = await fetchRoles(page, pageSize);
    all.push(...res.items);
    if (all.length >= res.totalCount || res.items.length === 0) break;
    page++;
  }
  return all;
}

export async function fetchCatalog(): Promise<AccessPermission[]> {
  const res = await request('/access/permissions');
  return z.array(accessPermissionSchema).parse(res);
}

export async function createRole(payload: CreateRolePayload): Promise<AccessRole> {
  const key = crypto.randomUUID();
  const res = await request('/access/roles', {
    method: 'POST',
    body: {
      code: payload.code.trim(),
      name: payload.name.trim(),
    },
    idempotencyKey: key,
  });
  return accessRoleSchema.parse(res);
}

/**
 * Grants permissions to a role. Handles chunking if more than 10 permissions
 * are selected, since backend accepts max 10 per request.
 */
export async function grantPermissionsToRole(
  roleId: string,
  permissions: string[],
  initialRoleVersion: string,
  reason: string,
  onProgress?: (granted: number, total: number) => void,
): Promise<AccessRole> {
  if (permissions.length === 0) {
    throw new Error('Select at least one permission to grant.');
  }

  let currentVersion = initialRoleVersion;
  let latestRole: AccessRole | null = null;
  const chunkSize = 10;
  const chunks: string[][] = [];

  for (let i = 0; i < permissions.length; i += chunkSize) {
    chunks.push(permissions.slice(i, i + chunkSize));
  }

  let grantedSoFar = 0;
  for (const chunk of chunks) {
    const key = crypto.randomUUID();
    const res = await request(`/access/roles/${roleId}/permissions`, {
      method: 'POST',
      body: {
        permissions: chunk,
        expectedRoleVersion: currentVersion,
        reason: reason.trim(),
      },
      idempotencyKey: key,
    });
    latestRole = accessRoleSchema.parse(res);
    currentVersion = latestRole.version;
    grantedSoFar += chunk.length;
    onProgress?.(grantedSoFar, permissions.length);
  }

  if (!latestRole) {
    throw new Error('Failed to grant role permissions.');
  }

  return latestRole;
}

export async function fetchAccount(accountId: string): Promise<AccessAccount> {
  const res = await request(`/access/accounts/${accountId}`);
  return accessAccountSchema.parse(res);
}

export async function fetchAssignments(accountId: string, page = 1, pageSize = 50) {
  const res = await request(
    `/access/accounts/${accountId}/assignments?page=${page}&pageSize=${pageSize}`,
  );
  return accessAssignmentPageSchema.parse(res);
}

export async function assignRoleToAccount(
  accountId: string,
  payload: AdditionalRoleAssignmentRequest,
) {
  const key = crypto.randomUUID();
  const res = await request(`/access/accounts/${accountId}/assignments`, {
    method: 'POST',
    body: payload,
    idempotencyKey: key,
  });
  return initialAccessResultSchema.parse(res);
}

export async function revokeAccountAssignment(
  accountId: string,
  assignmentId: string,
  payload: RevokeAssignmentPayload,
): Promise<AccessAccount> {
  const res = await request(`/access/accounts/${accountId}/assignments/${assignmentId}/revoke`, {
    method: 'POST',
    body: {
      reason: payload.reason.trim(),
      expectedAccountVersion: payload.expectedAccountVersion,
      expectedAssignmentVersion: payload.expectedAssignmentVersion,
    },
  });
  return accessAccountSchema.parse(res);
}

export async function setAccountMobileAccess(
  accountId: string,
  enabled: boolean,
  expectedAccountVersion: string,
): Promise<AccessAccount> {
  const res = await request(`/access/accounts/${accountId}/mobile-access`, {
    method: 'PUT',
    body: {
      enabled,
      expectedAccountVersion,
    },
  });
  return accessAccountSchema.parse(res);
}

export async function setAccountWebAccess(
  accountId: string,
  enabled: boolean,
  expectedAccountVersion: string,
): Promise<AccessAccount> {
  const res = await request(`/access/accounts/${accountId}/web-access`, {
    method: 'PUT',
    body: {
      enabled,
      expectedAccountVersion,
    },
  });
  return accessAccountSchema.parse(res);
}

import { useQuery, keepPreviousData } from '@tanstack/react-query'
import { getRoles, getRoleById, getAllPermissions, getPermissionTemplates } from '@/services/roles'
import { getUsers, getUserById, getUserRoles } from '@/services/users'
import { getTenants, getTenant, type GetTenantsParams } from '@/services/tenants'
import { roleKeys, userKeys, tenantKeys, type RolesParams, type UsersParams } from './queryKeys'

export const useRolesQuery = (params: RolesParams = {}) =>
  useQuery({
    queryKey: roleKeys.list(params),
    queryFn: () => getRoles(params),
    placeholderData: keepPreviousData,
  })

export const useRoleDetailQuery = (id: string | undefined, enabled: boolean) =>
  useQuery({
    queryKey: roleKeys.detail(id!),
    queryFn: () => getRoleById(id!),
    enabled: enabled && !!id,
  })

export const usePermissionsQuery = () =>
  useQuery({
    queryKey: roleKeys.permissions(),
    queryFn: () => getAllPermissions(),
  })

export const usePermissionTemplatesQuery = () =>
  useQuery({
    queryKey: roleKeys.permissionTemplates(),
    queryFn: () => getPermissionTemplates(),
  })

export const useUsersQuery = (params: UsersParams = {}) =>
  useQuery({
    queryKey: userKeys.list(params),
    queryFn: () => getUsers(params),
    placeholderData: keepPreviousData,
  })

export const useAvailableRolesQuery = () =>
  useQuery({
    queryKey: userKeys.availableRoles(),
    queryFn: () => getRoles({ pageSize: 100 }),
    select: (data) => data.items,
  })

export const useUserDetailQuery = (id: string | undefined, enabled: boolean) =>
  useQuery({
    queryKey: userKeys.detail(id!),
    queryFn: () => getUserById(id!),
    enabled: enabled && !!id,
  })

export const useUserRolesQuery = (userId: string | undefined, enabled: boolean) =>
  useQuery({
    queryKey: userKeys.roles(userId!),
    queryFn: () => getUserRoles(userId!),
    enabled: enabled && !!userId,
  })

export const useTenantsQuery = (params: GetTenantsParams = {}) =>
  useQuery({
    queryKey: tenantKeys.list(params),
    queryFn: () => getTenants(params),
    placeholderData: keepPreviousData,
  })

export const useTenantDetailQuery = (id: string | undefined, enabled: boolean) =>
  useQuery({
    queryKey: tenantKeys.detail(id!),
    queryFn: () => getTenant(id!),
    enabled: enabled && !!id,
  })
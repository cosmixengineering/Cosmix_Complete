/**
 * Customers API Service
 *
 * Provides methods for managing customers, loyalty points, and addresses.
 */
import { apiClient } from './apiClient'
import { downloadFileExport } from '@/lib/fileExport'
import type {
  CustomerDto,
  CustomerPagedResult,
  CustomerStatsDto,
  CustomerAddressDto,
  CustomerSegment,
  CustomerTier,
  CreateCustomerRequest,
  UpdateCustomerRequest,
  CreateCustomerAddressRequest,
  UpdateCustomerAddressRequest,
  OrderPagedResultForCustomer,
  LoyaltyPointsRequest,
  UpdateCustomerSegmentRequest,
} from '@/types/customer'

export interface GetCustomersParams {
  page?: number
  pageSize?: number
  search?: string
  segment?: CustomerSegment
  tier?: CustomerTier
  isActive?: boolean
  orderBy?: string
  isDescending?: boolean
}

export interface GetCustomerOrdersParams {
  page?: number
  pageSize?: number
}

export const getCustomers = async (params: GetCustomersParams = {}): Promise<CustomerPagedResult> => {
  const queryParams = new URLSearchParams()
  if (params.page != null) queryParams.append('page', params.page.toString())
  if (params.pageSize != null) queryParams.append('pageSize', params.pageSize.toString())
  if (params.search) queryParams.append('search', params.search)
  if (params.segment) queryParams.append('segment', params.segment)
  if (params.tier) queryParams.append('tier', params.tier)
  if (params.isActive != null) queryParams.append('isActive', params.isActive.toString())
  if (params.orderBy) queryParams.append('orderBy', params.orderBy)
  if (params.isDescending != null) queryParams.append('isDescending', params.isDescending.toString())

  const query = queryParams.toString()
  return apiClient<CustomerPagedResult>(`/customers${query ? `?${query}` : ''}`)
}

export const getCustomerById = async (id: string): Promise<CustomerDto> => {
  return apiClient<CustomerDto>(`/customers/${id}`)
}

export const getCustomerStats = async (topSpendersCount?: number): Promise<CustomerStatsDto> => {
  const queryParams = new URLSearchParams()
  if (topSpendersCount != null) queryParams.append('topSpendersCount', topSpendersCount.toString())
  const query = queryParams.toString()
  return apiClient<CustomerStatsDto>(`/customers/stats${query ? `?${query}` : ''}`)
}

export const getCustomerOrders = async (id: string, params: GetCustomerOrdersParams = {}): Promise<OrderPagedResultForCustomer> => {
  const queryParams = new URLSearchParams()
  if (params.page != null) queryParams.append('page', params.page.toString())
  if (params.pageSize != null) queryParams.append('pageSize', params.pageSize.toString())
  const query = queryParams.toString()
  return apiClient<OrderPagedResultForCustomer>(`/customers/${id}/orders${query ? `?${query}` : ''}`)
}

export const createCustomer = async (request: CreateCustomerRequest): Promise<CustomerDto> => {
  return apiClient<CustomerDto>('/customers', {
    method: 'POST',
    body: JSON.stringify(request),
  })
}

export const updateCustomer = async (id: string, request: UpdateCustomerRequest): Promise<CustomerDto> => {
  return apiClient<CustomerDto>(`/customers/${id}`, {
    method: 'PUT',
    body: JSON.stringify(request),
  })
}

export const deleteCustomer = async (id: string): Promise<CustomerDto> => {
  return apiClient<CustomerDto>(`/customers/${id}`, {
    method: 'DELETE',
  })
}

export const updateCustomerSegment = async (id: string, request: UpdateCustomerSegmentRequest): Promise<CustomerDto> => {
  return apiClient<CustomerDto>(`/customers/${id}/segment`, {
    method: 'PUT',
    body: JSON.stringify(request),
  })
}

export const addLoyaltyPoints = async (id: string, request: LoyaltyPointsRequest): Promise<CustomerDto> => {
  return apiClient<CustomerDto>(`/customers/${id}/loyalty/add`, {
    method: 'POST',
    body: JSON.stringify(request),
  })
}

export const redeemLoyaltyPoints = async (id: string, request: LoyaltyPointsRequest): Promise<CustomerDto> => {
  return apiClient<CustomerDto>(`/customers/${id}/loyalty/redeem`, {
    method: 'POST',
    body: JSON.stringify(request),
  })
}

export const addCustomerAddress = async (customerId: string, request: CreateCustomerAddressRequest): Promise<CustomerAddressDto> => {
  return apiClient<CustomerAddressDto>(`/customers/${customerId}/addresses`, {
    method: 'POST',
    body: JSON.stringify(request),
  })
}

export const updateCustomerAddress = async (customerId: string, addressId: string, request: UpdateCustomerAddressRequest): Promise<CustomerAddressDto> => {
  return apiClient<CustomerAddressDto>(`/customers/${customerId}/addresses/${addressId}`, {
    method: 'PUT',
    body: JSON.stringify(request),
  })
}

export const deleteCustomerAddress = async (customerId: string, addressId: string): Promise<CustomerAddressDto> => {
  return apiClient<CustomerAddressDto>(`/customers/${customerId}/addresses/${addressId}`, {
    method: 'DELETE',
  })
}

// ─── Bulk Operations ────────────────────────────────────────────────────

export interface CustomerBulkOperationResult {
  success: number
  failed: number
  errors: { entityId: string; entityName: string | null; message: string }[]
}

export const bulkActivateCustomers = async (customerIds: string[]): Promise<CustomerBulkOperationResult> => {
  return apiClient<CustomerBulkOperationResult>('/customers/bulk-activate', {
    method: 'POST',
    body: JSON.stringify({ customerIds }),
  })
}

export const bulkDeactivateCustomers = async (customerIds: string[]): Promise<CustomerBulkOperationResult> => {
  return apiClient<CustomerBulkOperationResult>('/customers/bulk-deactivate', {
    method: 'POST',
    body: JSON.stringify({ customerIds }),
  })
}

export const bulkDeleteCustomers = async (customerIds: string[]): Promise<CustomerBulkOperationResult> => {
  return apiClient<CustomerBulkOperationResult>('/customers/bulk-delete', {
    method: 'POST',
    body: JSON.stringify({ customerIds }),
  })
}

// ─── Import / Export ────────────────────────────────────────────────────

export const exportCustomers = async (params?: {
  format?: 'CSV' | 'Excel'
  segment?: string
  tier?: string
  isActive?: boolean
  search?: string
}): Promise<void> => {
  const queryParams = new URLSearchParams()
  if (params?.format) queryParams.append('format', params.format)
  if (params?.segment) queryParams.append('segment', params.segment)
  if (params?.tier) queryParams.append('tier', params.tier)
  if (params?.isActive !== undefined) queryParams.append('isActive', String(params.isActive))
  if (params?.search) queryParams.append('search', params.search)
  const ext = params?.format === 'Excel' ? 'xlsx' : 'csv'
  await downloadFileExport(`/api/customers/export?${queryParams}`, `customers.${ext}`)
}

export interface ImportCustomerDto {
  email: string
  firstName: string
  lastName: string
  phone?: string
  tags?: string
}

export interface BulkImportCustomersResult {
  success: number
  failed: number
  errors: { row: number; message: string }[]
}

export const bulkImportCustomers = async (customers: ImportCustomerDto[]): Promise<BulkImportCustomersResult> =>
  apiClient<BulkImportCustomersResult>('/customers/import', {
    method: 'POST',
    body: JSON.stringify({ customers }),
  })

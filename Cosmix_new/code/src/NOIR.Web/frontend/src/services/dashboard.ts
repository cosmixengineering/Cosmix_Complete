/**
 * Dashboard API Service
 *
 * Provides methods for fetching dashboard metrics and KPI data.
 */
import { apiClient } from './apiClient'

// ─── Types ───────────────────────────────────────────────────────────────

export interface DashboardMetricsParams {
  topProducts?: number
  lowStockThreshold?: number
  recentOrders?: number
  salesDays?: number
}

export interface RevenueMetrics {
  totalRevenue: number
  revenueThisMonth: number
  revenueLastMonth: number
  revenueToday: number
  totalOrders: number
  ordersThisMonth: number
  ordersToday: number
  averageOrderValue: number
}

export interface OrderCounts {
  pending: number
  confirmed: number
  processing: number
  shipped: number
  delivered: number
  completed: number
  cancelled: number
  refunded: number
  returned: number
}

export interface TopSellingProduct {
  productId: string
  productName: string
  imageUrl?: string | null
  totalQuantitySold: number
  totalRevenue: number
}

export interface LowStockProduct {
  productId: string
  variantId: string
  productName: string
  variantName: string
  sku: string
  stockQuantity: number
  lowStockThreshold: number
}

export interface RecentOrder {
  orderId: string
  orderNumber: string
  customerEmail: string
  grandTotal: number
  status: string
  createdAt: string
}

export interface SalesOverTime {
  date: string
  revenue: number
  orderCount: number
}

export interface ProductDistribution {
  draft: number
  active: number
  archived: number
}

export interface DashboardMetricsDto {
  revenue: RevenueMetrics
  orderCounts: OrderCounts
  topSellingProducts: TopSellingProduct[]
  lowStockProducts: LowStockProduct[]
  recentOrders: RecentOrder[]
  salesOverTime: SalesOverTime[]
  productDistribution: ProductDistribution
}

// ─── Core Dashboard Types ────────────────────────────────────────────────

export interface CoreDashboardDto {
  quickActions: QuickActionCountsDto
  recentActivity: ActivityFeedItemDto[]
  systemHealth: SystemHealthDto | null
}

export interface QuickActionCountsDto {
  pendingOrders: number
  pendingReviews: number
  lowStockAlerts: number
  draftProducts: number
}

export interface ActivityFeedItemDto {
  type: string
  title: string
  description: string
  timestamp: string
  entityId: string | null
  entityUrl: string | null
  userEmail: string | null
  targetDisplayName: string | null
}

export interface SystemHealthDto {
  apiHealthy: boolean
  backgroundJobsQueued: number
  backgroundJobsFailed: number
  activeTenants: number
}

// ─── Blog Dashboard Types ────────────────────────────────────────────────

export interface BlogDashboardDto {
  totalPosts: number
  publishedPosts: number
  draftPosts: number
  archivedPosts: number
  pendingComments: number
  topPosts: TopPostDto[]
  publishingTrend: PublishingTrendDto[]
}

export interface TopPostDto {
  postId: string
  title: string
  imageUrl: string | null
  viewCount: number
}

export interface PublishingTrendDto {
  date: string
  postCount: number
}

// ─── Inventory Dashboard Types ───────────────────────────────────────────

export interface InventoryDashboardDto {
  lowStockAlerts: LowStockAlertDto[]
  recentReceipts: RecentReceiptDto[]
  valueSummary: InventoryValueSummaryDto
  stockMovementTrend: StockMovementTrendDto[]
}

export interface LowStockAlertDto {
  productId: string
  productName: string
  sku: string | null
  currentStock: number
  threshold: number
}

export interface RecentReceiptDto {
  receiptId: string
  receiptNumber: string
  type: string
  date: string
  itemCount: number
}

export interface InventoryValueSummaryDto {
  totalValue: number
  totalSku: number
  inStockSku: number
  outOfStockSku: number
}

export interface StockMovementTrendDto {
  date: string
  stockIn: number
  stockOut: number
}

// ─── API Functions ───────────────────────────────────────────────────────

export const getDashboardMetrics = async (
  params: DashboardMetricsParams = {}
): Promise<DashboardMetricsDto> => {
  const queryParams = new URLSearchParams()
  if (params.topProducts != null) queryParams.append('topProducts', params.topProducts.toString())
  if (params.lowStockThreshold != null) queryParams.append('lowStockThreshold', params.lowStockThreshold.toString())
  if (params.recentOrders != null) queryParams.append('recentOrders', params.recentOrders.toString())
  if (params.salesDays != null) queryParams.append('salesDays', params.salesDays.toString())

  const query = queryParams.toString()
  return apiClient<DashboardMetricsDto>(`/dashboard/metrics${query ? `?${query}` : ''}`)
}

export const getCoreDashboard = async (activityCount?: number): Promise<CoreDashboardDto> => {
  const params = new URLSearchParams()
  if (activityCount != null) params.append('activityCount', activityCount.toString())
  const query = params.toString()
  return apiClient<CoreDashboardDto>(`/dashboard/core${query ? `?${query}` : ''}`)
}

export const getEcommerceDashboard = async (
  params: DashboardMetricsParams = {}
): Promise<DashboardMetricsDto> => {
  const queryParams = new URLSearchParams()
  if (params.topProducts != null) queryParams.append('topProducts', params.topProducts.toString())
  if (params.lowStockThreshold != null) queryParams.append('lowStockThreshold', params.lowStockThreshold.toString())
  if (params.recentOrders != null) queryParams.append('recentOrders', params.recentOrders.toString())
  if (params.salesDays != null) queryParams.append('salesDays', params.salesDays.toString())
  const query = queryParams.toString()
  return apiClient<DashboardMetricsDto>(`/dashboard/ecommerce${query ? `?${query}` : ''}`)
}

export const getBlogDashboard = async (trendDays?: number): Promise<BlogDashboardDto> => {
  const params = new URLSearchParams()
  if (trendDays != null) params.append('trendDays', trendDays.toString())
  const query = params.toString()
  return apiClient<BlogDashboardDto>(`/dashboard/blog${query ? `?${query}` : ''}`)
}

export const getInventoryDashboard = async (
  lowStockThreshold?: number,
  recentReceipts?: number
): Promise<InventoryDashboardDto> => {
  const params = new URLSearchParams()
  if (lowStockThreshold != null) params.append('lowStockThreshold', lowStockThreshold.toString())
  if (recentReceipts != null) params.append('recentReceipts', recentReceipts.toString())
  const query = params.toString()
  return apiClient<InventoryDashboardDto>(`/dashboard/inventory${query ? `?${query}` : ''}`)
}

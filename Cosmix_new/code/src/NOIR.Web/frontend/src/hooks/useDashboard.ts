/**
 * Dashboard TanStack Query hooks
 *
 * Fetches dashboard metrics with caching and refetch support.
 */
import { useQuery } from '@tanstack/react-query'
import {
  getDashboardMetrics,
  getCoreDashboard,
  getEcommerceDashboard,
  getBlogDashboard,
  getInventoryDashboard,
  type DashboardMetricsParams,
} from '@/services/dashboard'

export const dashboardKeys = {
  all: ['dashboard'] as const,
  metrics: (params?: DashboardMetricsParams) => [...dashboardKeys.all, 'metrics', params] as const,
  core: () => [...dashboardKeys.all, 'core'] as const,
  ecommerce: (params?: DashboardMetricsParams) => [...dashboardKeys.all, 'ecommerce', params] as const,
  blog: (trendDays?: number) => [...dashboardKeys.all, 'blog', trendDays] as const,
  inventory: (threshold?: number) => [...dashboardKeys.all, 'inventory', threshold] as const,
}

export const useDashboardMetrics = (params: DashboardMetricsParams = {}) =>
  useQuery({
    queryKey: dashboardKeys.metrics(params),
    queryFn: () => getDashboardMetrics(params),
    staleTime: 60_000,
    refetchInterval: 5 * 60_000,
  })

export const useCoreDashboard = () =>
  useQuery({
    queryKey: dashboardKeys.core(),
    queryFn: () => getCoreDashboard(),
    staleTime: 60_000,
    refetchInterval: 2 * 60_000,
  })

export const useEcommerceDashboard = (params: DashboardMetricsParams = {}) =>
  useQuery({
    queryKey: dashboardKeys.ecommerce(params),
    queryFn: () => getEcommerceDashboard(params),
    staleTime: 60_000,
    refetchInterval: 5 * 60_000,
  })

export const useBlogDashboard = (trendDays?: number) =>
  useQuery({
    queryKey: dashboardKeys.blog(trendDays),
    queryFn: () => getBlogDashboard(trendDays),
    staleTime: 60_000,
    refetchInterval: 5 * 60_000,
  })

export const useInventoryDashboard = (threshold?: number) =>
  useQuery({
    queryKey: dashboardKeys.inventory(threshold),
    queryFn: () => getInventoryDashboard(threshold),
    staleTime: 60_000,
    refetchInterval: 5 * 60_000,
  })

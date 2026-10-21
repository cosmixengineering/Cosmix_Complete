/**
 * Route Prefetch Registry
 * Maps route paths to their dynamic import functions for hover prefetching.
 * Chunks are loaded via requestIdleCallback when user hovers over nav links.
 */

// Registry mapping route paths to their dynamic import functions
// These MUST match the same import() calls used in App.tsx lazy()
const routeImports: Record<string, () => Promise<unknown>> = {
  '/portal': () => import('@/portal-app/dashboard/features/dashboard/DashboardPage'),
  '/portal/settings': () => import('@/portal-app/settings/features/personal-settings/PersonalSettingsPage'),
  '/portal/notifications': () => import('@/portal-app/notifications/features/notification-list/NotificationsPage'),
  '/portal/settings/notifications': () => import('@/portal-app/notifications/features/notification-preferences/NotificationPreferencesPage'),
  '/portal/admin/tenants': () => import('@/portal-app/user-access/features/tenant-list/TenantsPage'),
  '/portal/admin/roles': () => import('@/portal-app/user-access/features/role-list/RolesPage'),
  '/portal/admin/users': () => import('@/portal-app/user-access/features/user-list/UsersPage'),
  '/portal/activity-timeline': () => import('@/portal-app/systems/features/activity-timeline/ActivityTimelinePage'),
  '/portal/developer-logs': () => import('@/portal-app/systems/features/developer-logs/DeveloperLogsPage'),
  '/portal/blog/posts': () => import('@/portal-app/blogs/features/blog-post-list/BlogPostsPage'),
  '/portal/blog/posts/new': () => import('@/portal-app/blogs/features/blog-post-edit/BlogPostEditPage'),
  '/portal/blog/categories': () => import('@/portal-app/blogs/features/blog-category-list/BlogCategoriesPage'),
  '/portal/blog/tags': () => import('@/portal-app/blogs/features/blog-tag-list/BlogTagsPage'),
  '/portal/ecommerce/products': () => import('@/portal-app/products/features/product-list/ProductsPage'),
  '/portal/ecommerce/products/new': () => import('@/portal-app/products/features/product-edit/ProductFormPage'),
  '/portal/ecommerce/categories': () => import('@/portal-app/products/features/product-category-list/ProductCategoriesPage'),
  '/portal/ecommerce/brands': () => import('@/portal-app/brands/features/brand-list/BrandsPage'),
  '/portal/ecommerce/attributes': () => import('@/portal-app/products/features/product-attribute-list/ProductAttributesPage'),
  '/portal/ecommerce/payments': () => import('@/portal-app/payments/features/payment-list/PaymentsPage'),
  '/portal/ecommerce/orders': () => import('@/portal-app/orders/features/order-list/OrdersPage'),
  '/portal/ecommerce/orders/create': () => import('@/portal-app/orders/features/manual-create/ManualCreateOrderPage'),
  '/portal/ecommerce/inventory': () => import('@/portal-app/inventory/features/inventory-receipts/InventoryReceiptsPage'),
  '/portal/ecommerce/customers': () => import('@/portal-app/customers/features/customer-list/CustomersPage'),
  '/portal/ecommerce/customer-groups': () => import('@/portal-app/customer-groups/features/customer-group-list/CustomerGroupsPage'),
  '/portal/ecommerce/reviews': () => import('@/portal-app/reviews/features/review-list/ReviewsPage'),
  '/portal/ecommerce/wishlists': () => import('@/portal-app/wishlists/features/wishlist-analytics/WishlistAnalyticsPage'),
  '/portal/ecommerce/wishlists/manage': () => import('@/portal-app/wishlists/features/wishlist-page/WishlistPage'),
  '/portal/marketing/promotions': () => import('@/portal-app/promotions/features/promotion-list/PromotionsPage'),
  '/portal/marketing/reports': () => import('@/portal-app/reports/features/reports-page/ReportsPage'),
  '/portal/admin/platform-settings': () => import('@/portal-app/settings/features/platform-settings/PlatformSettingsPage'),
  '/portal/admin/tenant-settings': () => import('@/portal-app/settings/features/tenant-settings/TenantSettingsPage'),
}

// Track already-prefetched routes to avoid duplicate network requests
const prefetchedRoutes = new Set<string>()

/**
 * Find the best matching route pattern for a given path.
 * Exact match preferred, falls back to longest prefix match.
 */
const findBestMatch = (path: string): string | undefined => {
  // Exact match
  if (routeImports[path]) return path

  // Strip query params and hash
  const cleanPath = path.split('?')[0].split('#')[0]
  if (routeImports[cleanPath]) return cleanPath

  // Longest prefix match (for routes like /portal/ecommerce/products/123)
  let bestMatch: string | undefined
  let bestLength = 0
  for (const pattern of Object.keys(routeImports)) {
    if (cleanPath.startsWith(pattern) && pattern.length > bestLength) {
      bestMatch = pattern
      bestLength = pattern.length
    }
  }
  return bestMatch
}

/**
 * Prefetch a route's chunk by its path.
 * Uses requestIdleCallback for non-blocking loading.
 * Deduplicates to avoid redundant network requests.
 */
export const prefetchRoute = (path: string): void => {
  const matchedPattern = findBestMatch(path)
  if (!matchedPattern || prefetchedRoutes.has(matchedPattern)) return

  prefetchedRoutes.add(matchedPattern)

  const doImport = () => {
    routeImports[matchedPattern]?.().catch(() => {
      // Remove from set so it can be retried on next hover
      prefetchedRoutes.delete(matchedPattern)
    })
  }

  if ('requestIdleCallback' in window) {
    requestIdleCallback(doImport)
  } else {
    setTimeout(doImport, 100)
  }
}

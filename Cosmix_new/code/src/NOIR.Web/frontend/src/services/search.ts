/**
 * Global Search API Service
 */
import { apiClient } from './apiClient'

export interface GlobalSearchResult {
  type: 'product' | 'order' | 'customer' | 'blogPost' | 'user'
  id: string
  title: string
  subtitle?: string | null
  url: string
  imageUrl?: string | null
}

export interface GlobalSearchResponse {
  products: GlobalSearchResult[]
  orders: GlobalSearchResult[]
  customers: GlobalSearchResult[]
  blogPosts: GlobalSearchResult[]
  users: GlobalSearchResult[]
  totalCount: number
}

export const globalSearch = async (query: string): Promise<GlobalSearchResponse> =>
  apiClient<GlobalSearchResponse>(`/search?q=${encodeURIComponent(query)}`)

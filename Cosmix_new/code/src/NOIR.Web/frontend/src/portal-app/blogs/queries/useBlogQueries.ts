import { useQuery, keepPreviousData } from '@tanstack/react-query'
import {
  getPosts,
  getPostById,
  getCategories,
  getTags,
  type GetPostsParams,
  type GetCategoriesParams,
  type GetTagsParams,
} from '@/services/blog'
import { blogPostKeys, blogCategoryKeys, blogTagKeys } from './queryKeys'

export const useBlogPostsQuery = (params: GetPostsParams) =>
  useQuery({
    queryKey: blogPostKeys.list(params),
    queryFn: () => getPosts(params),
    placeholderData: keepPreviousData,
  })

export const useBlogPostDetailQuery = (id: string | undefined) =>
  useQuery({
    queryKey: blogPostKeys.detail(id!),
    queryFn: () => getPostById(id!),
    enabled: !!id,
  })

export const useBlogCategoriesQuery = (params: GetCategoriesParams = {}) =>
  useQuery({
    queryKey: blogCategoryKeys.list(params),
    queryFn: () => getCategories(params),
    placeholderData: keepPreviousData,
  })

export const useBlogTagsQuery = (params: GetTagsParams = {}) =>
  useQuery({
    queryKey: blogTagKeys.list(params),
    queryFn: () => getTags(params),
    placeholderData: keepPreviousData,
  })
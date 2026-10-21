/**
 * Media API Service
 *
 * Provides methods for uploading and managing media files.
 */
import { apiClient } from './apiClient'
import { i18n } from '@/i18n'
import type { MediaFile, MediaUploadResult, MediaFilesParams, PagedMediaResult, BulkMediaOperationResult } from '@/types'

export type MediaFolder = 'blog' | 'content' | 'avatars' | 'branding' | 'products'

/**
 * Upload a file to the media service
 */
export const uploadMedia = async (
  file: File,
  folder: MediaFolder = 'content',
  entityId?: string
): Promise<MediaUploadResult> => {
  const formData = new FormData()
  formData.append('file', file)

  const queryParams = new URLSearchParams()
  queryParams.append('folder', folder)
  if (entityId) {
    queryParams.append('entityId', entityId)
  }

  const response = await fetch(`/api/media/upload?${queryParams.toString()}`, {
    method: 'POST',
    body: formData,
    credentials: 'include',
  })

  if (!response.ok) {
    const error = await response.json().catch(() => ({ error: i18n.t('errors.uploadFailed', { ns: 'common' }) }))
    throw new Error(error.error || i18n.t('errors.uploadFailed', { ns: 'common' }))
  }

  return response.json()
}

/**
 * Get media file by ID
 */
export const getMediaById = async (id: string): Promise<MediaFile> => {
  return apiClient<MediaFile>(`/media/${id}`)
}

/**
 * Get media file by slug
 */
export const getMediaBySlug = async (slug: string): Promise<MediaFile> => {
  return apiClient<MediaFile>(`/media/by-slug/${slug}`)
}

/**
 * Get media file by short ID
 */
export const getMediaByShortId = async (shortId: string): Promise<MediaFile> => {
  return apiClient<MediaFile>(`/media/by-short-id/${shortId}`)
}

/**
 * Get media file by URL
 */
export const getMediaByUrl = async (url: string): Promise<MediaFile> => {
  const queryParams = new URLSearchParams()
  queryParams.append('url', url)
  return apiClient<MediaFile>(`/media/by-url?${queryParams.toString()}`)
}

/**
 * Batch get media files by IDs
 */
export const getMediaByIds = async (ids: string[]): Promise<MediaFile[]> => {
  return apiClient<MediaFile[]>('/media/batch/by-ids', {
    method: 'POST',
    body: JSON.stringify({ ids }),
  })
}

/**
 * Batch get media files by slugs
 */
export const getMediaBySlugs = async (slugs: string[]): Promise<MediaFile[]> => {
  return apiClient<MediaFile[]>('/media/batch/by-slugs', {
    method: 'POST',
    body: JSON.stringify({ slugs }),
  })
}

/**
 * Batch get media files by short IDs
 */
export const getMediaByShortIds = async (shortIds: string[]): Promise<MediaFile[]> => {
  return apiClient<MediaFile[]>('/media/batch/by-short-ids', {
    method: 'POST',
    body: JSON.stringify({ shortIds }),
  })
}

/**
 * Get paginated list of media files
 */
export const getMediaFiles = (params: MediaFilesParams): Promise<PagedMediaResult> => {
  const query = new URLSearchParams()
  if (params.search) query.append('search', params.search)
  if (params.fileType) query.append('fileType', params.fileType)
  if (params.folder) query.append('folder', params.folder)
  if (params.orderBy) query.append('orderBy', params.orderBy)
  if (params.isDescending != null) query.append('isDescending', String(params.isDescending))
  if (params.page) query.append('page', String(params.page))
  if (params.pageSize) query.append('pageSize', String(params.pageSize))
  return apiClient<PagedMediaResult>(`/media?${query.toString()}`)
}

/**
 * Delete a media file (soft delete)
 */
export const deleteMediaFile = (id: string): Promise<void> =>
  apiClient('/media/' + id, { method: 'DELETE' })

/**
 * Rename a media file
 */
export const renameMediaFile = (id: string, newFileName: string): Promise<void> =>
  apiClient('/media/' + id + '/rename', {
    method: 'PUT',
    body: JSON.stringify({ newFileName }),
  })

/**
 * Bulk delete media files
 */
export const bulkDeleteMediaFiles = (ids: string[]): Promise<BulkMediaOperationResult> =>
  apiClient('/media/bulk-delete', {
    method: 'POST',
    body: JSON.stringify({ ids }),
  })

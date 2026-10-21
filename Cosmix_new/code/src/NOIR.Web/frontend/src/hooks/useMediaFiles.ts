import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { getMediaFiles, deleteMediaFile, renameMediaFile, bulkDeleteMediaFiles, uploadMedia } from '@/services/media'
import type { MediaFilesParams } from '@/types'
import { toast } from 'sonner'
import { useTranslation } from 'react-i18next'

const MEDIA_QUERY_KEYS = {
  all: ['media'] as const,
  lists: () => [...MEDIA_QUERY_KEYS.all, 'list'] as const,
  list: (params: MediaFilesParams) => [...MEDIA_QUERY_KEYS.lists(), params] as const,
  detail: (id: string) => [...MEDIA_QUERY_KEYS.all, 'detail', id] as const,
}

export const useMediaFiles = (params: MediaFilesParams) => {
  return useQuery({
    queryKey: MEDIA_QUERY_KEYS.list(params),
    queryFn: () => getMediaFiles(params),
    placeholderData: (prev) => prev,
  })
}

export const useDeleteMediaFile = () => {
  const queryClient = useQueryClient()
  const { t } = useTranslation('common')
  return useMutation({
    mutationFn: deleteMediaFile,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: MEDIA_QUERY_KEYS.lists() })
      toast.success(t('media.deleteSuccess', 'Media file deleted'))
    },
    onError: () => toast.error(t('media.deleteError', 'Failed to delete media file')),
  })
}

export const useRenameMediaFile = () => {
  const queryClient = useQueryClient()
  const { t } = useTranslation('common')
  return useMutation({
    mutationFn: ({ id, newFileName }: { id: string; newFileName: string }) => renameMediaFile(id, newFileName),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: MEDIA_QUERY_KEYS.lists() })
      toast.success(t('media.renameSuccess', 'Media file renamed'))
    },
    onError: () => toast.error(t('media.renameError', 'Failed to rename media file')),
  })
}

export const useBulkDeleteMediaFiles = () => {
  const queryClient = useQueryClient()
  const { t } = useTranslation('common')
  return useMutation({
    mutationFn: bulkDeleteMediaFiles,
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: MEDIA_QUERY_KEYS.lists() })
      toast.success(t('media.bulkDeleteSuccess', { count: result.success, defaultValue: `${result.success} files deleted` }))
    },
    onError: () => toast.error(t('media.bulkDeleteError', 'Failed to delete media files')),
  })
}

export const useUploadMediaFile = () => {
  const queryClient = useQueryClient()
  const { t } = useTranslation('common')
  return useMutation({
    mutationFn: ({ file, folder }: { file: File; folder?: string }) => uploadMedia(file, (folder as 'blog' | 'content' | 'avatars' | 'branding' | 'products') || 'content'),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: MEDIA_QUERY_KEYS.lists() })
      toast.success(t('media.uploadSuccess', 'File uploaded successfully'))
    },
    onError: () => toast.error(t('media.uploadError', 'Failed to upload file')),
  })
}

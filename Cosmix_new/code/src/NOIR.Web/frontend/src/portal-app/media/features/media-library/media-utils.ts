/** Format file size from bytes to human-readable string */
export const formatFileSize = (bytes: number): string => {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i]
}

/** Known logical folders for media storage */
const KNOWN_FOLDERS = ['blog', 'content', 'avatars', 'branding', 'products']

/** Extract base folder name from storage path (e.g. "images/blog/uuid" → "blog") */
export const extractFolderName = (folder: string): string => {
  const segments = folder.split('/').filter(Boolean)
  const found = segments.find(s => KNOWN_FOLDERS.includes(s))
  return found || segments[0] || folder
}

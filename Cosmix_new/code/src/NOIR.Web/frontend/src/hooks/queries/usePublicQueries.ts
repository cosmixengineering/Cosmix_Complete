import { useQuery } from '@tanstack/react-query'
import { getPublicLegalPage } from '@/services/legalPages'
import { publicKeys } from './queryKeys'

export const usePublicLegalPageQuery = (slug: string) =>
  useQuery({
    queryKey: publicKeys.legalPage(slug),
    queryFn: () => getPublicLegalPage(slug),
    staleTime: 10 * 60_000,
  })

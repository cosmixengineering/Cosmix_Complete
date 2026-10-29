import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { ApiError } from '@/services/api/api-error';
import { attendanceGet } from '../api/attendance-api';
import { useAttendanceAccess } from './use-attendance-access';

export function useAttendanceQuery<T extends z.ZodType>(path: string, schema: T, allowed: boolean) {
  const access = useAttendanceAccess();
  return useQuery({
    queryKey: ['attendance', access.subject, path],
    queryFn: ({ signal }) => {
      if (!access.ready) throw new ApiError(401);
      if (!allowed) throw new ApiError(403);
      return attendanceGet(path, schema, signal);
    },
    enabled: access.ready && allowed,
    gcTime: 0,
    retry: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });
}

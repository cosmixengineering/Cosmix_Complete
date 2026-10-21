import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { attendanceGet } from '../api/attendance-api';
import { useAttendanceAccess } from './use-attendance-access';

export function useAttendanceQuery<T extends z.ZodType>(path: string, schema: T, allowed: boolean) {
  const access = useAttendanceAccess();
  return useQuery({
    queryKey: ['attendance', access.subject, path],
    queryFn: ({ signal }) => attendanceGet(path, schema, signal),
    enabled: access.ready && allowed,
    gcTime: 0,
    retry: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });
}

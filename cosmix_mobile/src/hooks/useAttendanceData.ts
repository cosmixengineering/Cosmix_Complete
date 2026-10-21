import { useCallback, useRef, useState } from 'react';
import { useFocusEffect } from '@react-navigation/native';
import { useServices, useSession } from '../app/AppProvider';
import type { AttendanceState, Page, PunchReceipt } from '../types/contracts';
import { AppError, describeError } from '../utils/errors';
import { historyWindow } from '../utils/presentation';
import { isGuid, isRecord, isTimestamp } from '../utils/validation';

export function useAttendanceData() {
  const { session } = useServices();
  const auth = useSession();
  const permitted = auth.permissions.some(
    grant => grant.permission === 'attendance.history.self',
  );
  const [state, setState] = useState<AttendanceState | null>(null);
  const [history, setHistory] = useState<PunchReceipt[]>([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const generation = useRef(0);
  const refresh = useCallback(async () => {
    const current = ++generation.current;
    setState(null);
    setHistory([]);
    setError('');
    setLoading(true);
    try {
      await session.revalidate();
      const currentAuth = session.getSnapshot();
      if (
        !currentAuth.permissions.some(
          grant => grant.permission === 'attendance.history.self',
        )
      ) {
        return;
      }
      const value = await session.request<AttendanceState>(
        '/api/v1/me/attendance/state',
      );
      if (
        !isRecord(value) ||
        value.employeeId !== currentAuth.account?.employeeId ||
        !isTimestamp(value.serverTimeUtc) ||
        !['checked_in', 'checked_out', 'conflict'].includes(value.state) ||
        typeof value.checkInEnabled !== 'boolean' ||
        typeof value.checkOutEnabled !== 'boolean' ||
        typeof value.nextAction !== 'string' ||
        typeof value.faceStatus !== 'string' ||
        typeof value.timeZone !== 'string' ||
        !Array.isArray(value.limitations) ||
        !value.limitations.every(item => typeof item === 'string') ||
        (value.activeSession !== null &&
          (!isRecord(value.activeSession) ||
            !isTimestamp(value.activeSession.startsAtUtc)))
      ) {
        throw new AppError('invalid_server_response');
      }
      const { from, to } = historyWindow(value.serverTimeUtc, value.timeZone);
      const page = await session.request<Page<PunchReceipt>>(
        `/api/v1/me/attendance/punches?from=${from}&to=${to}&page=1&pageSize=5`,
      );
      if (
        !isRecord(page) ||
        !Array.isArray(page.items) ||
        page.items.length > 5 ||
        !page.items.every(
          item =>
            isRecord(item) &&
            isGuid(item.id) &&
            ['CheckIn', 'CheckOut'].includes(item.action) &&
            isTimestamp(item.receivedAtUtc) &&
            typeof item.outcome === 'string',
        )
      ) {
        throw new AppError('invalid_server_response');
      }
      if (current === generation.current) {
        setState(value);
        setHistory(page.items);
      }
    } catch (failure) {
      if (current === generation.current) {
        setError(describeError(failure));
      }
    } finally {
      if (current === generation.current) {
        setLoading(false);
      }
    }
  }, [session]);

  useFocusEffect(
    useCallback(() => {
      refresh();
      return () => {
        generation.current++;
      };
    }, [refresh]),
  );
  return { state, history, error, loading, permitted, refresh };
}

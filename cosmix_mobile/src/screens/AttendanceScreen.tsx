import React, { useCallback, useEffect, useRef, useState } from 'react';
import { BackHandler, StyleSheet, Text, View } from 'react-native';
import { useFocusEffect } from '@react-navigation/native';
import { useServices, useSession } from '../app/AppProvider';
import { Button, Notice, Screen } from '../components/Controls';
import { useAttendanceData } from '../hooks/useAttendanceData';
import { useWorkspaceLock } from '../navigation/WorkspaceInteraction';
import { colors, layout } from '../theme/theme';
import type {
  AttendanceStep,
  PendingPunch,
  PunchReceipt,
} from '../types/contracts';
import { describeError } from '../utils/errors';
import { localTime, receiptMessage } from '../utils/presentation';
import { isAcceptedReceipt } from '../utils/validation';
import { LocationConsentScreen } from './consent/LocationConsentScreen';
import { LocationConsentService } from '../services/consent/locationConsentService';

const steps: Record<AttendanceStep, string> = {
  preparing: 'Checking attendance availability…',
  camera: 'Take a clear front-camera photo',
  uploading: 'Sending your photo securely…',
  verifying: 'Checking face and liveness…',
  location: 'Getting your current location…',
  submitting: 'Confirming with the company server…',
};

export function AttendanceScreen(): React.JSX.Element {
  const { attendance } = useServices();
  const auth = useSession();
  const data = useAttendanceData();
  const [step, setStep] = useState<AttendanceStep | null>(null);
  const [error, setError] = useState('');
  const [receipt, setReceipt] = useState<PunchReceipt | null>(null);
  const [pending, setPending] = useState<PendingPunch | null>(null);
  const [pendingReady, setPendingReady] = useState(false);
  const flight = useRef(false);
  const mounted = useRef(true);
  const busy = step !== null;
  useWorkspaceLock(busy);
  const captureAllowed = auth.permissions.some(
    grant => grant.permission === 'attendance.punches.self',
  );
  const faceAllowed = auth.permissions.some(
    grant => grant.permission === 'attendance.face.self',
  );

  const [consentPending, setConsentPending] = useState(false);
  const consentService = useRef(new LocationConsentService(auth as any)).current;

  useEffect(() => {
    consentService.getConsentStatus().then(status => {
      if (status.hasActivePolicy && status.status === 'PendingConsent') {
        setConsentPending(true);
      }
    }).catch(() => {});
  }, [consentService]);

  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);
  useEffect(() => {
    if (!busy) {
      return;
    }
    const subscription = BackHandler.addEventListener(
      'hardwareBackPress',
      () => true,
    );
    return () => subscription.remove();
  }, [busy]);
  useFocusEffect(
    useCallback(() => {
      let active = true;
      setPendingReady(false);
      attendance
        .getPending()
        .then(value => {
          if (active) {
            setPending(value);
            setPendingReady(true);
          }
        })
        .catch(failure => {
          if (active) {
            setError(describeError(failure));
          }
        });
      return () => {
        active = false;
      };
    }, [attendance]),
  );

  const submit = async (action: 'check-in' | 'check-out' | 'retry') => {
    if (flight.current) {
      return;
    }
    flight.current = true;
    setStep('preparing');
    setError('');
    setReceipt(null);
    const progress = (value: AttendanceStep) => {
      if (mounted.current) {
        setStep(value);
      }
    };
    try {
      const result =
        action === 'retry'
          ? await attendance.retryPending(progress)
          : action === 'check-in'
          ? await attendance.checkIn(progress)
          : await attendance.checkOut(progress);
      if (mounted.current) {
        setReceipt(result);
      }
    } catch (failure) {
      if (mounted.current) {
        setError(describeError(failure));
      }
    } finally {
      if (mounted.current) {
        try {
          setPending(await attendance.getPending());
          setPendingReady(true);
        } catch (failure) {
          setPendingReady(false);
          setError(describeError(failure));
        }
        if (mounted.current) {
          setStep(null);
          data.refresh();
        }
      }
      flight.current = false;
    }
  };

  if (consentPending) {
    return (
      <LocationConsentScreen
        onConsentAccepted={() => setConsentPending(false)}
        onDeclineOrSkip={() => setConsentPending(false)}
      />
    );
  }

  return (
    <Screen>
      <View>
        <Text style={layout.title}>Attendance</Text>
        <Text style={layout.muted}>
          A secure start and finish to your workday.
        </Text>
      </View>
      {receipt ? (
        <Notice
          message={receiptMessage(receipt)}
          tone={isAcceptedReceipt(receipt) ? 'success' : 'info'}
        />
      ) : null}
      {receipt ? (
        <Text selectable style={layout.muted}>
          Receipt: {receipt.id}
        </Text>
      ) : null}
      {error || data.error ? <Notice message={error || data.error} /> : null}
      {pending ? (
        <View style={layout.card}>
          <Text style={layout.heading}>Submission needs confirmation</Text>
          <Text style={layout.body}>
            Your last{' '}
            {pending.endpoint === 'check-in' ? 'Check In' : 'Check Out'} may
            already be recorded. Retry to retrieve its result before starting
            another submission.
          </Text>
          <Button
            title="Confirm previous submission"
            onPress={() => submit('retry')}
            busy={busy}
          />
        </View>
      ) : null}
      <View style={layout.card}>
        <View style={layout.row}>
          <Text style={layout.heading}>Current status</Text>
          <Text style={styles.status}>
            {data.state?.state === 'checked_in'
              ? 'Checked in'
              : data.state?.state === 'checked_out'
              ? 'Checked out'
              : data.loading
              ? 'Loading…'
              : 'Unavailable'}
          </Text>
        </View>
        {data.state?.activeSession ? (
          <Text style={layout.body}>
            Started{' '}
            {localTime(
              data.state.activeSession.startsAtUtc,
              data.state.timeZone,
            )}
          </Text>
        ) : null}
        {data.state &&
        !data.state.checkInEnabled &&
        !data.state.checkOutEnabled ? (
          <Notice
            tone="info"
            message={
              data.state.nextAction === 'view_shift'
                ? 'Check In is available during your assigned shift. Contact your supervisor if your schedule needs updating.'
                : 'Attendance is currently unavailable. Your supervisor can check your schedule, enrollment and account access.'
            }
          />
        ) : null}
        <Text style={layout.muted}>
          Your company validates your current location and assigned site or
          office when you submit.
        </Text>
      </View>
      <View style={layout.card}>
        <Text style={layout.heading}>Check In</Text>
        <Text style={layout.body}>
          Use good lighting, remove anything covering your face, and keep one
          face in view. We will verify your face and liveness, then confirm your
          location.
        </Text>
        <Text style={layout.muted}>
          Camera and precise location access are required. Your face must
          already be enrolled by HR.
        </Text>
        <Button
          title="Verify face & check in"
          onPress={() => submit('check-in')}
          disabled={
            busy ||
            !!pending ||
            !pendingReady ||
            !captureAllowed ||
            !faceAllowed ||
            !data.state?.checkInEnabled
          }
        />
      </View>
      <View style={layout.card}>
        <Text style={layout.heading}>Check Out</Text>
        <Text style={layout.body}>
          Finish your active work session using your current location. Face
          verification is not required.
        </Text>
        <Button
          title="Check out"
          secondary
          onPress={() => submit('check-out')}
          disabled={
            busy ||
            !!pending ||
            !pendingReady ||
            !captureAllowed ||
            !data.state?.checkOutEnabled
          }
        />
      </View>
      {step ? <Notice tone="info" message={steps[step]} /> : null}
      {!busy ? (
        <Button
          title="Refresh attendance"
          secondary
          busy={data.loading}
          onPress={() => data.refresh()}
        />
      ) : null}
    </Screen>
  );
}

const styles = StyleSheet.create({
  status: { fontSize: 14, fontWeight: '700', color: colors.teal },
});

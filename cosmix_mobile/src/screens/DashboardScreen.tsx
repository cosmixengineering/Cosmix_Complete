import React, { useState } from 'react';
import { ActivityIndicator, Alert, StyleSheet, Text, View } from 'react-native';
import { useServices, useSession } from '../app/AppProvider';
import { Button, Notice, Screen } from '../components/Controls';
import { useAttendanceData } from '../hooks/useAttendanceData';
import type { WorkspaceScreenProps } from '../navigation/types';
import { colors, layout } from '../theme/theme';
import { localTime, outcomeLabel } from '../utils/presentation';

export function DashboardScreen({
  navigation,
}: WorkspaceScreenProps<'Dashboard'>): React.JSX.Element {
  const auth = useSession();
  const { session } = useServices();
  const data = useAttendanceData();
  const [signingOut, setSigningOut] = useState(false);
  const signOut = () =>
    Alert.alert(
      'Sign out?',
      'You will need an SMS verification code to sign in again.',
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Sign out',
          style: 'destructive',
          onPress: () => {
            setSigningOut(true);
            session
              .signOut()
              .catch(() => undefined)
              .finally(() => setSigningOut(false));
          },
        },
      ],
    );
  const status =
    data.state?.state === 'checked_in'
      ? 'Checked in'
      : data.state?.state === 'checked_out'
      ? 'Checked out'
      : 'Needs review';
  return (
    <Screen>
      <View>
        <Text style={styles.eyebrow}>COSMIX · EMPLOYEE WORKSPACE</Text>
        <Text style={layout.title}>
          Hello, {auth.account?.displayName || 'there'}.
        </Text>
        <Text style={layout.muted}>Your workday at a glance.</Text>
      </View>
      <View style={styles.hero}>
        <Text style={styles.heroLabel}>ATTENDANCE</Text>
        {data.loading ? (
          <ActivityIndicator color={colors.white} />
        ) : (
          <Text style={styles.heroTitle}>
            {data.state ? status : 'Status unavailable'}
          </Text>
        )}
        <Text style={styles.heroBody}>
          {data.state?.activeSession
            ? `Started ${localTime(
                data.state.activeSession.startsAtUtc,
                data.state.timeZone,
              )}`
            : 'View your current attendance and submit Check In or Check Out.'}
        </Text>
        {data.state ? (
          <Text style={styles.heroBody}>
            Updated {localTime(data.state.serverTimeUtc, data.state.timeZone)}
          </Text>
        ) : null}
      </View>
      {!data.permitted ? (
        <Notice
          tone="info"
          message="Attendance access is currently unavailable. Refresh to check your access, or contact your administrator."
        />
      ) : null}
      {data.error ? <Notice message={data.error} /> : null}
      <Button
        title="Open attendance"
        onPress={() => navigation.navigate('Attendance')}
        disabled={!data.permitted || signingOut}
      />
      <View style={layout.card}>
        <Text style={layout.heading}>Employee services</Text>
        <Text style={layout.body}>
          Leave, salary advances, loans, resignation requests and your work
          records.
        </Text>
        <Button
          title="Explore employee services"
          onPress={() => navigation.navigate('Services')}
          disabled={signingOut}
        />
      </View>
      <View style={layout.card}>
        <View style={layout.row}>
          <Text style={layout.heading}>Recent attendance</Text>
          <Text style={layout.muted}>7 days</Text>
        </View>
        {data.history.length === 0 ? (
          <Text style={layout.muted}>
            {data.loading
              ? 'Loading attendance…'
              : data.state
              ? 'No submissions in this period.'
              : 'Attendance history is currently unavailable.'}
          </Text>
        ) : (
          data.history.map(receipt => (
            <View key={receipt.id} style={styles.historyRow}>
              <View style={styles.historyText}>
                <Text style={layout.label}>
                  {receipt.action === 'CheckIn' ? 'Check In' : 'Check Out'}
                </Text>
                <Text style={layout.muted}>
                  {localTime(receipt.receivedAtUtc, data.state?.timeZone)}
                </Text>
              </View>
              <Text
                style={[
                  styles.badge,
                  receipt.outcome === 'Accepted'
                    ? styles.accepted
                    : styles.review,
                ]}
              >
                {outcomeLabel(receipt.outcome)}
              </Text>
            </View>
          ))
        )}
        <Text style={layout.muted}>
          Submission records show the server assessment. Review and payroll
          decisions are handled separately.
        </Text>
        <Button
          title="View full attendance history"
          secondary
          onPress={() =>
            navigation.navigate('History', { category: 'punches' })
          }
          disabled={!data.permitted || signingOut}
        />
      </View>
      <Button
        title="Refresh"
        secondary
        busy={data.loading}
        onPress={() => data.refresh()}
      />
      <Button title="Sign out" secondary busy={signingOut} onPress={signOut} />
    </Screen>
  );
}

const styles = StyleSheet.create({
  eyebrow: {
    fontSize: 11,
    fontWeight: '700',
    color: colors.teal,
    letterSpacing: 1.2,
    marginBottom: 10,
  },
  hero: {
    backgroundColor: colors.navy,
    borderRadius: 22,
    padding: 24,
    gap: 12,
  },
  heroLabel: {
    color: '#9ADCD2',
    fontSize: 12,
    fontWeight: '700',
    letterSpacing: 1.4,
  },
  heroTitle: { color: colors.white, fontSize: 30, fontWeight: '700' },
  heroBody: { color: '#D9E9F0', fontSize: 14, lineHeight: 22 },
  historyRow: {
    flexDirection: 'row',
    gap: 10,
    alignItems: 'center',
    paddingVertical: 12,
    borderTopWidth: 1,
    borderColor: colors.line,
  },
  historyText: { flex: 1, gap: 4 },
  badge: {
    fontSize: 12,
    fontWeight: '600',
    padding: 8,
    borderRadius: 8,
    maxWidth: '48%',
  },
  accepted: { backgroundColor: colors.mint, color: colors.teal },
  review: { backgroundColor: colors.amberSoft, color: colors.amber },
});

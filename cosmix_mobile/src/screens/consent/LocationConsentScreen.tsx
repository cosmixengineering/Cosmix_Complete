import React, { useEffect, useState } from 'react';
import {
  ScrollView,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import { useSession } from '../../app/AppProvider';
import { Button, Notice, Screen } from '../../components/Controls';
import { LocationConsentService, type LocationConsentStatus } from '../../services/consent/locationConsentService';
import { colors, layout } from '../../theme/theme';

interface Props {
  onConsentAccepted: () => void;
  onDeclineOrSkip?: () => void;
}

export function LocationConsentScreen({
  onConsentAccepted,
  onDeclineOrSkip,
}: Props): React.JSX.Element {
  const session = useSession();
  const [consentService] = useState(() => new LocationConsentService(session as any));
  const [status, setStatus] = useState<LocationConsentStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [agreed, setAgreed] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    let mounted = true;
    consentService.getConsentStatus()
      .then((res) => {
        if (!mounted) return;
        setStatus(res);
        if (res.status === 'Consented') {
          onConsentAccepted();
        }
      })
      .catch(() => {
        if (mounted) {
          setStatus({ hasActivePolicy: false, status: 'NotRequired' });
        }
      })
      .finally(() => {
        if (mounted) setLoading(false);
      });

    return () => {
      mounted = false;
    };
  }, [consentService, onConsentAccepted]);

  const handleAccept = async () => {
    if (!agreed) return;
    setSubmitting(true);
    setError('');
    try {
      await consentService.acceptConsent();
      onConsentAccepted();
    } catch {
      setError('Failed to record location consent. Please check network connection.');
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <Screen>
        <View style={styles.center}>
          <Text style={styles.loadingText}>Checking location tracking policy…</Text>
        </View>
      </Screen>
    );
  }

  if (!status?.hasActivePolicy || status.status === 'NotRequired') {
    return (
      <Screen>
        <View style={layout.card}>
          <Text style={layout.title}>Location Notice</Text>
          <Notice
            tone="info"
            message="No specific location tracking policy is currently active for your organization."
          />
          <View style={styles.spacer} />
          <Button
            title="Continue"
            onPress={onConsentAccepted}
            disabled={false}
          />
        </View>
      </Screen>
    );
  }

  return (
    <Screen>
      <View style={layout.card}>
        <Text style={layout.title}>Location Collection Notice</Text>
        <Text style={styles.subheading}>
          Policy Version {status.activePolicyVersion ?? 1}
        </Text>

        <ScrollView style={styles.noticeBox} contentContainerStyle={styles.noticeContent}>
          <Text style={styles.noticeText}>
            {status.noticeText ||
              'Location coordinates are collected strictly for verified attendance check-ins, departures, and approved zone operations. Your coordinates are never tracked outside active duty sessions.'}
          </Text>
        </ScrollView>

        {error ? <Notice tone="error" message={error} /> : null}

        <TouchableOpacity
          style={styles.checkboxRow}
          onPress={() => setAgreed(!agreed)}
          activeOpacity={0.8}
        >
          <View style={[styles.checkbox, agreed && styles.checkboxActive]}>
            {agreed && <Text style={styles.checkmark}>✓</Text>}
          </View>
          <Text style={styles.checkboxLabel}>
            I have read and accept the company location collection policy
          </Text>
        </TouchableOpacity>

        <View style={styles.buttonGroup}>
          <Button
            title={submitting ? 'Recording acceptance…' : 'Accept and Continue'}
            onPress={handleAccept}
            disabled={!agreed || submitting}
            busy={submitting}
          />

          {onDeclineOrSkip && (
            <TouchableOpacity
              style={styles.skipBtn}
              onPress={onDeclineOrSkip}
              disabled={submitting}
            >
              <Text style={styles.skipText}>Decline (use other features)</Text>
            </TouchableOpacity>
          )}
        </View>
      </View>
    </Screen>
  );
}

const styles = StyleSheet.create({
  center: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 24,
  },
  loadingText: {
    color: colors.muted,
    fontSize: 14,
  },
  subheading: {
    fontSize: 13,
    color: colors.muted,
    marginBottom: 8,
  },
  noticeBox: {
    maxHeight: 220,
    borderWidth: 1,
    borderColor: colors.line,
    borderRadius: 12,
    backgroundColor: colors.background,
    padding: 12,
    marginBottom: 12,
  },
  noticeContent: {
    paddingBottom: 8,
  },
  noticeText: {
    fontSize: 14,
    lineHeight: 20,
    color: colors.text,
  },
  checkboxRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 16,
  },
  checkbox: {
    width: 22,
    height: 22,
    borderRadius: 4,
    borderWidth: 2,
    borderColor: colors.line,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: 10,
    backgroundColor: colors.white,
  },
  checkboxActive: {
    backgroundColor: colors.teal,
    borderColor: colors.teal,
  },
  checkmark: {
    color: colors.white,
    fontSize: 14,
    fontWeight: 'bold',
  },
  checkboxLabel: {
    flex: 1,
    fontSize: 13,
    color: colors.text,
  },
  buttonGroup: {
    gap: 8,
  },
  skipBtn: {
    paddingVertical: 10,
    alignItems: 'center',
  },
  skipText: {
    fontSize: 13,
    color: colors.muted,
  },
  spacer: {
    height: 12,
  },
});

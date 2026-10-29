import React, { useRef, useState } from 'react';
import {
  ScrollView,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import { Button, Notice, Screen } from '../../components/Controls';
import type {
  LocationConsentService,
  LocationConsentStatus,
} from '../../services/consent/locationConsentService';
import { colors, layout } from '../../theme/theme';

interface Props {
  status: LocationConsentStatus;
  service: LocationConsentService;
  onConsentAccepted: () => Promise<boolean>;
  onPolicyChanged: () => Promise<boolean>;
  onSignOut: () => void;
}

export function LocationConsentScreen({
  status,
  service,
  onConsentAccepted,
  onPolicyChanged,
  onSignOut,
}: Props): React.JSX.Element {
  const [agreed, setAgreed] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');
  const flight = useRef(false);
  const handleAccept = async () => {
    if (!agreed || flight.current || status.activePolicyVersion === undefined)
      return;
    flight.current = true;
    setSubmitting(true);
    setError('');
    try {
      await service.acceptConsent(status.activePolicyVersion);
      setAgreed(false);
      await onConsentAccepted();
    } catch {
      setAgreed(false);
      setError(
        'Acceptance was not confirmed. Check the current policy again and review it before retrying.',
      );
      await onPolicyChanged();
    } finally {
      flight.current = false;
      setSubmitting(false);
    }
  };
  return (
    <Screen>
      <View style={layout.card}>
        <Text style={layout.title}>Location Collection Notice</Text>
        <Text style={styles.subheading}>
          Policy Version {status.activePolicyVersion}
        </Text>
        <ScrollView
          style={styles.noticeBox}
          contentContainerStyle={styles.noticeContent}
        >
          <Text style={styles.noticeText}>{status.noticeText}</Text>
        </ScrollView>
        {error ? <Notice tone="error" message={error} /> : null}
        <TouchableOpacity
          style={styles.checkboxRow}
          onPress={() => setAgreed(!agreed)}
          disabled={submitting}
          accessibilityRole="checkbox"
          accessibilityState={{ checked: agreed, disabled: submitting }}
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
          <Button
            title="Refresh policy"
            secondary
            onPress={() => {
              setAgreed(false);
              onPolicyChanged();
            }}
            disabled={submitting}
          />
          <Button
            title="Decline and sign out"
            secondary
            onPress={onSignOut}
            disabled={submitting}
          />
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
});

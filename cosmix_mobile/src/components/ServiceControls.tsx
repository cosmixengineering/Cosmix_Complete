import React, { useEffect, useState } from 'react';
import {
  KeyboardAvoidingView,
  Modal,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { Button, Field, Notice } from './Controls';
import { describeError } from '../utils/errors';
import { colors, layout } from '../theme/theme';
import type { Choice, DataRecord } from '../types/selfService';
import { status } from '../utils/serviceValues';

export function ChoiceField({
  label,
  value,
  choices,
  onChange,
  disabled = false,
}: {
  label: string;
  value: string;
  choices: readonly Choice[];
  onChange(value: string): void;
  disabled?: boolean;
}): React.JSX.Element {
  return (
    <View style={styles.group}>
      <Text style={layout.label}>{label}</Text>
      <View style={styles.choices}>
        {choices.map(choice => (
          <Pressable
            key={choice.value}
            accessibilityRole="radio"
            accessibilityState={{ selected: value === choice.value, disabled }}
            disabled={disabled}
            onPress={() => onChange(choice.value)}
            style={[styles.choice, value === choice.value && styles.selected]}
          >
            <Text
              style={[
                styles.choiceText,
                value === choice.value && styles.selectedText,
              ]}
            >
              {choice.label}
            </Text>
          </Pressable>
        ))}
        {choices.length === 0 ? (
          <Text style={layout.muted}>
            No available options. Refresh or contact HR.
          </Text>
        ) : null}
      </View>
    </View>
  );
}

export function DateField({
  label,
  value,
  onChange,
  disabled = false,
}: {
  label: string;
  value: string;
  onChange(value: string): void;
  disabled?: boolean;
}): React.JSX.Element {
  const [open, setOpen] = useState(false);
  const [month, setMonth] = useState(() => {
    const parsed = new Date(value + 'T12:00:00Z');
    return Number.isFinite(parsed.getTime()) ? parsed : new Date();
  });
  const year = month.getUTCFullYear();
  const index = month.getUTCMonth();
  const offset = (new Date(Date.UTC(year, index, 1)).getUTCDay() + 6) % 7;
  const count = new Date(Date.UTC(year, index + 1, 0)).getUTCDate();
  const move = (delta: number) => {
    const next = new Date(Date.UTC(year, index + delta, 1));
    if (next.getUTCFullYear() >= 1900 && next.getUTCFullYear() <= 9999) {
      setMonth(next);
    }
  };
  return (
    <View style={styles.group}>
      <Text style={layout.label}>{label}</Text>
      <Pressable
        disabled={disabled}
        accessibilityRole="button"
        accessibilityLabel={`${label}, ${value || 'choose date'}`}
        style={styles.dateButton}
        onPress={() => {
          const parsed = new Date(value + 'T12:00:00Z');
          if (Number.isFinite(parsed.getTime())) {
            setMonth(parsed);
          }
          setOpen(true);
        }}
      >
        <Text style={layout.body}>{value || 'Choose date'}</Text>
        <Text style={styles.calendarLabel}>Calendar</Text>
      </Pressable>
      <Modal
        visible={open}
        transparent
        animationType="fade"
        onRequestClose={() => setOpen(false)}
      >
        <View style={styles.scrim}>
          <View style={styles.modal}>
            <View style={layout.row}>
              <Button title="‹" secondary onPress={() => move(-1)} />
              <Text style={layout.label}>
                {new Intl.DateTimeFormat('en-GB', {
                  month: 'long',
                  year: 'numeric',
                  timeZone: 'UTC',
                }).format(month)}
              </Text>
              <Button title="›" secondary onPress={() => move(1)} />
            </View>
            <View style={styles.calendarGrid}>
              {['M', 'T', 'W', 'T', 'F', 'S', 'S'].map((day, key) => (
                <Text key={`header-${key}`} style={styles.dayHeader}>
                  {day}
                </Text>
              ))}
              {Array.from({ length: offset }, (_, key) => (
                <View key={`empty-${key}`} style={styles.day} />
              ))}
              {Array.from({ length: count }, (_, key) => {
                const day = key + 1;
                const date = `${year}-${String(index + 1).padStart(
                  2,
                  '0',
                )}-${String(day).padStart(2, '0')}`;
                return (
                  <Pressable
                    key={date}
                    accessibilityRole="button"
                    accessibilityLabel={date}
                    onPress={() => {
                      onChange(date);
                      setOpen(false);
                    }}
                    style={[styles.day, value === date && styles.selectedDay]}
                  >
                    <Text
                      style={value === date ? styles.selectedText : layout.body}
                    >
                      {day}
                    </Text>
                  </Pressable>
                );
              })}
            </View>
            <Button
              title="Close calendar"
              secondary
              onPress={() => setOpen(false)}
            />
          </View>
        </View>
      </Modal>
    </View>
  );
}

export function InfoRow({
  label,
  value,
}: {
  label: string;
  value: string;
}): React.JSX.Element {
  return (
    <View style={styles.info}>
      <Text style={layout.muted}>{label}</Text>
      <Text selectable style={styles.value}>
        {value}
      </Text>
    </View>
  );
}
export function StatusBadge({ value }: { value: unknown }): React.JSX.Element {
  const label = status(value);
  const positive = [
    'Approved',
    'Accepted',
    'Settled',
    'Paid',
    'Repaid',
  ].includes(label);
  return (
    <Text style={[styles.badge, positive ? styles.positive : styles.neutral]}>
      {label}
    </Text>
  );
}
export function RecordCard({
  row,
  title,
  children,
}: {
  row: DataRecord;
  title: string;
  children?: React.ReactNode;
}): React.JSX.Element {
  return (
    <View style={layout.card}>
      <View style={layout.row}>
        <Text style={styles.recordTitle}>{title}</Text>
        <StatusBadge value={row.state ?? row.approvalState ?? row.status} />
      </View>
      {children}
      {typeof row.id === 'string' ? (
        <Text selectable style={styles.reference}>
          Reference {row.id}
        </Text>
      ) : null}
    </View>
  );
}
export function ReasonSheet({
  title,
  visible,
  busy,
  onClose,
  onConfirm,
}: {
  title: string;
  visible: boolean;
  busy: boolean;
  onClose(): void;
  onConfirm(reason: string): Promise<void>;
}): React.JSX.Element {
  const [value, setValue] = useState('');
  const [error, setError] = useState('');
  useEffect(() => {
    if (visible) {
      setValue('');
      setError('');
    }
  }, [visible]);
  return (
    <Modal
      visible={visible}
      transparent
      animationType="slide"
      onRequestClose={() => {
        if (!busy) {
          onClose();
        }
      }}
    >
      <KeyboardAvoidingView
        style={styles.scrim}
        behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
      >
        <ScrollView
          keyboardShouldPersistTaps="handled"
          contentContainerStyle={styles.modal}
        >
          <Text style={layout.heading}>{title}</Text>
          <Text style={layout.muted}>
            Your request and its review history will be retained.
          </Text>
          {error ? <Notice message={error} /> : null}
          <Field
            label="Reason"
            value={value}
            onChangeText={setValue}
            multiline
            maxLength={1000}
            editable={!busy}
          />
          <Button
            title="Confirm request"
            busy={busy}
            disabled={!value.trim()}
            onPress={() => {
              setError('');
              onConfirm(value)
                .then(() => {
                  setValue('');
                  onClose();
                })
                .catch(failure => setError(describeError(failure)));
            }}
          />
          <Button title="Close" secondary disabled={busy} onPress={onClose} />
        </ScrollView>
      </KeyboardAvoidingView>
    </Modal>
  );
}

const styles = StyleSheet.create({
  group: { gap: 9 },
  choices: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  choice: {
    borderWidth: 1,
    borderColor: colors.line,
    borderRadius: 12,
    padding: 12,
    backgroundColor: colors.white,
  },
  selected: { borderColor: colors.teal, backgroundColor: colors.teal },
  choiceText: { fontSize: 14, color: colors.navy },
  selectedText: { color: colors.white, fontWeight: '600' },
  dateButton: {
    minHeight: 54,
    borderWidth: 1,
    borderColor: colors.line,
    borderRadius: 12,
    backgroundColor: colors.white,
    padding: 14,
    flexDirection: 'row',
    justifyContent: 'space-between',
  },
  calendarLabel: { color: colors.teal, fontSize: 14, fontWeight: '600' },
  scrim: {
    flex: 1,
    backgroundColor: '#102D4288',
    justifyContent: 'center',
    padding: 24,
  },
  modal: {
    padding: 20,
    borderRadius: 22,
    backgroundColor: colors.white,
    gap: 18,
  },
  calendarGrid: { flexDirection: 'row', flexWrap: 'wrap' },
  dayHeader: {
    width: '14.2857%',
    textAlign: 'center',
    paddingVertical: 10,
    color: colors.muted,
  },
  day: {
    width: '14.2857%',
    minHeight: 42,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: 8,
  },
  selectedDay: { backgroundColor: colors.teal },
  info: { gap: 4 },
  value: { fontSize: 15, lineHeight: 22, color: colors.navy },
  badge: {
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: 8,
    fontSize: 12,
    fontWeight: '600',
    overflow: 'hidden',
    maxWidth: '45%',
  },
  positive: { backgroundColor: colors.mint, color: colors.teal },
  neutral: { backgroundColor: colors.amberSoft, color: colors.amber },
  recordTitle: { flex: 1, fontSize: 17, fontWeight: '700', color: colors.navy },
  reference: { fontSize: 11, color: colors.muted },
});

import React, { useContext } from 'react';
import { BottomTabBarHeightContext } from '@react-navigation/bottom-tabs';
import {
  ActivityIndicator,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
  type TextInputProps,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { colors, layout } from '../theme/theme';

export function Screen({
  children,
}: React.PropsWithChildren): React.JSX.Element {
  const tabBarHeight = useContext(BottomTabBarHeightContext);
  return (
    <SafeAreaView
      style={layout.screen}
      edges={
        tabBarHeight === undefined
          ? ['left', 'right', 'bottom']
          : ['left', 'right']
      }
    >
      <ScrollView
        contentContainerStyle={layout.content}
        keyboardShouldPersistTaps="handled"
        keyboardDismissMode="on-drag"
      >
        {children}
      </ScrollView>
    </SafeAreaView>
  );
}

export function Button({
  title,
  onPress,
  busy = false,
  disabled = false,
  secondary = false,
}: {
  title: string;
  onPress(): void;
  busy?: boolean;
  disabled?: boolean;
  secondary?: boolean;
}): React.JSX.Element {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={title}
      accessibilityState={{ disabled: disabled || busy, busy }}
      disabled={disabled || busy}
      onPress={onPress}
      style={({ pressed }) => [
        styles.button,
        secondary && styles.secondary,
        (disabled || busy) && styles.disabled,
        pressed && styles.pressed,
      ]}
    >
      {busy ? (
        <ActivityIndicator color={secondary ? colors.teal : colors.white} />
      ) : null}
      <Text style={[styles.buttonText, secondary && styles.secondaryText]}>
        {title}
      </Text>
    </Pressable>
  );
}

export function Field({
  label,
  ...props
}: TextInputProps & { label: string }): React.JSX.Element {
  return (
    <View style={styles.field}>
      <Text style={layout.label}>{label}</Text>
      <TextInput
        accessibilityLabel={label}
        placeholderTextColor={colors.muted}
        {...props}
        style={[styles.input, props.multiline && styles.multiline, props.style]}
      />
    </View>
  );
}

export function Notice({
  message,
  tone = 'error',
}: {
  message: string;
  tone?: 'error' | 'info' | 'success';
}): React.JSX.Element {
  return (
    <View
      accessibilityRole="alert"
      accessibilityLiveRegion="polite"
      style={[
        styles.notice,
        tone === 'error'
          ? styles.error
          : tone === 'success'
          ? styles.success
          : styles.info,
      ]}
    >
      <Text style={[layout.body, tone === 'error' && styles.errorText]}>
        {message}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  button: {
    minHeight: 54,
    borderRadius: 14,
    backgroundColor: colors.teal,
    paddingVertical: 15,
    paddingHorizontal: 20,
    alignItems: 'center',
    justifyContent: 'center',
    flexDirection: 'row',
    gap: 10,
  },
  buttonText: { color: colors.white, fontSize: 16, fontWeight: '700' },
  secondary: {
    backgroundColor: colors.white,
    borderWidth: 1,
    borderColor: colors.teal,
  },
  secondaryText: { color: colors.teal },
  disabled: { opacity: 0.5 },
  pressed: { opacity: 0.8 },
  field: { gap: 8 },
  input: {
    minHeight: 54,
    borderWidth: 1,
    borderColor: colors.line,
    borderRadius: 12,
    backgroundColor: colors.white,
    fontSize: 17,
    color: colors.navy,
    paddingHorizontal: 15,
    paddingVertical: 13,
  },
  multiline: { minHeight: 112, maxHeight: 180, textAlignVertical: 'top' },
  notice: { padding: 16, borderRadius: 14 },
  error: { backgroundColor: colors.dangerSoft },
  errorText: { color: colors.danger },
  success: { backgroundColor: colors.mint },
  info: { backgroundColor: colors.amberSoft },
});

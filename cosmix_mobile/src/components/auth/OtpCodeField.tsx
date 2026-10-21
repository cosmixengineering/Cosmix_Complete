import React, { useRef, useState } from 'react';
import { Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { authColors, authStyles } from '../../theme/authTheme';
import { sanitizeOtp } from '../../utils/validation';
import { AuthIcon } from './AuthArtwork';

export function OtpCodeField({
  value,
  onChange,
  disabled,
  onSubmit,
}: {
  value: string;
  onChange(value: string): void;
  disabled: boolean;
  onSubmit(): void;
}): React.JSX.Element {
  const input = useRef<React.ComponentRef<typeof TextInput>>(null);
  const [focused, setFocused] = useState(false);
  const [visible, setVisible] = useState(false);
  return (
    <View style={styles.group}>
      <Pressable
        accessibilityRole="none"
        accessible={false}
        onPress={() => input.current?.focus()}
        style={styles.inputArea}
      >
        <View
          pointerEvents="none"
          accessible={false}
          importantForAccessibility="no-hide-descendants"
          style={styles.cells}
        >
          {Array.from({ length: 6 }, (_, index) => (
            <View
              testID={`otp-cell-${index}`}
              key={index}
              style={[
                styles.cell,
                value[index] && styles.filled,
                focused &&
                  index === Math.min(value.length, 5) &&
                  styles.focused,
              ]}
            >
              <Text style={styles.digit}>
                {value[index] ? (visible ? value[index] : '•') : ''}
              </Text>
              {!value[index] && focused && index === value.length ? (
                <View style={styles.cursor} />
              ) : null}
            </View>
          ))}
        </View>
        <TextInput
          ref={input}
          accessibilityLabel="Six-digit verification code"
          value={value}
          onChangeText={next => onChange(sanitizeOtp(next))}
          keyboardType="number-pad"
          autoComplete="sms-otp"
          textContentType="oneTimeCode"
          autoCorrect={false}
          autoCapitalize="none"
          spellCheck={false}
          secureTextEntry={!visible}
          maxLength={12}
          editable={!disabled}
          caretHidden
          selectionColor="transparent"
          style={styles.input}
          onFocus={() => setFocused(true)}
          onBlur={() => {
            setFocused(false);
            setVisible(false);
          }}
          onSubmitEditing={onSubmit}
          returnKeyType="done"
        />
      </Pressable>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={visible ? 'Hide code' : 'Show code'}
        disabled={disabled}
        onPress={() => setVisible(current => !current)}
        style={styles.visibility}
      >
        <AuthIcon name={visible ? 'eye-off' : 'eye'} size={17} />
        <Text style={authStyles.caption}>
          {visible ? 'Hide code' : 'Show code'}
        </Text>
      </Pressable>
    </View>
  );
}
const styles = StyleSheet.create({
  group: { gap: 2 },
  inputArea: { justifyContent: 'center', minHeight: 52 },
  cells: { flexDirection: 'row', gap: 8 },
  cell: {
    flex: 1,
    aspectRatio: 1,
    borderRadius: 13,
    borderWidth: 1.2,
    borderColor: authColors.line,
    backgroundColor: '#FCFBFF',
    alignItems: 'center',
    justifyContent: 'center',
  },
  filled: { backgroundColor: authColors.soft, borderColor: '#A5A0C9' },
  focused: { borderColor: authColors.primary, borderWidth: 2 },
  digit: { color: authColors.primary, fontSize: 24, fontWeight: '600' },
  cursor: {
    width: 2,
    height: 20,
    borderRadius: 1,
    backgroundColor: authColors.primary,
  },
  input: {
    position: 'absolute',
    top: 0,
    bottom: 0,
    left: 0,
    right: 0,
    color: 'transparent',
    backgroundColor: 'transparent',
    fontSize: 1,
    padding: 0,
  },
  visibility: {
    alignSelf: 'flex-end',
    minHeight: 44,
    flexDirection: 'row',
    gap: 6,
    alignItems: 'center',
    paddingHorizontal: 4,
  },
});

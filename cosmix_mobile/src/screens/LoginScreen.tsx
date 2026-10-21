import React, { useEffect, useRef, useState } from 'react';
import { Keyboard, StyleSheet, Text, TextInput, View } from 'react-native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useServices, useSession } from '../app/AppProvider';
import { Notice } from '../components/Controls';
import { AuthFrame } from '../components/auth/AuthFrame';
import { AuthButton } from '../components/auth/AuthButton';
import { AuthIcon, PakistanFlag } from '../components/auth/AuthArtwork';
import { AuthBrandLogo } from '../components/auth/AuthBrandLogo';
import {
  AuthFieldFrame,
  AuthFormatIndicator,
} from '../components/auth/AuthFieldFrame';
import { NeedHelpLink, NeedHelpSheet } from '../components/auth/NeedHelpSheet';
import type { RootStackParams } from '../navigation/types';
import { authColors, authStyles } from '../theme/authTheme';
import { describeError, messageForCode } from '../utils/errors';
import { normalizePakistanPhoneNumber } from '../utils/validation';

export function LoginScreen({
  navigation,
}: NativeStackScreenProps<RootStackParams, 'Login'>): React.JSX.Element {
  const { session } = useServices();
  const auth = useSession();
  const [phone, setPhone] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [focused, setFocused] = useState(false);
  const [help, setHelp] = useState(false);
  const flight = useRef(false);
  const mounted = useRef(true);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);
  let valid = false;
  try {
    normalizePakistanPhoneNumber(phone);
    valid = true;
  } catch {
    /* Format is checked again on submit. */
  }
  const changePhone = (value: string) => {
    setError('');
    try {
      setPhone(normalizePakistanPhoneNumber(value).slice(3));
    } catch {
      setPhone(value);
    }
  };
  const submit = async () => {
    if (flight.current) return;
    flight.current = true;
    setBusy(true);
    setError('');
    try {
      const phoneNumber = normalizePakistanPhoneNumber(phone);
      Keyboard.dismiss();
      const challenge = await session.requestOtp(phoneNumber);
      if (mounted.current)
        navigation.navigate('VerifyCode', { phoneNumber, challenge });
    } catch (failure) {
      if (mounted.current) setError(describeError(failure));
    } finally {
      flight.current = false;
      if (mounted.current) setBusy(false);
    }
  };
  return (
    <AuthFrame>
      <View style={styles.header}>
        <View style={[authStyles.stack, styles.headerText]}>
          <Text style={styles.brand}>COSMIX</Text>
          <Text accessibilityRole="header" style={authStyles.title}>
            Login account
          </Text>
        </View>
        <View accessibilityLabel="Pakistan" style={styles.country}>
          <PakistanFlag />
          <Text style={styles.countryLabel}>PK</Text>
        </View>
      </View>
      <Text style={authStyles.body}>
        Hello, welcome back to your workspace.
      </Text>
      <AuthBrandLogo />
      <View style={styles.form}>
        <AuthFieldFrame label="Phone number" focused={focused} disabled={busy}>
          <View style={styles.phone}>
            <View
              accessibilityLabel="Pakistan calling code +92"
              style={styles.prefix}
            >
              <PakistanFlag size={24} />
              <Text style={styles.code}>+92</Text>
            </View>
            <TextInput
              accessibilityLabel="Mobile number"
              value={phone}
              onChangeText={changePhone}
              placeholder="300 1234567"
              placeholderTextColor="#9B99AD"
              selectionColor={authColors.primary}
              keyboardType="phone-pad"
              textContentType="telephoneNumber"
              autoComplete="tel"
              autoCorrect={false}
              maxLength={32}
              editable={!busy}
              returnKeyType="done"
              onSubmitEditing={() => submit()}
              onFocus={() => setFocused(true)}
              onBlur={() => setFocused(false)}
              style={styles.input}
            />
            <AuthFormatIndicator visible={valid} />
          </View>
        </AuthFieldFrame>
        <Text style={authStyles.caption}>
          Use the mobile number registered with HR.
        </Text>
        {error ? (
          <Notice message={error} />
        ) : auth.message ? (
          <Notice message={messageForCode(auth.message)} tone="info" />
        ) : null}
        <AuthButton
          title={busy ? 'Sending code…' : 'Send code'}
          accessibilityLabel="Send code"
          onPress={submit}
          busy={busy}
          disabled={!valid}
        />
        <Text style={[authStyles.caption, authStyles.centered]}>
          If your number is eligible, you’ll receive a six-digit code by SMS.
        </Text>
      </View>
      <View style={styles.footer}>
        <View style={styles.divider} />
        <NeedHelpLink onPress={() => setHelp(true)} disabled={busy} />
        <View style={styles.protected}>
          <AuthIcon name="lock" size={14} color={authColors.muted} />
          <Text style={authStyles.caption}>Protected employee access</Text>
        </View>
      </View>
      <NeedHelpSheet visible={help} onClose={() => setHelp(false)} />
    </AuthFrame>
  );
}
const styles = StyleSheet.create({
  headerText: { flex: 1, gap: 10 },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: 12,
  },
  brand: {
    fontSize: 11,
    fontWeight: '800',
    color: authColors.primary,
    letterSpacing: 2.6,
  },
  country: {
    flexDirection: 'row',
    gap: 6,
    alignItems: 'center',
    backgroundColor: authColors.soft,
    padding: 7,
    borderRadius: 20,
  },
  countryLabel: { color: authColors.ink, fontSize: 11, fontWeight: '700' },
  form: { gap: 15 },
  phone: {
    minHeight: 58,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    borderRadius: 16,
    borderWidth: 1,
    borderColor: authColors.line,
    paddingHorizontal: 12,
    backgroundColor: '#FDFCFF',
  },
  prefix: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 7,
    borderRightWidth: 1,
    borderRightColor: authColors.line,
    paddingRight: 11,
  },
  code: { color: authColors.ink, fontSize: 15, fontWeight: '600' },
  input: {
    flex: 1,
    minWidth: 0,
    color: authColors.ink,
    fontSize: 17,
    minHeight: 56,
    paddingVertical: 12,
    paddingHorizontal: 0,
  },
  footer: { marginTop: 'auto', gap: 12, paddingTop: 18 },
  divider: { height: 1, backgroundColor: authColors.line },
  protected: {
    flexDirection: 'row',
    gap: 6,
    justifyContent: 'center',
    alignItems: 'center',
  },
});

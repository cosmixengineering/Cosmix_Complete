import React, { useEffect, useRef, useState } from 'react';
import {
  AppState,
  BackHandler,
  Keyboard,
  Pressable,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useServices } from '../app/AppProvider';
import { Notice } from '../components/Controls';
import { AuthFrame } from '../components/auth/AuthFrame';
import { AuthButton } from '../components/auth/AuthButton';
import { AuthIcon, AuthSecurityArtwork } from '../components/auth/AuthArtwork';
import { OtpCodeField } from '../components/auth/OtpCodeField';
import { NeedHelpLink, NeedHelpSheet } from '../components/auth/NeedHelpSheet';
import type { RootStackParams } from '../navigation/types';
import { authColors, authStyles } from '../theme/authTheme';
import { describeError, messageForCode } from '../utils/errors';

export function VerifyCodeScreen({
  route,
  navigation,
}: NativeStackScreenProps<RootStackParams, 'VerifyCode'>): React.JSX.Element {
  const { session } = useServices();
  const [challenge, setChallenge] = useState(route.params.challenge);
  const [code, setCode] = useState('');
  const [busy, setBusy] = useState<'verify' | 'resend' | null>(null);
  const [error, setError] = useState('');
  const [help, setHelp] = useState(false);
  const [clock, setClock] = useState(Date.now());
  const flight = useRef(false);
  const mounted = useRef(true);
  useEffect(() => {
    mounted.current = true;
    const timer = setInterval(() => setClock(Date.now()), 1000);
    const subscription = AppState.addEventListener('change', next => {
      if (next === 'active') setClock(Date.now());
    });
    return () => {
      mounted.current = false;
      clearInterval(timer);
      subscription.remove();
    };
  }, []);
  useEffect(() => {
    navigation.setOptions({ gestureEnabled: busy === null });
    if (!busy) return;
    const subscription = BackHandler.addEventListener(
      'hardwareBackPress',
      () => true,
    );
    return () => subscription.remove();
  }, [busy, navigation]);
  const expired = clock >= Date.parse(challenge.expiresAt);
  const remaining = Math.max(
    0,
    Math.ceil((Date.parse(challenge.resendAvailableAt) - clock) / 1000),
  );
  const run = async (operation: 'verify' | 'resend') => {
    if (flight.current) return;
    const now = Date.now();
    if (
      operation === 'verify' &&
      (code.length !== 6 || now >= Date.parse(challenge.expiresAt))
    ) {
      setClock(now);
      setError(
        messageForCode(code.length !== 6 ? 'invalid_otp' : 'otp_expired'),
      );
      return;
    }
    if (operation === 'resend' && now < Date.parse(challenge.resendAvailableAt))
      return;
    flight.current = true;
    setBusy(operation);
    setError('');
    Keyboard.dismiss();
    try {
      if (operation === 'verify') {
        await session.verifyOtp(challenge.challengeId, code);
        if (mounted.current) setCode('');
      } else {
        const next = await session.requestOtp(
          route.params.phoneNumber,
          challenge.challengeId,
        );
        if (mounted.current) {
          setChallenge(next);
          setCode('');
          setClock(Date.now());
        }
      }
    } catch (failure) {
      if (mounted.current) setError(describeError(failure));
    } finally {
      flight.current = false;
      if (mounted.current) setBusy(null);
    }
  };
  const changePhone = () => {
    if (!flight.current) {
      setCode('');
      navigation.goBack();
    }
  };
  const phone = route.params.phoneNumber.replace(
    /^(\+92)(\d{3})(\d{7})$/,
    '$1 $2 $3',
  );
  return (
    <AuthFrame>
      <View style={styles.header}>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Back to login"
          accessibilityState={{ disabled: busy !== null }}
          disabled={busy !== null}
          onPress={changePhone}
          style={styles.back}
        >
          <AuthIcon name="back" size={19} />
        </Pressable>
        <Text style={styles.headerTitle}>Verification</Text>
        <View style={styles.headerSpacer} />
      </View>
      <AuthSecurityArtwork />
      <View style={authStyles.stack}>
        <Text
          accessibilityRole="header"
          style={[authStyles.heading, authStyles.centered]}
        >
          Verification code
        </Text>
        <Text style={[authStyles.body, authStyles.centered]}>
          If your number is eligible, an SMS with a six-digit code will arrive
          shortly.
        </Text>
      </View>
      <OtpCodeField
        key={challenge.challengeId}
        value={code}
        onChange={value => {
          setCode(value);
          setError('');
        }}
        disabled={busy !== null}
        onSubmit={() => run('verify')}
      />
      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Change phone number"
        accessibilityState={{ disabled: busy !== null }}
        disabled={busy !== null}
        onPress={changePhone}
        style={styles.destination}
      >
        <Text style={styles.number}>{phone}</Text>
        <View style={styles.edit}>
          <AuthIcon name="edit" size={16} />
        </View>
      </Pressable>
      {error ? <Notice message={error} /> : null}
      {expired && !error ? (
        <Notice message={messageForCode('otp_expired')} tone="info" />
      ) : null}
      <View style={styles.actions}>
        <AuthButton
          secondary
          title={remaining ? `Send again · ${remaining}s` : 'Send again'}
          accessibilityLabel="Send again"
          accessibilityHint={
            remaining
              ? `Available in ${remaining} seconds`
              : 'Request a new SMS verification code'
          }
          busy={busy === 'resend'}
          disabled={busy !== null || remaining > 0}
          onPress={() => run('resend')}
        />
        <AuthButton
          title="Verify and sign in"
          busy={busy === 'verify'}
          disabled={busy !== null || code.length !== 6 || expired}
          onPress={() => run('verify')}
        />
      </View>
      <View style={styles.footer}>
        <Text style={[authStyles.caption, authStyles.centered]}>
          Keep your code private. Cosmix will never ask you to share it.
        </Text>
        <NeedHelpLink disabled={busy !== null} onPress={() => setHelp(true)} />
      </View>
      <NeedHelpSheet visible={help} onClose={() => setHelp(false)} />
    </AuthFrame>
  );
}
const styles = StyleSheet.create({
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: 10,
  },
  back: {
    width: 44,
    height: 44,
    borderRadius: 22,
    borderWidth: 1,
    borderColor: authColors.line,
    alignItems: 'center',
    justifyContent: 'center',
  },
  headerTitle: {
    flex: 1,
    textAlign: 'center',
    fontSize: 16,
    fontWeight: '600',
    color: authColors.ink,
  },
  headerSpacer: { width: 44 },
  destination: {
    minHeight: 44,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 10,
  },
  number: {
    color: authColors.primary,
    fontSize: 16,
    fontWeight: '600',
    flexShrink: 1,
  },
  edit: {
    width: 30,
    height: 30,
    borderRadius: 15,
    borderWidth: 1,
    borderColor: authColors.line,
    alignItems: 'center',
    justifyContent: 'center',
  },
  actions: { gap: 12 },
  footer: { marginTop: 'auto', gap: 4 },
});

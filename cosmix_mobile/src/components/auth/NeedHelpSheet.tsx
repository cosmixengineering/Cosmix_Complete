import React from 'react';
import {
  Keyboard,
  Modal,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { authColors, authStyles } from '../../theme/authTheme';
import { AuthButton } from './AuthButton';
import { AuthIcon } from './AuthArtwork';
import { useAuthMotion } from './AuthFrame';

export function NeedHelpSheet({
  visible,
  onClose,
}: {
  visible: boolean;
  onClose(): void;
}): React.JSX.Element {
  const motion = useAuthMotion();
  return (
    <Modal
      visible={visible}
      transparent
      animationType={motion ? 'fade' : 'none'}
      onRequestClose={onClose}
    >
      <SafeAreaView style={styles.scrim}>
        <View accessibilityViewIsModal style={styles.sheet}>
          <ScrollView contentContainerStyle={styles.content}>
            <View style={styles.icon}>
              <AuthIcon name="help" size={28} />
            </View>
            <Text accessibilityRole="header" style={authStyles.heading}>
              Let’s get you signed in.
            </Text>
            <Text style={authStyles.body}>
              Your HR team or supervisor can help with employee access.
            </Text>
            <View style={authStyles.stack}>
              <Text style={authStyles.label}>Check your registered number</Text>
              <Text style={authStyles.body}>
                Use the Pakistan mobile number recorded with HR. This app is for
                existing company accounts.
              </Text>
            </View>
            <View style={authStyles.stack}>
              <Text style={authStyles.label}>Waiting for a code?</Text>
              <Text style={authStyles.body}>
                Check your mobile signal and SMS inbox. When the countdown ends,
                select Send again. Never share the code.
              </Text>
            </View>
            <View style={authStyles.stack}>
              <Text style={authStyles.label}>Still unable to sign in?</Text>
              <Text style={authStyles.body}>
                Ask HR to check your account, SMS delivery and device approval.
                Only your company can update your registered number or access.
              </Text>
            </View>
            <AuthButton title="Got it" onPress={onClose} />
          </ScrollView>
        </View>
      </SafeAreaView>
    </Modal>
  );
}

export function NeedHelpLink({
  onPress,
  disabled = false,
}: {
  onPress(): void;
  disabled?: boolean;
}): React.JSX.Element {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel="Need help"
      accessibilityState={{ disabled }}
      disabled={disabled}
      onPress={() => {
        Keyboard.dismiss();
        onPress();
      }}
      style={({ pressed }) => [styles.help, pressed && styles.pressed]}
    >
      <Text style={authStyles.caption}>Having trouble signing in? </Text>
      <Text style={authStyles.link}>Need help</Text>
    </Pressable>
  );
}
const styles = StyleSheet.create({
  scrim: {
    flex: 1,
    padding: 20,
    justifyContent: 'center',
    backgroundColor: 'rgba(25, 21, 59, 0.4)',
  },
  sheet: {
    width: '100%',
    maxWidth: 460,
    maxHeight: '95%',
    alignSelf: 'center',
    borderRadius: 28,
    backgroundColor: '#FFFFFF',
  },
  content: { padding: 24, gap: 20 },
  icon: {
    width: 52,
    height: 52,
    borderRadius: 18,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: authColors.soft,
  },
  help: {
    minHeight: 44,
    alignItems: 'center',
    justifyContent: 'center',
    flexDirection: 'row',
    flexWrap: 'wrap',
    paddingVertical: 10,
  },
  pressed: { opacity: 0.7 },
});

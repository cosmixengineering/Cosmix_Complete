import React, { useEffect, useRef } from 'react';
import { Animated, Easing, StyleSheet, View } from 'react-native';
import { authColors, authStyles } from '../../theme/authTheme';
import { AuthIcon } from './AuthArtwork';
import { useAuthMotion } from './AuthFrame';

export function AuthFieldFrame({
  label,
  focused,
  disabled = false,
  children,
}: React.PropsWithChildren<{
  label: string;
  focused: boolean;
  disabled?: boolean;
}>): React.JSX.Element {
  const motion = useAuthMotion();
  const focus = useRef(new Animated.Value(0)).current;
  const active = focused && !disabled;
  useEffect(() => {
    const target = active ? 1 : 0;
    if (!motion) {
      focus.setValue(target);
      return;
    }
    const animation = Animated.timing(focus, {
      toValue: target,
      duration: 220,
      easing: Easing.out(Easing.quad),
      useNativeDriver: true,
      isInteraction: false,
    });
    animation.start();
    return () => animation.stop();
  }, [active, motion, focus]);
  return (
    <View style={styles.group}>
      <Animated.Text
        style={[
          authStyles.label,
          active && styles.activeLabel,
          {
            transform: [
              {
                translateX: focus.interpolate({
                  inputRange: [0, 1],
                  outputRange: [0, 2],
                }),
              },
              {
                translateY: focus.interpolate({
                  inputRange: [0, 1],
                  outputRange: [0, -2],
                }),
              },
            ],
          },
        ]}
      >
        {label}
      </Animated.Text>
      <View>
        <Animated.View
          pointerEvents="none"
          style={[
            styles.glow,
            {
              opacity: focus,
              transform: [
                {
                  scale: focus.interpolate({
                    inputRange: [0, 1],
                    outputRange: [0.98, 1.015],
                  }),
                },
              ],
            },
          ]}
        />
        {children}
        <Animated.View
          testID="auth-field-focus-ring"
          pointerEvents="none"
          style={[styles.ring, { opacity: focus }]}
        />
      </View>
    </View>
  );
}

export function AuthFormatIndicator({
  visible,
}: {
  visible: boolean;
}): React.JSX.Element {
  const motion = useAuthMotion();
  const progress = useRef(new Animated.Value(visible ? 1 : 0)).current;
  useEffect(() => {
    const target = visible ? 1 : 0;
    if (!motion) {
      progress.setValue(target);
      return;
    }
    const animation = Animated.spring(progress, {
      toValue: target,
      damping: 16,
      stiffness: 220,
      mass: 0.7,
      useNativeDriver: true,
      isInteraction: false,
    });
    animation.start();
    return () => animation.stop();
  }, [visible, motion, progress]);
  return (
    <Animated.View
      accessible={visible}
      accessibilityLabel={visible ? 'Number format complete' : undefined}
      importantForAccessibility={visible ? 'auto' : 'no-hide-descendants'}
      style={[
        styles.check,
        {
          opacity: progress,
          transform: [
            {
              scale: progress.interpolate({
                inputRange: [0, 1],
                outputRange: [0.7, 1],
              }),
            },
          ],
        },
      ]}
    >
      <AuthIcon name="check" size={14} color="#FFFFFF" />
    </Animated.View>
  );
}

const styles = StyleSheet.create({
  group: { gap: 15 },
  activeLabel: { color: authColors.primary },
  glow: {
    position: 'absolute',
    top: -5,
    bottom: -5,
    left: -5,
    right: -5,
    borderRadius: 21,
    backgroundColor: '#EEEBFF',
  },
  ring: {
    position: 'absolute',
    top: 0,
    bottom: 0,
    left: 0,
    right: 0,
    borderRadius: 16,
    borderWidth: 1.8,
    borderColor: '#6D5BC0',
    boxShadow: '0 3px 14px rgba(91, 72, 164, 0.12)',
  },
  check: {
    width: 19,
    height: 19,
    borderRadius: 10,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: authColors.green,
  },
});

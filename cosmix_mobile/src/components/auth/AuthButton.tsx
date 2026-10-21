import React, { useEffect, useRef, useState } from 'react';
import {
  ActivityIndicator,
  Animated,
  Easing,
  Pressable,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import Svg, { Defs, LinearGradient, Rect, Stop } from 'react-native-svg';
import { authColors } from '../../theme/authTheme';
import { useAuthMotion } from './AuthFrame';

function ButtonGradient(): React.JSX.Element {
  const [size, setSize] = useState({ width: 0, height: 0 });
  return (
    <View
      pointerEvents="none"
      accessible={false}
      style={styles.gradient}
      onLayout={({ nativeEvent: { layout } }) =>
        setSize(previous =>
          previous.width === layout.width && previous.height === layout.height
            ? previous
            : { width: layout.width, height: layout.height },
        )
      }
    >
      {size.width > 0 && size.height > 0 ? (
        <Svg
          width={size.width}
          height={size.height}
          viewBox="0 0 320 56"
          preserveAspectRatio="none"
        >
          <Defs>
            <LinearGradient
              id="authPrimaryButton"
              x1="0%"
              y1="0%"
              x2="100%"
              y2="100%"
            >
              <Stop offset="0%" stopColor="#5746A3" />
              <Stop offset="100%" stopColor="#30266E" />
            </LinearGradient>
          </Defs>
          <Rect width={320} height={56} fill="url(#authPrimaryButton)" />
        </Svg>
      ) : null}
    </View>
  );
}

export function AuthButton({
  title,
  accessibilityLabel = title,
  accessibilityHint,
  onPress,
  busy = false,
  disabled = false,
  secondary = false,
}: {
  title: string;
  accessibilityLabel?: string;
  accessibilityHint?: string;
  onPress(): void;
  busy?: boolean;
  disabled?: boolean;
  secondary?: boolean;
}): React.JSX.Element {
  const motion = useAuthMotion();
  const scale = useRef(new Animated.Value(1)).current;
  useEffect(() => {
    if (!motion || disabled || busy) {
      scale.stopAnimation();
      scale.setValue(1);
    }
  }, [motion, disabled, busy, scale]);
  useEffect(() => () => scale.stopAnimation(), [scale]);
  const animate = (value: number) => {
    if (!motion) return;
    Animated.timing(scale, {
      toValue: value,
      duration: 110,
      easing: Easing.out(Easing.quad),
      useNativeDriver: true,
      isInteraction: false,
    }).start();
  };
  return (
    <Animated.View
      style={[
        !secondary && !disabled && !busy && styles.shadow,
        { transform: [{ scale }] },
      ]}
    >
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={accessibilityLabel}
        accessibilityHint={accessibilityHint}
        accessibilityState={{ disabled: disabled || busy, busy }}
        disabled={disabled || busy}
        onPress={onPress}
        onPressIn={() => animate(0.975)}
        onPressOut={() => animate(1)}
        android_ripple={
          motion
            ? {
                color: secondary
                  ? 'rgba(57, 50, 124, 0.10)'
                  : 'rgba(255, 255, 255, 0.18)',
                foreground: true,
              }
            : undefined
        }
        style={({ pressed }) => [
          styles.button,
          secondary && styles.secondary,
          (disabled || busy) && styles.disabled,
          pressed && styles.pressed,
        ]}
      >
        {!secondary ? <ButtonGradient /> : null}
        {busy ? (
          <ActivityIndicator
            color={secondary ? authColors.primary : '#FFFFFF'}
          />
        ) : null}
        <Text style={[styles.title, secondary && styles.secondaryTitle]}>
          {title}
        </Text>
      </Pressable>
    </Animated.View>
  );
}
const styles = StyleSheet.create({
  button: {
    overflow: 'hidden',
    minHeight: 54,
    borderRadius: 17,
    backgroundColor: authColors.primary,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 9,
    paddingHorizontal: 16,
    paddingVertical: 15,
  },
  shadow: { borderRadius: 17, boxShadow: '0 6px 15px rgba(57, 50, 124, 0.18)' },
  gradient: { position: 'absolute', top: 0, bottom: 0, left: 0, right: 0 },
  title: {
    color: '#FFFFFF',
    fontSize: 16,
    fontWeight: '600',
    textAlign: 'center',
    flexShrink: 1,
  },
  secondary: {
    backgroundColor: '#FFFFFF',
    borderWidth: 1,
    borderColor: authColors.line,
  },
  secondaryTitle: { color: authColors.primary },
  disabled: { opacity: 0.5 },
  pressed: { opacity: 0.85 },
});

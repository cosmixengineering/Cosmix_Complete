import React, { useEffect, useRef } from 'react';
import { Animated, Easing, StyleSheet, View } from 'react-native';
import Svg, { Circle, ClipPath, Defs, G, Path, Rect } from 'react-native-svg';
import { authColors } from '../../theme/authTheme';
import { useAuthMotion } from './AuthFrame';

type IconName =
  | 'back'
  | 'lock'
  | 'phone-lock'
  | 'edit'
  | 'eye'
  | 'eye-off'
  | 'check'
  | 'help';
export function AuthIcon({
  name,
  size = 22,
  color = authColors.primary,
}: {
  name: IconName;
  size?: number;
  color?: string;
}): React.JSX.Element {
  return (
    <Svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke={color}
      strokeWidth={1.7}
      strokeLinecap="round"
      strokeLinejoin="round"
      accessible={false}
    >
      {name === 'back' ? <Path d="m14 6-6 6 6 6" /> : null}
      {name === 'edit' ? (
        <>
          <Path d="m14 5 5 5M5 19l4-1 11-11a2.8 2.8 0 0 0-4-4L5 14l-1 6 6-1" />
          <Path d="M13 21h8" />
        </>
      ) : null}
      {name === 'lock' ? (
        <>
          <Rect x={5} y={10} width={14} height={11} rx={3} />
          <Path d="M8 10V7a4 4 0 0 1 8 0v3m-4 5v2" />
        </>
      ) : null}
      {name === 'phone-lock' ? (
        <>
          <Path d="M11 21H5V3h12v6M8 18h2" />
          <Rect x={13} y={15} width={9} height={7} rx={1.5} />
          <Path d="M15 15v-2a2.5 2.5 0 0 1 5 0v2m-2.5 3v1" />
        </>
      ) : null}
      {name === 'check' ? <Path d="m5 12 4 4L19 6" /> : null}
      {name === 'help' ? (
        <>
          <Circle cx={12} cy={12} r={9} />
          <Path d="M9.5 9a2.5 2.5 0 1 1 4.4 1.6c-1.1.8-1.9 1.2-1.9 2.9m0 3h.01" />
        </>
      ) : null}
      {name === 'eye' || name === 'eye-off' ? (
        <>
          <Path d="M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12Z" />
          <Circle cx={12} cy={12} r={3} />
          {name === 'eye-off' ? <Path d="m3 3 18 18" /> : null}
        </>
      ) : null}
    </Svg>
  );
}

export function PakistanFlag({
  size = 26,
}: {
  size?: number;
}): React.JSX.Element {
  return (
    <Svg width={size} height={size} viewBox="0 0 32 32" accessible={false}>
      <Defs>
        <ClipPath id="pakistanFlag">
          <Circle cx={16} cy={16} r={15} />
        </ClipPath>
      </Defs>
      <G clipPath="url(#pakistanFlag)">
        <Rect width={32} height={32} fill="#FFFFFF" />
        <Rect x={8} width={24} height={32} fill="#075B35" />
        <Circle cx={20} cy={18} r={8} fill="#FFFFFF" />
        <Circle cx={23} cy={15} r={7} fill="#075B35" />
        <Path
          d="m25 5 1.1 2.6 2.8.2-2.1 1.9.6 2.8-2.4-1.5-2.4 1.5.7-2.8-2.2-1.9 2.8-.2Z"
          fill="#FFFFFF"
        />
      </G>
      <Circle
        cx={16}
        cy={16}
        r={15}
        stroke="#E2E5EA"
        strokeWidth={0.7}
        fill="none"
      />
    </Svg>
  );
}

export function AuthSecurityArtwork({
  compact = false,
}: {
  compact?: boolean;
}): React.JSX.Element {
  const motion = useAuthMotion();
  const pulse = useRef(new Animated.Value(0)).current;
  useEffect(() => {
    if (!motion) {
      pulse.setValue(0);
      return;
    }
    const config = {
      duration: 1800,
      easing: Easing.inOut(Easing.sin),
      useNativeDriver: true,
      isInteraction: false,
    };
    const animation = Animated.loop(
      Animated.sequence([
        Animated.timing(pulse, { ...config, toValue: 1 }),
        Animated.timing(pulse, { ...config, toValue: 0 }),
      ]),
    );
    animation.start();
    return () => animation.stop();
  }, [motion, pulse]);
  const size = compact ? 124 : 148;
  return (
    <View
      accessible={false}
      importantForAccessibility="no-hide-descendants"
      style={[styles.art, { width: size, height: size }]}
    >
      <Animated.View
        style={[
          styles.orbit,
          {
            width: size,
            height: size,
            borderRadius: size / 2,
            opacity: pulse.interpolate({
              inputRange: [0, 1],
              outputRange: [0.65, 1],
            }),
            transform: [
              {
                scale: pulse.interpolate({
                  inputRange: [0, 1],
                  outputRange: [0.94, 1.04],
                }),
              },
            ],
          },
        ]}
      />
      <View
        style={[
          styles.halo,
          { width: size - 28, height: size - 28, borderRadius: size },
        ]}
      />
      <View style={[styles.center, compact && styles.compact]}>
        <AuthIcon name="phone-lock" size={compact ? 32 : 38} color="#FFFFFF" />
      </View>
    </View>
  );
}
const styles = StyleSheet.create({
  art: {
    alignSelf: 'center',
    alignItems: 'center',
    justifyContent: 'center',
    marginVertical: 4,
  },
  orbit: {
    position: 'absolute',
    borderWidth: 1,
    borderColor: '#DAD7EC',
    borderStyle: 'dashed',
  },
  halo: { position: 'absolute', backgroundColor: '#F7F6FD' },
  center: {
    width: 84,
    height: 84,
    borderRadius: 42,
    backgroundColor: authColors.primary,
    alignItems: 'center',
    justifyContent: 'center',
    boxShadow: '0 8px 20px rgba(57, 50, 124, 0.13)',
  },
  compact: { width: 72, height: 72, borderRadius: 36 },
});

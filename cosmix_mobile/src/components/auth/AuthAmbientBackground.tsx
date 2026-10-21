import React, { useEffect, useRef } from 'react';
import { Animated, Easing, StyleSheet, View } from 'react-native';

export function AuthAmbientBackground({
  motion,
}: {
  motion: boolean;
}): React.JSX.Element {
  const drift = useRef(new Animated.Value(0)).current;
  useEffect(() => {
    if (!motion) {
      drift.setValue(0);
      return;
    }
    const config = {
      duration: 7000,
      easing: Easing.inOut(Easing.sin),
      useNativeDriver: true,
      isInteraction: false,
    };
    const animation = Animated.loop(
      Animated.sequence([
        Animated.timing(drift, { ...config, toValue: 1 }),
        Animated.timing(drift, { ...config, toValue: 0 }),
      ]),
    );
    animation.start();
    return () => animation.stop();
  }, [motion, drift]);
  return (
    <View pointerEvents="none" style={styles.background}>
      <Animated.View
        style={[
          styles.top,
          {
            transform: [
              {
                translateX: drift.interpolate({
                  inputRange: [0, 1],
                  outputRange: [0, 18],
                }),
              },
              {
                translateY: drift.interpolate({
                  inputRange: [0, 1],
                  outputRange: [0, 12],
                }),
              },
            ],
          },
        ]}
      />
      <Animated.View
        style={[
          styles.bottom,
          {
            transform: [
              {
                translateX: drift.interpolate({
                  inputRange: [0, 1],
                  outputRange: [0, -16],
                }),
              },
              {
                translateY: drift.interpolate({
                  inputRange: [0, 1],
                  outputRange: [0, -18],
                }),
              },
            ],
          },
        ]}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  background: {
    position: 'absolute',
    top: 0,
    bottom: 0,
    left: 0,
    right: 0,
    overflow: 'hidden',
  },
  top: {
    position: 'absolute',
    width: 440,
    height: 440,
    borderRadius: 220,
    backgroundColor: '#E2E4F2',
    top: -100,
    left: -160,
  },
  bottom: {
    position: 'absolute',
    width: 350,
    height: 350,
    borderRadius: 175,
    backgroundColor: '#E6E1F5',
    bottom: -100,
    right: -150,
    opacity: 0.65,
  },
});

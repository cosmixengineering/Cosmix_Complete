import React, { useEffect, useRef } from 'react';
import {
  Animated,
  Easing,
  Image,
  StyleSheet,
  View,
  type ImageSourcePropType,
} from 'react-native';
import { useAuthMotion } from './AuthFrame';

const logo: ImageSourcePropType = require('../../assets/images/cosmix-logo-hd.png');

export function AuthBrandLogo(): React.JSX.Element {
  const motion = useAuthMotion();
  const entrance = useRef(new Animated.Value(1)).current;
  const drift = useRef(new Animated.Value(0)).current;
  useEffect(() => {
    if (!motion) {
      entrance.setValue(1);
      drift.setValue(0);
      return;
    }
    entrance.setValue(0);
    const reveal = Animated.timing(entrance, {
      toValue: 1,
      duration: 650,
      easing: Easing.out(Easing.cubic),
      useNativeDriver: true,
      isInteraction: false,
    });
    const config = {
      duration: 2600,
      easing: Easing.inOut(Easing.sin),
      useNativeDriver: true,
      isInteraction: false,
    };
    const float = Animated.loop(
      Animated.sequence([
        Animated.timing(drift, { ...config, toValue: 1 }),
        Animated.timing(drift, { ...config, toValue: 0 }),
      ]),
    );
    reveal.start();
    float.start();
    return () => {
      reveal.stop();
      float.stop();
    };
  }, [motion, entrance, drift]);
  return (
    <View style={styles.frame}>
      <Animated.View
        pointerEvents="none"
        style={[
          styles.glow,
          {
            opacity: drift.interpolate({
              inputRange: [0, 1],
              outputRange: [0.35, 0.65],
            }),
            transform: [
              {
                scale: drift.interpolate({
                  inputRange: [0, 1],
                  outputRange: [0.96, 1.06],
                }),
              },
            ],
          },
        ]}
      />
      <Animated.View
        style={[
          styles.logo,
          {
            transform: [
              {
                translateY: entrance.interpolate({
                  inputRange: [0, 1],
                  outputRange: [14, 0],
                }),
              },
              {
                scale: entrance.interpolate({
                  inputRange: [0, 1],
                  outputRange: [0.94, 1],
                }),
              },
            ],
          },
        ]}
      >
        <Animated.View
          style={[
            styles.imageFrame,
            {
              transform: [
                {
                  translateY: drift.interpolate({
                    inputRange: [0, 1],
                    outputRange: [0, -4],
                  }),
                },
              ],
            },
          ]}
        >
          <Image
            source={logo}
            accessibilityLabel="Cosmix Engineering logo"
            accessible
            resizeMode="contain"
            fadeDuration={0}
            style={styles.image}
          />
        </Animated.View>
      </Animated.View>
    </View>
  );
}

const styles = StyleSheet.create({
  frame: {
    minHeight: 142,
    alignItems: 'center',
    justifyContent: 'center',
    marginVertical: 6,
  },
  logo: { width: '100%', maxWidth: 300 },
  imageFrame: { width: '100%', height: 124 },
  image: { width: '100%', height: '100%', backgroundColor: 'transparent' },
  glow: {
    position: 'absolute',
    width: '85%',
    height: 92,
    borderRadius: 100,
    backgroundColor: '#F1EEFB',
  },
});

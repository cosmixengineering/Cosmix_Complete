import React, {
  createContext,
  useContext,
  useEffect,
  useRef,
  useState,
} from 'react';
import {
  Animated,
  AppState,
  Easing,
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  StyleSheet,
  useWindowDimensions,
} from 'react-native';
import {
  SafeAreaView,
  useSafeAreaInsets,
} from 'react-native-safe-area-context';
import { useIsFocused } from '@react-navigation/native';
import { useMotionPreference } from '../../hooks/useMotionPreference';
import { authColors } from '../../theme/authTheme';
import { AuthAmbientBackground } from './AuthAmbientBackground';

const MotionContext = createContext(false);
export const useAuthMotion = () => useContext(MotionContext);

export function AuthFrame({
  children,
}: React.PropsWithChildren): React.JSX.Element {
  const reduced = useMotionPreference();
  const focused = useIsFocused();
  const [active, setActive] = useState(AppState.currentState === 'active');
  const progress = useRef(new Animated.Value(1)).current;
  const { height } = useWindowDimensions();
  const insets = useSafeAreaInsets();
  const motion = focused && active && !reduced;
  useEffect(() => {
    const subscription = AppState.addEventListener('change', next =>
      setActive(next === 'active'),
    );
    return () => subscription.remove();
  }, []);
  useEffect(() => {
    if (!motion) {
      progress.setValue(1);
      return;
    }
    progress.setValue(0);
    const animation = Animated.timing(progress, {
      toValue: 1,
      duration: 420,
      easing: Easing.out(Easing.cubic),
      useNativeDriver: true,
      isInteraction: false,
    });
    animation.start();
    return () => animation.stop();
  }, [motion, progress]);
  return (
    <MotionContext.Provider value={motion}>
      <KeyboardAvoidingView
        style={styles.screen}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      >
        <AuthAmbientBackground motion={motion} />
        <SafeAreaView style={styles.safe}>
          <ScrollView
            contentContainerStyle={styles.content}
            keyboardShouldPersistTaps="handled"
            keyboardDismissMode="on-drag"
            showsVerticalScrollIndicator={false}
          >
            <Animated.View
              style={[
                styles.card,
                {
                  minHeight: Math.max(
                    580,
                    height - insets.top - insets.bottom - 32,
                  ),
                  opacity: progress.interpolate({
                    inputRange: [0, 1],
                    outputRange: [0.82, 1],
                  }),
                  transform: [
                    {
                      translateY: progress.interpolate({
                        inputRange: [0, 1],
                        outputRange: [14, 0],
                      }),
                    },
                  ],
                },
              ]}
            >
              {children}
            </Animated.View>
          </ScrollView>
        </SafeAreaView>
      </KeyboardAvoidingView>
    </MotionContext.Provider>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: authColors.background },
  safe: { flex: 1 },
  content: {
    flexGrow: 1,
    padding: 16,
    alignItems: 'center',
    justifyContent: 'center',
  },
  card: {
    width: '100%',
    maxWidth: 460,
    borderRadius: 34,
    padding: 24,
    backgroundColor: authColors.card,
    gap: 20,
    boxShadow: '0 12px 45px rgba(45, 39, 101, 0.06)',
  },
});

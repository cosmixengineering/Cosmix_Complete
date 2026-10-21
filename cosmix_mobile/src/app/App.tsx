import React, { useEffect, useState } from 'react';
import { AppState, StatusBar, StyleSheet, Text, View } from 'react-native';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { AppProvider } from './AppProvider';
import { RootNavigator } from '../navigation/RootNavigator';
import { colors } from '../theme/theme';

export default function App(): React.JSX.Element {
  const [visible, setVisible] = useState(AppState.currentState === 'active');
  useEffect(() => {
    const subscription = AppState.addEventListener('change', next =>
      setVisible(next === 'active'),
    );
    return () => subscription.remove();
  }, []);
  return (
    <SafeAreaProvider>
      <AppProvider>
        <StatusBar barStyle="dark-content" />
        <RootNavigator />
        {!visible ? (
          <View style={styles.privacy} accessibilityViewIsModal>
            <Text style={styles.brand}>COSMIX</Text>
            <Text style={styles.message}>Your workspace is private.</Text>
          </View>
        ) : null}
      </AppProvider>
    </SafeAreaProvider>
  );
}
const styles = StyleSheet.create({
  privacy: {
    position: 'absolute',
    top: 0,
    bottom: 0,
    left: 0,
    right: 0,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.background,
    gap: 12,
  },
  brand: {
    fontSize: 28,
    color: colors.navy,
    fontWeight: '800',
    letterSpacing: 3,
  },
  message: { fontSize: 15, color: colors.muted },
});

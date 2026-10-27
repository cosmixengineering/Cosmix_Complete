import React from 'react';
import { ActivityIndicator, Text, View } from 'react-native';
import { NavigationContainer } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { useSession } from '../app/AppProvider';
import { LoginScreen } from '../screens/LoginScreen';
import { VerifyCodeScreen } from '../screens/VerifyCodeScreen';
import { ServiceDetailScreen } from '../screens/ServiceDetailScreen';
import { RequestFormScreen } from '../screens/RequestFormScreen';
import { colors, layout } from '../theme/theme';
import type { RootStackParams } from './types';
import { WorkspaceNavigator } from './WorkspaceNavigator';
import { useMotionPreference } from '../hooks/useMotionPreference';

import { LocationConsentGate } from '../screens/consent/LocationConsentGate';

const Stack = createNativeStackNavigator<RootStackParams>();
export function RootNavigator(): React.JSX.Element {
  const auth = useSession();
  const reducedMotion = useMotionPreference();
  if (auth.status === 'booting') {
    return (
      <View style={[layout.screen, layout.content]}>
        <Text style={layout.title}>COSMIX</Text>
        <ActivityIndicator color={colors.teal} />
        <Text style={layout.muted}>Restoring your secure session…</Text>
      </View>
    );
  }
  const navigation = (
    <NavigationContainer>
      <Stack.Navigator
        screenOptions={{
          headerStyle: { backgroundColor: colors.background },
          headerTintColor: colors.navy,
          headerShadowVisible: false,
          contentStyle: { backgroundColor: colors.background },
        }}
      >
        {auth.status === 'authenticated' ? (
          <Stack.Group navigationKey={auth.sessionId}>
            <Stack.Screen
              name="Workspace"
              component={WorkspaceNavigator}
              options={{ headerShown: false }}
            />
            <Stack.Screen
              name="ServiceDetail"
              component={ServiceDetailScreen}
              options={{ title: 'My records' }}
            />
            <Stack.Screen
              name="RequestForm"
              component={RequestFormScreen}
              options={{ title: 'New request' }}
            />
          </Stack.Group>
        ) : (
          <Stack.Group
            navigationKey="anonymous"
            screenOptions={{ animation: reducedMotion ? 'none' : 'fade' }}
          >
            <Stack.Screen
              name="Login"
              component={LoginScreen}
              options={{ headerShown: false }}
            />
            <Stack.Screen
              name="VerifyCode"
              component={VerifyCodeScreen}
              options={{ headerShown: false }}
            />
          </Stack.Group>
        )}
      </Stack.Navigator>
    </NavigationContainer>
  );
  return auth.status === 'authenticated' ? (
    <LocationConsentGate key={auth.sessionId}>{navigation}</LocationConsentGate>
  ) : (
    navigation
  );
}

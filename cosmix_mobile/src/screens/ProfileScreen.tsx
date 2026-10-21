import React, { useState } from 'react';
import { Alert, Text, View } from 'react-native';
import { useServices, useSession } from '../app/AppProvider';
import { Button, Screen } from '../components/Controls';
import { AccessRecovery } from '../components/AccessRecovery';
import { ServiceRecords } from '../components/ServiceRecords';
import { canOpenService } from '../constants/services';
import { layout } from '../theme/theme';

export function ProfileScreen(): React.JSX.Element {
  const auth = useSession();
  const { session } = useServices();
  const [busy, setBusy] = useState(false);
  const header = (
    <View style={layout.card}>
      <Text style={layout.heading}>
        {auth.account?.displayName || 'My account'}
      </Text>
      <Text style={layout.muted}>Your secure Cosmix workspace.</Text>
      <Button
        title="Sign out"
        secondary
        busy={busy}
        onPress={() =>
          Alert.alert(
            'Sign out?',
            'You will need an SMS verification code to sign in again.',
            [
              { text: 'Cancel', style: 'cancel' },
              {
                text: 'Sign out',
                style: 'destructive',
                onPress: () => {
                  setBusy(true);
                  session
                    .signOut()
                    .catch(() => undefined)
                    .finally(() => setBusy(false));
                },
              },
            ],
          )
        }
      />
    </View>
  );
  if (!canOpenService('profile', auth))
    return (
      <Screen>
        {header}
        <AccessRecovery service="Employment profile" />
      </Screen>
    );
  return <ServiceRecords service="profile" header={header} readOnly />;
}

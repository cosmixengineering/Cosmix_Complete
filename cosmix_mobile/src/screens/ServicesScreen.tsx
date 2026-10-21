import React from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useSession } from '../app/AppProvider';
import { Screen } from '../components/Controls';
import { canOpenService, employeeServices } from '../constants/services';
import type { WorkspaceScreenProps } from '../navigation/types';
import { colors, layout } from '../theme/theme';

export function ServicesScreen({
  navigation,
}: WorkspaceScreenProps<'Services'>): React.JSX.Element {
  const auth = useSession();
  return (
    <Screen>
      <View>
        <Text style={styles.eyebrow}>YOUR EMPLOYEE WORKSPACE</Text>
        <Text style={layout.title}>Employee services</Text>
        <Text style={layout.muted}>
          Requests, work records and company updates in one place.
        </Text>
      </View>
      <View style={styles.hero}>
        <Text style={styles.heroTitle}>Here for your workday.</Text>
        <Text style={styles.heroText}>
          Apply for leave, follow a request and view the records your company
          shares with you.
        </Text>
      </View>
      <View style={styles.grid}>
        {employeeServices.map(service => {
          const permitted = canOpenService(service.key, auth);
          return (
            <Pressable
              key={service.key}
              accessibilityRole="button"
              accessibilityState={{ disabled: !permitted }}
              disabled={!permitted}
              onPress={() =>
                ['punches', 'calendar', 'payroll'].includes(service.key)
                  ? navigation.navigate('History', { category: service.key })
                  : service.key === 'profile'
                  ? navigation.navigate('Profile')
                  : navigation.navigate('ServiceDetail', {
                      service: service.key,
                    })
              }
              style={({ pressed }) => [
                styles.tile,
                !permitted && styles.locked,
                pressed && styles.pressed,
              ]}
            >
              <View style={styles.mark}>
                <Text style={styles.markText}>{service.mark}</Text>
              </View>
              <Text style={styles.title}>{service.title}</Text>
              <Text style={styles.description}>{service.description}</Text>
              <Text style={[styles.link, !permitted && styles.lockedText]}>
                {permitted ? 'Open service →' : 'Access required'}
              </Text>
            </Pressable>
          );
        })}
      </View>
      <Text style={layout.muted}>
        Your company controls access to each service. Contact HR if a service
        you need is unavailable.
      </Text>
    </Screen>
  );
}
const styles = StyleSheet.create({
  eyebrow: {
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 1.2,
    color: colors.teal,
    marginBottom: 10,
  },
  hero: {
    padding: 22,
    borderRadius: 20,
    backgroundColor: colors.navy,
    gap: 10,
  },
  heroTitle: { fontSize: 23, fontWeight: '700', color: colors.white },
  heroText: { fontSize: 14, lineHeight: 22, color: '#D9E9F0' },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: 12 },
  tile: {
    flexBasis: '47%',
    flexGrow: 1,
    padding: 17,
    borderRadius: 18,
    borderWidth: 1,
    borderColor: colors.line,
    backgroundColor: colors.white,
    gap: 10,
    minHeight: 205,
  },
  mark: {
    width: 40,
    height: 40,
    borderRadius: 12,
    backgroundColor: colors.mint,
    alignItems: 'center',
    justifyContent: 'center',
  },
  markText: { fontSize: 13, fontWeight: '800', color: colors.teal },
  title: { fontSize: 17, fontWeight: '700', color: colors.navy },
  description: { fontSize: 13, lineHeight: 20, color: colors.muted, flex: 1 },
  link: { fontSize: 13, fontWeight: '700', color: colors.teal },
  locked: { opacity: 0.6 },
  lockedText: { color: colors.muted },
  pressed: { opacity: 0.8 },
});

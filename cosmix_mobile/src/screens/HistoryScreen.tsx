import React from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useSession } from '../app/AppProvider';
import { Screen } from '../components/Controls';
import { AccessRecovery } from '../components/AccessRecovery';
import { NavigationIcon } from '../components/NavigationIcon';
import { ServiceRecords } from '../components/ServiceRecords';
import {
  canOpenService,
  historyCategories,
  resolveHistoryCategory,
} from '../constants/services';
import type { WorkspaceScreenProps } from '../navigation/types';
import { colors, layout } from '../theme/theme';

export function HistoryScreen({
  route,
  navigation,
}: WorkspaceScreenProps<'History'>): React.JSX.Element {
  const auth = useSession();
  const selected = resolveHistoryCategory(route.params?.category, auth);
  const header = (
    <View style={layout.stack8}>
      <View style={styles.heading}>
        <View style={styles.mark}>
          <NavigationIcon name="history" color={colors.teal} />
        </View>
        <View style={styles.headingText}>
          <Text style={layout.title}>My history</Text>
          <Text style={layout.muted}>Your records, all in one place.</Text>
        </View>
      </View>
      <Text style={styles.label}>CHOOSE A RECORD</Text>
      <View style={styles.categories}>
        {historyCategories
          .filter(item => canOpenService(item.key, auth))
          .map(item => (
            <Pressable
              key={item.key}
              accessibilityRole="button"
              accessibilityLabel={`${item.title} history`}
              accessibilityState={{ selected: selected === item.key }}
              onPress={() => navigation.setParams({ category: item.key })}
              style={({ pressed }) => [
                styles.category,
                selected === item.key && styles.active,
                pressed && styles.pressed,
              ]}
            >
              <Text
                style={[
                  styles.categoryText,
                  selected === item.key && styles.activeText,
                ]}
              >
                {item.title}
              </Text>
            </Pressable>
          ))}
      </View>
    </View>
  );
  if (!selected)
    return (
      <Screen>
        {header}
        <AccessRecovery service="Record history" />
      </Screen>
    );
  return (
    <ServiceRecords
      key={selected}
      service={selected}
      header={header}
      readOnly
    />
  );
}

const styles = StyleSheet.create({
  heading: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    marginBottom: 14,
  },
  headingText: { flex: 1, gap: 4 },
  mark: {
    width: 48,
    height: 48,
    borderRadius: 16,
    backgroundColor: colors.mint,
    alignItems: 'center',
    justifyContent: 'center',
  },
  label: {
    color: colors.muted,
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 1.1,
    marginBottom: 6,
  },
  categories: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  category: {
    minHeight: 44,
    justifyContent: 'center',
    paddingHorizontal: 14,
    paddingVertical: 11,
    borderWidth: 1,
    borderColor: colors.line,
    borderRadius: 24,
    backgroundColor: colors.white,
  },
  active: { backgroundColor: colors.navy, borderColor: colors.navy },
  categoryText: { color: colors.text, fontSize: 13, fontWeight: '600' },
  activeText: { color: colors.white },
  pressed: { opacity: 0.7 },
});

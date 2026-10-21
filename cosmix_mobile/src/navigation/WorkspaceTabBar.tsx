import React, { useEffect, useState } from 'react';
import {
  Keyboard,
  Pressable,
  StyleSheet,
  View,
  useWindowDimensions,
} from 'react-native';
import type { BottomTabBarProps } from '@react-navigation/bottom-tabs';
import {
  NavigationIcon,
  type NavigationIconName,
} from '../components/NavigationIcon';
import { colors } from '../theme/theme';
import { useWorkspaceInteraction } from './WorkspaceInteraction';
import type { WorkspaceTabParams } from './types';

const icons: Record<keyof WorkspaceTabParams, NavigationIconName> = {
  Dashboard: 'home',
  Services: 'services',
  Attendance: 'attendance',
  History: 'history',
  Profile: 'profile',
};

export function WorkspaceTabBar({
  state,
  navigation,
  descriptors,
  insets,
}: BottomTabBarProps): React.JSX.Element {
  const { locked } = useWorkspaceInteraction();
  const { width } = useWindowDimensions();
  const left = Math.max(insets.left, 20);
  const right = Math.max(insets.right, 20);
  const diameter = Math.max(
    44,
    (Math.min(width - left - right, 390) - 12) / 5 - 4,
  );
  const [keyboardVisible, setKeyboardVisible] = useState(false);
  useEffect(() => {
    const show = Keyboard.addListener('keyboardDidShow', () =>
      setKeyboardVisible(true),
    );
    const hide = Keyboard.addListener('keyboardDidHide', () =>
      setKeyboardVisible(false),
    );
    return () => {
      show.remove();
      hide.remove();
    };
  }, []);
  if (keyboardVisible) return <View />;
  return (
    <View
      style={[
        styles.container,
        {
          paddingBottom: Math.max(insets.bottom, 12),
          paddingLeft: left,
          paddingRight: right,
        },
      ]}
    >
      <View style={styles.tray}>
        {state.routes.map((route, index) => {
          const selected = state.index === index;
          const options = descriptors[route.key].options;
          const label =
            options.tabBarAccessibilityLabel ?? options.title ?? route.name;
          return (
            <View key={route.key} style={styles.slot}>
              <Pressable
                accessibilityRole="tab"
                accessibilityLabel={label}
                accessibilityHint={
                  locked
                    ? 'Available when attendance verification finishes'
                    : undefined
                }
                accessibilityState={{ selected, disabled: locked }}
                disabled={locked}
                testID={options.tabBarButtonTestID}
                onLongPress={() =>
                  navigation.emit({ type: 'tabLongPress', target: route.key })
                }
                onPress={() => {
                  if (locked) return;
                  const event = navigation.emit({
                    type: 'tabPress',
                    target: route.key,
                    canPreventDefault: true,
                  });
                  if (!selected && !event.defaultPrevented)
                    navigation.navigate(route.name, route.params);
                }}
                style={({ pressed }) => [
                  styles.circle,
                  { width: diameter, height: diameter },
                  selected && styles.selected,
                  pressed && styles.pressed,
                ]}
              >
                <NavigationIcon
                  name={icons[route.name as keyof WorkspaceTabParams]}
                />
              </Pressable>
            </View>
          );
        })}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    backgroundColor: colors.background,
    paddingTop: 10,
    alignItems: 'center',
  },
  tray: {
    width: '100%',
    maxWidth: 390,
    flexDirection: 'row',
    alignItems: 'center',
    borderRadius: 48,
    padding: 4,
    backgroundColor: '#E8E9E8',
    borderWidth: 2,
    borderColor: '#FAFBFA',
    boxShadow: '0 2px 9px rgba(16, 45, 66, 0.10)',
  },
  slot: { flex: 1, alignItems: 'center' },
  circle: {
    borderRadius: 40,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.white,
  },
  selected: { backgroundColor: '#F1FF79' },
  pressed: { opacity: 0.65 },
});

import React from 'react';
import {
  createBottomTabNavigator,
  type BottomTabBarProps,
} from '@react-navigation/bottom-tabs';
import { DashboardScreen } from '../screens/DashboardScreen';
import { ServicesScreen } from '../screens/ServicesScreen';
import { AttendanceScreen } from '../screens/AttendanceScreen';
import { HistoryScreen } from '../screens/HistoryScreen';
import { ProfileScreen } from '../screens/ProfileScreen';
import { colors } from '../theme/theme';
import { WorkspaceInteraction } from './WorkspaceInteraction';
import { WorkspaceTabBar } from './WorkspaceTabBar';
import type { WorkspaceTabParams } from './types';

const Tabs = createBottomTabNavigator<WorkspaceTabParams>();

function renderTabBar(props: BottomTabBarProps): React.JSX.Element {
  return <WorkspaceTabBar {...props} />;
}

export function WorkspaceNavigator(): React.JSX.Element {
  return (
    <WorkspaceInteraction>
      <Tabs.Navigator
        backBehavior="history"
        tabBar={renderTabBar}
        screenOptions={{
          headerStyle: { backgroundColor: colors.background },
          headerTintColor: colors.navy,
          headerShadowVisible: false,
          sceneStyle: { backgroundColor: colors.background },
        }}
      >
        <Tabs.Screen
          name="Dashboard"
          component={DashboardScreen}
          options={{ title: 'Home', tabBarAccessibilityLabel: 'Home' }}
        />
        <Tabs.Screen
          name="Services"
          component={ServicesScreen}
          options={{
            title: 'Services',
            tabBarAccessibilityLabel: 'Employee services',
          }}
        />
        <Tabs.Screen
          name="Attendance"
          component={AttendanceScreen}
          options={{
            title: 'Attendance',
            tabBarAccessibilityLabel: 'Attendance: Check In and Check Out',
          }}
        />
        <Tabs.Screen
          name="History"
          component={HistoryScreen}
          options={{
            title: 'History',
            tabBarAccessibilityLabel: 'History: your records',
          }}
        />
        <Tabs.Screen
          name="Profile"
          component={ProfileScreen}
          options={{
            title: 'Profile',
            tabBarAccessibilityLabel: 'Your profile',
          }}
        />
      </Tabs.Navigator>
    </WorkspaceInteraction>
  );
}

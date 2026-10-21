export { OperationalZonesPage } from './ZonesPage';
export { ZoneEventsPage, MyZonesPage, ZoneDutyWindowsPage } from './ZoneEventsPage';
export { MySiteReportsPage, SiteReportQueuePage } from './SiteReportsPage';
export { MyNotificationsPage } from './NotificationsPage';

// Parent composition owns registration and navigation.
export const fieldRouteMap = {
  OperationalZonesPage: '/hr/attendance/zones/:siteId?',
  ZoneEventsPage: '/hr/attendance/zone-events/:siteId?',
  ZoneDutyWindowsPage: '/hr/attendance/zone-windows/:siteId/:employeeId?',
  SiteReportQueuePage: '/hr/attendance/site-reports/:siteId?',
  MyZonesPage: '/hr/self-service/zones',
  MySiteReportsPage: '/hr/self-service/site-reports',
  MyNotificationsPage: '/account/notifications',
} as const;

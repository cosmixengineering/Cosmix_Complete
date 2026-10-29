import { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Bell,
  CheckCheck,
  X,
  Calendar,
  UserCheck,
  Receipt,
  MapPin,
  ShieldAlert,
  Inbox,
  ChevronRight,
} from 'lucide-react';
import '@/styles/hr-dashboard.css';

export interface NotificationItem {
  id: string;
  title: string;
  body: string;
  timeAgo: string;
  category: 'LEAVE' | 'APPROVAL' | 'PAYROLL' | 'ATTENDANCE' | 'SECURITY';
  path: string;
  isRead: boolean;
  createdAt: string;
}

const STORAGE_KEY = 'cosmix_erp_notifications_v2';

const DEFAULT_NOTIFICATIONS: NotificationItem[] = [
  {
    id: 'noti-001',
    title: 'Employee Approval Pending',
    body: 'Zubair Ahmed (EMP-1012) onboarding completed. Role & access setup ready for review.',
    timeAgo: '15m ago',
    category: 'APPROVAL',
    path: '/hr/approvals',
    isRead: false,
    createdAt: new Date(Date.now() - 15 * 60 * 1000).toISOString(),
  },
  {
    id: 'noti-002',
    title: 'Annual Leave Request Submitted',
    body: 'Engr. Bilal Aslam applied for 5 days Annual Leave (02-Nov to 06-Nov).',
    timeAgo: '35m ago',
    category: 'LEAVE',
    path: '/hr/leaves',
    isRead: false,
    createdAt: new Date(Date.now() - 35 * 60 * 1000).toISOString(),
  },
  {
    id: 'noti-003',
    title: 'October 2026 Payroll Batch Ready',
    body: 'Payroll calculation batch #2026-10 frozen & ready for finance handoff review.',
    timeAgo: '2h ago',
    category: 'PAYROLL',
    path: '/hr/payroll/handoff',
    isRead: false,
    createdAt: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
  },
  {
    id: 'noti-004',
    title: 'Geofence Perimeter Alert',
    body: 'Outside-boundary punch attempt detected at Karachi Head Office Site.',
    timeAgo: '4h ago',
    category: 'ATTENDANCE',
    path: '/hr/attendance',
    isRead: true,
    createdAt: new Date(Date.now() - 4 * 60 * 60 * 1000).toISOString(),
  },
  {
    id: 'noti-005',
    title: 'Executive Role Permissions Updated',
    body: 'HR Executive role permission matrix synchronized with new statutory compliance modules.',
    timeAgo: '1d ago',
    category: 'SECURITY',
    path: '/admin/roles',
    isRead: true,
    createdAt: new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString(),
  },
];

export function NotificationBellToggle() {
  const navigate = useNavigate();
  const [isOpen, setIsOpen] = useState(false);
  const [activeTab, setActiveTab] = useState<'ALL' | 'UNREAD'>('ALL');
  const [notifications, setNotifications] = useState<NotificationItem[]>(() => {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored) {
        const parsed = JSON.parse(stored);
        if (Array.isArray(parsed) && parsed.length > 0) {
          return parsed;
        }
      }
    } catch {
      // ignore
    }
    return DEFAULT_NOTIFICATIONS;
  });

  const wrapperRef = useRef<HTMLDivElement>(null);

  // Sync to localStorage
  useEffect(() => {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(notifications));
    } catch {
      // ignore
    }
  }, [notifications]);

  // Click outside to close
  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    }
    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') {
        setIsOpen(false);
      }
    }

    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside);
      document.addEventListener('keydown', handleKeyDown);
    }
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [isOpen]);

  const unreadCount = notifications.filter((n) => !n.isRead).length;

  const handleToggle = () => {
    setIsOpen((prev) => !prev);
  };

  const handleMarkAllRead = (e: React.MouseEvent) => {
    e.stopPropagation();
    setNotifications((prev) => prev.map((n) => ({ ...n, isRead: true })));
  };

  const handleNotificationClick = (item: NotificationItem) => {
    // Mark as read
    setNotifications((prev) => prev.map((n) => (n.id === item.id ? { ...n, isRead: true } : n)));
    setIsOpen(false);
    navigate(item.path);
  };

  const handleDismiss = (e: React.MouseEvent, id: string) => {
    e.stopPropagation();
    setNotifications((prev) => prev.filter((n) => n.id !== id));
  };

  const handleClearAll = (e: React.MouseEvent) => {
    e.stopPropagation();
    setNotifications([]);
  };

  const filteredNotifications = notifications.filter((n) => {
    if (activeTab === 'UNREAD') return !n.isRead;
    return true;
  });

  const getCategoryIcon = (category: NotificationItem['category']) => {
    switch (category) {
      case 'APPROVAL':
        return {
          icon: <UserCheck size={14} color="#1d4ed8" />,
          bg: '#eff6ff',
          tag: 'HR APPROVAL',
          tagColor: '#1d4ed8',
          tagBg: '#dbeafe',
        };
      case 'LEAVE':
        return {
          icon: <Calendar size={14} color="#b45309" />,
          bg: '#fef3c7',
          tag: 'LEAVE',
          tagColor: '#b45309',
          tagBg: '#fef3c7',
        };
      case 'PAYROLL':
        return {
          icon: <Receipt size={14} color="#15803d" />,
          bg: '#dcfce7',
          tag: 'PAYROLL',
          tagColor: '#15803d',
          tagBg: '#dcfce7',
        };
      case 'ATTENDANCE':
        return {
          icon: <MapPin size={14} color="#b91c1c" />,
          bg: '#fee2e2',
          tag: 'ATTENDANCE',
          tagColor: '#b91c1c',
          tagBg: '#fee2e2',
        };
      case 'SECURITY':
      default:
        return {
          icon: <ShieldAlert size={14} color="#4338ca" />,
          bg: '#e0e7ff',
          tag: 'SECURITY',
          tagColor: '#4338ca',
          tagBg: '#e0e7ff',
        };
    }
  };

  return (
    <div className="hr-notification-wrapper" ref={wrapperRef}>
      {/* Trigger Bell Button */}
      <button
        type="button"
        className={`hr-notification-bell ${isOpen ? 'active' : ''}`}
        aria-label={`${unreadCount} notifications`}
        title="Notifications"
        onClick={handleToggle}
      >
        <Bell size={17} />
        {unreadCount > 0 && (
          <span className="hr-notification-counter">{unreadCount > 9 ? '9+' : unreadCount}</span>
        )}
      </button>

      {/* Dropdown Popover */}
      {isOpen && (
        <div className="hr-notification-dropdown" role="dialog" aria-label="Notifications panel">
          {/* Header */}
          <div className="hr-noti-header">
            <div className="hr-noti-header-left">
              <span className="hr-noti-title">Notifications</span>
              {unreadCount > 0 && <span className="hr-noti-badge">{unreadCount} New</span>}
            </div>
            {unreadCount > 0 && (
              <button
                type="button"
                className="hr-noti-mark-read"
                onClick={handleMarkAllRead}
                title="Mark all as read"
              >
                <CheckCheck size={12} style={{ display: 'inline', marginRight: '3px' }} />
                Mark all read
              </button>
            )}
          </div>

          {/* Filter Tabs */}
          <div className="hr-noti-tabs">
            <button
              type="button"
              className={`hr-noti-tab ${activeTab === 'ALL' ? 'active' : ''}`}
              onClick={() => setActiveTab('ALL')}
            >
              All ({notifications.length})
            </button>
            <button
              type="button"
              className={`hr-noti-tab ${activeTab === 'UNREAD' ? 'active' : ''}`}
              onClick={() => setActiveTab('UNREAD')}
            >
              Unread ({unreadCount})
            </button>
          </div>

          {/* List */}
          <div className="hr-noti-list">
            {filteredNotifications.length === 0 ? (
              <div className="hr-noti-empty">
                <Inbox size={26} className="hr-noti-empty-icon" />
                <div className="hr-noti-empty-title">All caught up!</div>
                <div className="hr-noti-empty-desc">
                  {activeTab === 'UNREAD'
                    ? 'No unread notifications right now.'
                    : 'No notifications to display.'}
                </div>
              </div>
            ) : (
              filteredNotifications.map((item) => {
                const config = getCategoryIcon(item.category);
                return (
                  <div
                    key={item.id}
                    className={`hr-noti-item ${!item.isRead ? 'unread' : ''}`}
                    onClick={() => handleNotificationClick(item)}
                    role="button"
                    tabIndex={0}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter' || e.key === ' ') {
                        handleNotificationClick(item);
                      }
                    }}
                  >
                    <div className="hr-noti-icon-wrap" style={{ background: config.bg }}>
                      {config.icon}
                    </div>

                    <div className="hr-noti-content">
                      <div className="hr-noti-item-title">
                        <span>{item.title}</span>
                        {!item.isRead && <span className="hr-noti-dot" />}
                      </div>
                      <div className="hr-noti-item-body">{item.body}</div>
                      <div className="hr-noti-item-meta">
                        <span
                          className="hr-noti-tag"
                          style={{ color: config.tagColor, background: config.tagBg }}
                        >
                          {config.tag}
                        </span>
                        <span>{item.timeAgo}</span>
                      </div>
                    </div>

                    <button
                      type="button"
                      className="hr-noti-dismiss-btn"
                      title="Dismiss notification"
                      onClick={(e) => handleDismiss(e, item.id)}
                    >
                      <X size={12} />
                    </button>
                  </div>
                );
              })
            )}
          </div>

          {/* Footer */}
          {notifications.length > 0 && (
            <div className="hr-noti-footer">
              <button type="button" className="hr-noti-clear-btn" onClick={handleClearAll}>
                Clear all notifications
              </button>
              <button
                type="button"
                className="hr-noti-view-link"
                onClick={() => {
                  setIsOpen(false);
                  navigate('/hr/approvals');
                }}
              >
                <span>View Action Center</span>
                <ChevronRight size={12} />
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

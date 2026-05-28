export type NotificationType =
  | 'Reminder'
  | 'Confirmation'
  | 'SlotSwap'
  | 'General'
  | 'SwapRequest'
  | 'SwapResult'
  | 'ArrivalAlert'
  | 'StatusChange';

export interface Notification {
  id: string;
  type: NotificationType;
  title: string;
  message: string;
  actionUrl: string | null;
  isRead: boolean;
  sentAt: string; // ISO 8601
}

export interface GetNotificationsResult {
  items: Notification[];
  unreadCount: number;
  totalCount: number;
  page: number;
  pageSize: number;
}

/** High-priority types that trigger a toast popup. */
export const HIGH_PRIORITY_TYPES: NotificationType[] = ['SwapRequest', 'ArrivalAlert'];

/** Maps NotificationType to a PrimeNG icon class. */
export const NOTIFICATION_ICONS: Record<NotificationType, string> = {
  Reminder: 'pi pi-clock',
  Confirmation: 'pi pi-check-circle',
  SlotSwap: 'pi pi-arrows-h',
  General: 'pi pi-info-circle',
  SwapRequest: 'pi pi-arrows-h',
  SwapResult: 'pi pi-arrows-h',
  ArrivalAlert: 'pi pi-map-marker',
  StatusChange: 'pi pi-sync',
};

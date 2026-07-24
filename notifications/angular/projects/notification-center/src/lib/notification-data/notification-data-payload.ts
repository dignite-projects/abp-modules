import { NotificationData } from '../proxy/dignite/abp/notifications';

/**
 * The runtime JSON envelope includes the stable discriminator and flattened concrete payload fields. ABP's proxy
 * generator can only see the abstract CLR base properties, so UI renderers use this structural view outside the
 * generated proxy tree.
 */
export interface NotificationDataPayload extends NotificationData {
  type?: string;
  [key: string]: unknown;
}

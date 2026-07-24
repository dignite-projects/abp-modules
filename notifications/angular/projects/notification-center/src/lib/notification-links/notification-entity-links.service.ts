import { Injectable } from '@angular/core';
import { UrlTree } from '@angular/router';
import { UserNotificationDto } from '../proxy/dignite/abp/notification-center';

export type NotificationEntityLinkTarget = string | unknown[] | UrlTree;

export type NotificationEntityLinkResolver = (
  notification: UserNotificationDto,
) => NotificationEntityLinkTarget | null | undefined;

/**
 * Registry of SPA navigation targets for notification entities, keyed by `EntityTypeName`.
 * This mirrors MVC's `NotificationCenterWebOptions.EntityLinkResolvers`: the notification stores
 * only the stable entity identity, while each UI resolves that identity into its own route shape.
 */
@Injectable({ providedIn: 'root' })
export class NotificationEntityLinksService {
  private readonly resolvers = new Map<string, NotificationEntityLinkResolver>();

  register(entityTypeName: string, resolver: NotificationEntityLinkResolver): void {
    this.resolvers.set(entityTypeName, resolver);
  }

  resolve(notification: UserNotificationDto): NotificationEntityLinkTarget | null {
    const entityTypeName = notification.entityTypeName;
    if (!entityTypeName) {
      return null;
    }

    return this.resolvers.get(entityTypeName)?.(notification) ?? null;
  }
}

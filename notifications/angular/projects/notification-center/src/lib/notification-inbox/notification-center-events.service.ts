import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';

/**
 * In-app signal that the current user's inbox changed outside the bell (e.g. the inbox page marked or deleted
 * notifications), so the bell re-reads its unread count and recent list.
 */
@Injectable({ providedIn: 'root' })
export class NotificationCenterEventsService {
  private readonly inboxChangedSubject = new Subject<void>();

  readonly inboxChanged$ = this.inboxChangedSubject.asObservable();

  notifyInboxChanged(): void {
    this.inboxChangedSubject.next();
  }
}

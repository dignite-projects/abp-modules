import { Component, Input } from '@angular/core';
import { NotificationDataPayload } from '@dignite/ng.notification-center';

@Component({
  selector: 'app-order-shipped-notification-data',
  standalone: true,
  template: `
    <div class="app-order-shipped-notification">
      @if (imageUrl) {
        <img class="app-order-shipped-image" [src]="imageUrl" alt="" />
      }
      <div class="app-order-shipped-body">
        <div class="app-order-shipped-number">{{ orderNumber }}</div>
        <div class="app-order-shipped-meta">{{ itemCount }} item(s) shipped</div>
      </div>
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
        margin-top: 6px;
      }

      .app-order-shipped-notification {
        display: flex;
        align-items: center;
        gap: 10px;
        padding: 8px;
        border: 1px solid #dfe4ea;
        border-radius: 6px;
        background: #fff;
      }

      .app-order-shipped-image {
        width: 44px;
        height: 44px;
        flex: 0 0 44px;
        object-fit: cover;
        border-radius: 4px;
      }

      .app-order-shipped-body {
        min-width: 0;
      }

      .app-order-shipped-number {
        overflow-wrap: anywhere;
        font-size: 0.9rem;
        font-weight: 600;
        line-height: 1.2;
      }

      .app-order-shipped-meta {
        margin-top: 2px;
        color: #6c757d;
        font-size: 0.78rem;
        line-height: 1.2;
      }
    `,
  ],
})
export class OrderShippedNotificationDataComponent {
  @Input() data?: NotificationDataPayload | null;

  get orderNumber(): string {
    return String(this.data?.['orderNumber'] ?? 'Order');
  }

  get itemCount(): number {
    const value = this.data?.['itemCount'];
    return typeof value === 'number' ? value : 0;
  }

  get imageUrl(): string | null {
    const value = this.data?.['imageUrl'];
    return typeof value === 'string' ? value : null;
  }
}

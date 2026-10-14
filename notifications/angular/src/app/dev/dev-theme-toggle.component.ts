import { Component, computed, inject, ChangeDetectionStrategy } from '@angular/core';
import { DevThemeService } from './dev-theme.service';

/**
 * Demo-only dark-mode switch, for eyeballing how the notification UI - the bell's dropdown above all,
 * plus the inbox and subscriptions pages - renders against a dark host. Not part of any published
 * package.
 *
 * Mounted once in the app shell rather than on one page, because the bell lives in the toolbar of
 * every page: the switch is reachable wherever the thing being inspected is.
 *
 * All of the actual work is elsewhere: {@link DevThemeService} owns the state and the `<html>`
 * markers, `dark-mode-rewriter.ts` owns the stylesheet rewriting and documents where its reach
 * stops.
 */
@Component({
  selector: 'app-dev-theme-toggle',
  template: `
    <button type="button" class="dev-theme-toggle" [title]="title()" (click)="toggle()">
      {{ isDark() ? '☀ Light' : '☾ Dark' }}
    </button>
  `,
  changeDetection: ChangeDetectionStrategy.Eager,
  styles: `
    /* Fixed rather than in the page flow, so it stays put wherever a page is scrolled to, and clears
       anything it might otherwise land under: ng-bootstrap's modal sits at z-index 1055. */
    .dev-theme-toggle {
      position: fixed;
      right: 1rem;
      bottom: 1rem;
      z-index: 2000;
      padding: 0.375rem 0.75rem;
      border: 1px solid var(--bs-border-color, #ccc);
      border-radius: 0.375rem;
      background: var(--bs-body-bg, #fff);
      color: var(--bs-body-color, #333);
      font-size: 0.8125rem;
      cursor: pointer;
      opacity: 0.75;
    }

    .dev-theme-toggle:hover {
      opacity: 1;
    }
  `,
})
export class DevThemeToggleComponent {
  private readonly theme = inject(DevThemeService);

  readonly isDark = this.theme.isDark;

  readonly title = computed(() =>
    this.isDark()
      ? `Demo-only dark-mode simulation - ${this.theme.rewrittenCount()} declarations rewritten.`
      : 'Demo-only dark-mode simulation, for checking how the notification UI follows a dark host.',
  );

  toggle(): void {
    this.theme.toggle();
  }
}

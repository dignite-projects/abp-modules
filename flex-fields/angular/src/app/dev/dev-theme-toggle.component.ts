import { Component, computed, inject } from '@angular/core';
import { DevThemeService } from './dev-theme.service';

/**
 * Demo-only dark-mode switch, for eyeballing how the flex-field controls - CKEditor above all -
 * render against a dark host. Not part of any published package.
 *
 * It sits on the home page rather than on the pages it is used to inspect, and that works because
 * the state it drives is global, not page-scoped: {@link DevThemeService} rewrites
 * `document.styleSheets` in place, so switching dark on here and then navigating to the products
 * page leaves that page rendering against a dark host. Only the button itself is left behind - come
 * back to the home page to switch it off.
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
  styles: `
    /* Fixed rather than in the page flow, so it stays put wherever the home page is scrolled to,
       and clears anything it might otherwise land under: ng-bootstrap's modal sits at z-index 1055
       and CKEditor's balloons are raised to 1100 by ckeditor-control.component.css. */
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
      ? `Demo-only dark-mode simulation - ${this.theme.rewrittenCount()} declarations rewritten. ` +
        'Stays on while you navigate; come back here to switch it off.'
      : 'Demo-only dark-mode simulation, for checking how the flex-field controls follow a dark host.',
  );

  toggle(): void {
    this.theme.toggle();
  }
}

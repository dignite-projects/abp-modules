import { Component, ChangeDetectionStrategy } from '@angular/core';
import { DynamicLayoutComponent } from '@abp/ng.core';
import { LoaderBarComponent } from '@abp/ng.theme.shared';
import { DevThemeToggleComponent } from './dev/dev-theme-toggle.component';


@Component({
  selector: 'app-root',
  template: `
    <abp-loader-bar />
    <abp-dynamic-layout />
    <app-dev-theme-toggle />
  `,
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [LoaderBarComponent, DynamicLayoutComponent, DevThemeToggleComponent],
})
export class AppComponent {}

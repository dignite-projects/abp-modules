import { AuthService, LocalizationPipe } from '@abp/ng.core';
import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { DevThemeToggleComponent } from '../dev/dev-theme-toggle.component';

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.scss'],
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [LocalizationPipe, DevThemeToggleComponent]
})
export class HomeComponent {
  private authService = inject(AuthService);

  get hasLoggedIn(): boolean {
    return this.authService.isAuthenticated
  }

  login() {
    this.authService.navigateToLogin();
  }
}

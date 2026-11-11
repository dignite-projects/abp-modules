import { AuthService, LocalizationPipe } from '@abp/ng.core';
import { JsonPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { DriveFileDto, DriveFileInputComponent } from '@dignite/ng.drive';

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.scss'],
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [LocalizationPipe, JsonPipe, ReactiveFormsModule, DriveFileInputComponent]
})
export class HomeComponent {
  private authService = inject(AuthService);

  readonly attachments = new FormControl<DriveFileDto[]>([], { nonNullable: true });

  get hasLoggedIn(): boolean {
    return this.authService.isAuthenticated
  }

  login() {
    this.authService.navigateToLogin();
  }
}

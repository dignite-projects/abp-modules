import { AuthService, LocalizationPipe } from '@abp/ng.core';
import { JsonPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { BlobManagementFileDto, BlobManagementFileInputComponent } from '@dignite/ng.blob-management';

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.scss'],
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [LocalizationPipe, JsonPipe, ReactiveFormsModule, BlobManagementFileInputComponent]
})
export class HomeComponent {
  private authService = inject(AuthService);

  readonly attachments = new FormControl<BlobManagementFileDto[]>([], { nonNullable: true });

  get hasLoggedIn(): boolean {
    return this.authService.isAuthenticated
  }

  login() {
    this.authService.navigateToLogin();
  }
}

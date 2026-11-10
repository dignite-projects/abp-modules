import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { DriveService } from '../services/drive.service';

@Component({
  selector: 'lib-drive',
  template: ` <p>drive works!</p> `,
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class DriveComponent {
  protected readonly service = inject(DriveService);

  constructor() {
    this.service.sample().subscribe(console.log);
  }
}

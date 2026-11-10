import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'lib-drive',
  template: ` <p>drive works!</p> `,
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class DriveComponent {}

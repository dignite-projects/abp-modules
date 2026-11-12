import { ChangeDetectionStrategy, Component } from '@angular/core';
import { BlobManagementExplorerComponent } from './blob-explorer/blob-explorer.component';

/** The management page: the managed containers the host declares, one at a time. */
@Component({
  selector: 'blob-management-page',
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="card">
      <div class="card-body">
        <blob-explorer />
      </div>
    </div>
  `,
  imports: [BlobManagementExplorerComponent],
})
export class BlobManagementComponent {}

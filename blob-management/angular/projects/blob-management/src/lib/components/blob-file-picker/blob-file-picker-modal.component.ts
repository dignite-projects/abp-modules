import { CoreModule } from '@abp/ng.core';
import { ThemeSharedModule } from '@abp/ng.theme.shared';
import { ChangeDetectionStrategy, Component, effect, input, model, output, signal, untracked } from '@angular/core';
import { BlobManagementFileDto } from '../../proxy/files';
import { BlobManagementExplorerComponent } from '../blob-explorer/blob-explorer.component';

/**
 * A modal for choosing files from a container - browsing, uploading and everything else the explorer offers, plus a
 * choice. The choice is handed over only when confirmed; cancelling keeps what was chosen before.
 */
@Component({
  selector: 'blob-file-picker-modal',
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <abp-modal [visible]="visible()" (visibleChange)="visible.set($event)" [options]="{ size: 'xl' }">
      <ng-template #abpHeader>
        <h3>{{ 'BlobManagement::SelectFiles' | abpLocalization }}</h3>
      </ng-template>
      <ng-template #abpBody>
        @if (visible()) {
          <blob-explorer
            [containerName]="containerName()"
            [selection]="multiple() ? 'multiple' : 'single'"
            [(selectedFiles)]="draft"
          />
        }
      </ng-template>
      <ng-template #abpFooter>
        <button type="button" class="btn btn-secondary" abpClose>{{ 'AbpUi::Cancel' | abpLocalization }}</button>
        <button type="button" class="btn btn-primary" [disabled]="draft().length === 0" (click)="confirm()">
          {{ 'BlobManagement::Select' | abpLocalization }} ({{ draft().length }})
        </button>
      </ng-template>
    </abp-modal>
  `,
  imports: [CoreModule, ThemeSharedModule, BlobManagementExplorerComponent],
})
export class BlobManagementFilePickerModalComponent {
  readonly visible = model(false);
  /** Only this container; every container the caller has something to do in when empty. */
  readonly containerName = input<string | null | undefined>(null);
  readonly multiple = input(false);
  /** What is chosen when the modal opens. */
  readonly value = input<BlobManagementFileDto[]>([]);

  /** The files chosen, when the choice is confirmed. */
  readonly confirmed = output<BlobManagementFileDto[]>();

  readonly draft = signal<BlobManagementFileDto[]>([]);

  constructor() {
    effect(() => {
      if (this.visible()) {
        untracked(() => this.draft.set([...this.value()]));
      }
    });
  }

  confirm(): void {
    this.confirmed.emit(this.draft());
    this.visible.set(false);
  }
}

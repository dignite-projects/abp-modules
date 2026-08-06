import { Component, Input } from '@angular/core';
import { CoreModule } from '@abp/ng.core';
import { FlexFieldValue } from '@dignite/ng.flex-fields';
import { marked } from 'marked';
import { CKEditorContentFormat } from './ckeditor-content-format';

/**
 * Displays the value of a `CKEditor` field read-only: HTML as-is, or Markdown converted to HTML
 * client-side (`marked`, GFM on by default) when the field's ContentFormat is Markdown.
 *
 * Binds through `[innerHTML]` on a plain string and relies entirely on Angular's own `DomSanitizer` -
 * deliberately does **not** call `bypassSecurityTrustHtml`. The legacy dignite-abp
 * `SetCkeditorContentPipe` did that (and was itself orphaned, unused code - not a pattern to carry
 * forward); the legacy Angular view component before it interpolated the value as escaped plain text
 * (`{{showValue}}`, no `[innerHTML]` at all) - functionally broken for a rich-text field. This
 * component fixes both: real HTML rendering, sanitized by Angular's default security context rather
 * than bypassed.
 */
@Component({
  selector: 'ff-ckeditor-view',
  templateUrl: './ckeditor-view.component.html',
  imports: [CoreModule],
})
export class CKEditorViewComponent {
  /** Renders bare, without the label wrapper, for use inside a table cell. */
  @Input() showInList = false;

  @Input() fields?: FlexFieldValue;

  /** Registration key of the field type, e.g. `CKEditor`. */
  @Input() type?: string;

  @Input() value: unknown = '';

  get html(): string {
    if (typeof this.value !== 'string' || this.value.length === 0) {
      return '';
    }

    const contentFormat = Number(
      this.fields?.field?.configuration?.['CKEditor.ContentFormat'] ?? CKEditorContentFormat.Html,
    );

    return contentFormat === CKEditorContentFormat.Markdown ? (marked.parse(this.value) as string) : this.value;
  }
}

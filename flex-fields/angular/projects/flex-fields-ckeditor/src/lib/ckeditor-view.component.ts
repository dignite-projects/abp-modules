import { Component, Input, OnChanges, inject } from '@angular/core';
import { CoreModule } from '@abp/ng.core';
import { FlexFieldValue } from '@dignite/ng.flex-fields';
import { marked } from 'marked';
import { CKEditorContentFormat } from './ckeditor-content-format';
import { CKEDITOR_DISPLAY_CONTRIBUTORS, applyDisplayContributors } from './ckeditor-display-contributor';

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
 *
 * The host's `CKEDITOR_DISPLAY_CONTRIBUTORS` rewrite the HTML after any Markdown conversion and before
 * it is bound, so the sanitizer sees their output too.
 */
@Component({
  selector: 'ff-ckeditor-view',
  templateUrl: './ckeditor-view.component.html',
  imports: [CoreModule],
})
export class CKEditorViewComponent implements OnChanges {
  private readonly displayContributors = inject(CKEDITOR_DISPLAY_CONTRIBUTORS, { optional: true }) ?? [];

  /** Renders bare, without the label wrapper, for use inside a table cell. */
  @Input() showInList = false;

  @Input() fields?: FlexFieldValue;

  /** Registration key of the field type, e.g. `CKEditor`. */
  @Input() type?: string;

  @Input() value: unknown = '';

  /**
   * The HTML bound to the view. Worked out when an input changes, not on every change detection, so a
   * `CKEDITOR_DISPLAY_CONTRIBUTORS` entry runs once per value rather than on every pass.
   */
  html = '';

  ngOnChanges(): void {
    this.html = this.buildHtml();
  }

  private buildHtml(): string {
    if (typeof this.value !== 'string' || this.value.length === 0) {
      return '';
    }

    const contentFormat = Number(
      this.fields?.field?.configuration?.['CKEditor.ContentFormat'] ?? CKEditorContentFormat.Html,
    ) as CKEditorContentFormat;

    const html = contentFormat === CKEditorContentFormat.Markdown ? (marked.parse(this.value) as string) : this.value;

    if (this.displayContributors.length === 0 || !this.fields?.field) {
      return html;
    }

    return applyDisplayContributors(html, this.displayContributors, {
      field: this.fields.field,
      contentFormat,
    });
  }
}

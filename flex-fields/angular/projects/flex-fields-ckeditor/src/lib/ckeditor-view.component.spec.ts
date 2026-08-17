import { TestBed } from '@angular/core/testing';
import { FlexFieldValue } from '@dignite/ng.flex-fields';
import { CKEditorContentFormat } from './ckeditor-content-format';
import { CKEditorViewComponent } from './ckeditor-view.component';

function fieldValue(contentFormat: CKEditorContentFormat): FlexFieldValue {
  return {
    field: {
      id: '1',
      name: 'body',
      displayName: 'Body',
      fieldTypeName: 'CKEditor',
      configuration: { 'CKEditor.ContentFormat': contentFormat },
    },
    required: false,
    searchable: false,
  };
}

describe('CKEditorViewComponent', () => {
  function render(value: unknown, fields?: FlexFieldValue, showInList = false) {
    const fixture = TestBed.createComponent(CKEditorViewComponent);
    fixture.componentRef.setInput('value', value);
    if (fields) {
      fixture.componentRef.setInput('fields', fields);
    }
    fixture.componentRef.setInput('showInList', showInList);
    fixture.detectChanges();
    return fixture;
  }

  it('renders HTML content as-is by default', () => {
    const fixture = render('<p>Hello <strong>world</strong></p>');

    expect(fixture.nativeElement.innerHTML).toContain('<strong>world</strong>');
  });

  it('converts Markdown content to HTML client-side', () => {
    const fixture = render('**bold** and _em_', fieldValue(CKEditorContentFormat.Markdown));

    expect(fixture.componentInstance.html).toContain('<strong>bold</strong>');
    expect(fixture.componentInstance.html).toContain('<em>em</em>');
  });

  it('does not convert HTML-format content even when it looks like Markdown', () => {
    const fixture = render('**not bold**', fieldValue(CKEditorContentFormat.Html));

    expect(fixture.componentInstance.html).toBe('**not bold**');
  });

  it('renders empty for a non-string or empty value, rather than throwing', () => {
    expect(render('').componentInstance.html).toBe('');
    expect(render(null).componentInstance.html).toBe('');
    expect(render(undefined).componentInstance.html).toBe('');
  });

  it('renders bare in list mode and inside a label wrapper otherwise', () => {
    const bare = render('<p>hi</p>', undefined, true);
    expect(bare.nativeElement.querySelector('.flex-field-value-ckeditor-compact')).toBeTruthy();

    const wrapped = render('<p>hi</p>', undefined, false);
    expect(wrapped.nativeElement.querySelector('.mb-3')).toBeTruthy();
  });
});

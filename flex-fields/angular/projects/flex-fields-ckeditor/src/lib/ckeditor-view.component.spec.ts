import { TestBed } from '@angular/core/testing';
import { FlexFieldValue } from '@dignite/ng.flex-fields';
import { CKEditorContentFormat } from './ckeditor-content-format';
import { CKEDITOR_DISPLAY_CONTRIBUTORS, CKEditorDisplayContributor } from './ckeditor-display-contributor';
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

const NL = String.fromCharCode(10);

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

  describe('with CKEDITOR_DISPLAY_CONTRIBUTORS', () => {
    const toApiHost: CKEditorDisplayContributor = html =>
      html.replaceAll('src="/api/my-app/files/', 'src="https://api.example.com/api/my-app/files/');

    function register(...contributors: CKEditorDisplayContributor[]) {
      TestBed.configureTestingModule({
        providers: contributors.map(useValue => ({
          provide: CKEDITOR_DISPLAY_CONTRIBUTORS,
          multi: true,
          useValue,
        })),
      });
    }

    it('shows the rewritten HTML for an HTML value', () => {
      register(toApiHost);

      const fixture = render('<p><img src="/api/my-app/files/a.png"></p>', fieldValue(CKEditorContentFormat.Html));

      const img = fixture.nativeElement.querySelector('img') as HTMLImageElement;
      expect(img.getAttribute('src')).toBe('https://api.example.com/api/my-app/files/a.png');
    });

    it('rewrites the HTML a Markdown value was converted to, not the Markdown source', () => {
      const seen: string[] = [];
      register((html, context) => {
        seen.push(html);
        return toApiHost(html, context);
      });

      const fixture = render('![alt](/api/my-app/files/a.png)', fieldValue(CKEditorContentFormat.Markdown), true);

      expect(seen).toHaveLength(1);
      expect(seen[0]).toContain('<img src="/api/my-app/files/a.png"');
      const img = fixture.nativeElement.querySelector('img') as HTMLImageElement;
      expect(img.getAttribute('src')).toBe('https://api.example.com/api/my-app/files/a.png');
    });

    it('hands each contributor the field and the content format, in registration order', () => {
      const first = vi.fn<CKEditorDisplayContributor>(html => `${html}1`);
      const second = vi.fn<CKEditorDisplayContributor>(html => `${html}2`);
      register(first, second);
      const fields = fieldValue(CKEditorContentFormat.Markdown);

      const fixture = render('x', fields);

      expect(first).toHaveBeenCalledWith('<p>x</p>' + NL, {
        field: fields.field,
        contentFormat: CKEditorContentFormat.Markdown,
      });
      expect(second).toHaveBeenCalledWith('<p>x</p>' + NL + '1', expect.anything());
      expect(fixture.componentInstance.html).toBe('<p>x</p>' + NL + '12');
    });

    it('still sanitizes what a contributor returns', () => {
      register(html => `${html}<script>window.__ckeditorViewXss = true</script>`);

      const fixture = render('<p>hi</p>', fieldValue(CKEditorContentFormat.Html));

      expect(fixture.nativeElement.querySelector('script')).toBeNull();
      expect((window as unknown as Record<string, unknown>)['__ckeditorViewXss']).toBeUndefined();
    });
  });
});

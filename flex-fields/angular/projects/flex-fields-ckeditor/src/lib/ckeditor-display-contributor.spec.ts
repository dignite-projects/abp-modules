import { CKEditorContentFormat } from './ckeditor-content-format';
import {
  CKEditorDisplayContext,
  CKEditorDisplayContributor,
  applyDisplayContributors,
} from './ckeditor-display-contributor';

const context: CKEditorDisplayContext = {
  field: {
    id: '1',
    name: 'body',
    displayName: 'Body',
    fieldTypeName: 'CKEditor',
    configuration: {},
  },
  contentFormat: CKEditorContentFormat.Html,
};

describe('applyDisplayContributors', () => {
  it('returns the HTML unchanged when no contributor is registered', () => {
    expect(applyDisplayContributors('<p>hi</p>', [], context)).toBe('<p>hi</p>');
  });

  it('calls each contributor with the HTML and the context', () => {
    const contributor = vi.fn<CKEditorDisplayContributor>(html => html);

    applyDisplayContributors('<p>hi</p>', [contributor], context);

    expect(contributor).toHaveBeenCalledTimes(1);
    expect(contributor).toHaveBeenCalledWith('<p>hi</p>', context);
  });

  it('applies contributors in registration order, each one getting what the previous one returned', () => {
    const first: CKEditorDisplayContributor = html => `${html}1`;
    const second: CKEditorDisplayContributor = html => `${html}2`;

    expect(applyDisplayContributors('x', [first, second], context)).toBe('x12');
    expect(applyDisplayContributors('x', [second, first], context)).toBe('x21');
  });
});

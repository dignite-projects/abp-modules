import type { EditorConfig } from 'ckeditor5';
import { CKEditorContentFormat } from './ckeditor-content-format';
import { CKEditorMode } from './ckeditor-mode';
import {
  CKEditorConfigContext,
  CKEditorConfigContributor,
  applyEditorConfigContributors,
} from './ckeditor-config-contributor';

const context: CKEditorConfigContext = {
  ckeditor5: {} as CKEditorConfigContext['ckeditor5'],
  field: { id: '1', name: 'body', displayName: 'Body', fieldTypeName: 'CKEditor', configuration: {} },
  mode: CKEditorMode.Full,
  contentFormat: CKEditorContentFormat.Html,
};

function baseConfig(): EditorConfig {
  return { licenseKey: 'GPL', toolbar: ['bold'] };
}

describe('applyEditorConfigContributors', () => {
  it('leaves the configuration unchanged when no contributor is registered', () => {
    const config = baseConfig();

    const result = applyEditorConfigContributors(config, [], context);

    expect(result).toBe(config);
    expect(result).toEqual(baseConfig());
  });

  it('calls each contributor with the configuration and the context', () => {
    const contributor = vi.fn<CKEditorConfigContributor>();
    const config = baseConfig();

    applyEditorConfigContributors(config, [contributor], context);

    expect(contributor).toHaveBeenCalledTimes(1);
    expect(contributor).toHaveBeenCalledWith(config, context);
  });

  it('applies contributors in registration order, each one seeing what the previous one left', () => {
    const seen: string[][] = [];
    const first: CKEditorConfigContributor = config => {
      seen.push([...(config.toolbar as string[])]);
      (config.toolbar as string[]).push('first');
    };
    const second: CKEditorConfigContributor = config => {
      seen.push([...(config.toolbar as string[])]);
      (config.toolbar as string[]).push('second');
    };

    const result = applyEditorConfigContributors(baseConfig(), [first, second], context);

    expect(seen).toEqual([['bold'], ['bold', 'first']]);
    expect(result.toolbar).toEqual(['bold', 'first', 'second']);
  });

  it('takes the configuration a contributor returns in place of the one it was given', () => {
    const plugin = function MyPlugin(): void {};
    const replace: CKEditorConfigContributor = config => ({ ...config, extraPlugins: [plugin] });
    const after = vi.fn<CKEditorConfigContributor>();
    const config = baseConfig();

    const result = applyEditorConfigContributors(config, [replace, after], context);

    expect(result).not.toBe(config);
    expect(result.extraPlugins).toEqual([plugin]);
    expect(config.extraPlugins).toBeUndefined();
    expect(after).toHaveBeenCalledWith(result, context);
  });

  it('keeps changes a contributor makes in place when it returns nothing', () => {
    const plugin = function MyPlugin(): void {};
    const mutate: CKEditorConfigContributor = config => {
      config.extraPlugins = [...(config.extraPlugins ?? []), plugin];
    };
    const config = baseConfig();

    const result = applyEditorConfigContributors(config, [mutate], context);

    expect(result).toBe(config);
    expect(result.extraPlugins).toEqual([plugin]);
  });
});

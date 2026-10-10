import { of } from 'rxjs';
import type { FileLoader } from 'ckeditor5';
import { CKEditorUploadAdapter } from './ckeditor-upload-adapter';
import { CKEditorUploadProvider } from './ckeditor-upload-provider';

function fakeLoader(file: File | null): FileLoader {
  return { file: Promise.resolve(file) } as unknown as FileLoader;
}

describe('CKEditorUploadAdapter', () => {
  it('hands the file and the field container to the host provider and returns its URL', async () => {
    const provider: CKEditorUploadProvider = { upload: vi.fn(() => of('https://files/pic.png')) };
    const file = new File(['content'], 'pic.png', { type: 'image/png' });
    const adapter = new CKEditorUploadAdapter(fakeLoader(file), 'images', provider);

    const result = await adapter.upload();

    expect(result).toEqual({ default: 'https://files/pic.png' });
    expect(provider.upload).toHaveBeenCalledWith(file, 'images');
  });

  it('accepts a provider that answers with a promise', async () => {
    const provider: CKEditorUploadProvider = { upload: vi.fn(() => Promise.resolve('/files/pic.png')) };
    const file = new File(['content'], 'pic.png', { type: 'image/png' });
    const adapter = new CKEditorUploadAdapter(fakeLoader(file), 'images', provider);

    expect(await adapter.upload()).toEqual({ default: '/files/pic.png' });
  });

  it('throws instead of uploading when the loader resolves no file', async () => {
    const provider: CKEditorUploadProvider = { upload: vi.fn() };
    const adapter = new CKEditorUploadAdapter(fakeLoader(null), 'images', provider);

    await expect(adapter.upload()).rejects.toThrow('No file to upload.');
    expect(provider.upload).not.toHaveBeenCalled();
  });

  it('throws when the upload succeeds but the provider returns no URL', async () => {
    const provider: CKEditorUploadProvider = { upload: vi.fn(() => of('')) };
    const file = new File(['content'], 'pic.png', { type: 'image/png' });
    const adapter = new CKEditorUploadAdapter(fakeLoader(file), 'images', provider);

    await expect(adapter.upload()).rejects.toThrow(/did not return a file URL/);
  });

  it('does not throw on abort - there is no server-side cancellation endpoint to call', () => {
    const adapter = new CKEditorUploadAdapter(fakeLoader(null), 'images', { upload: vi.fn() });

    expect(() => adapter.abort()).not.toThrow();
  });
});

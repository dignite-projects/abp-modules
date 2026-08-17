import { of } from 'rxjs';
import { RestService } from '@abp/ng.core';
import type { FileLoader } from 'ckeditor5';
import { CKEditorUploadAdapter } from './ckeditor-upload-adapter';

function fakeLoader(file: File | null): FileLoader {
  return { file: Promise.resolve(file) } as unknown as FileLoader;
}

describe('CKEditorUploadAdapter', () => {
  it('uploads the file and returns its URL in the shape CKEditor expects', async () => {
    const restService = { request: vi.fn(() => of({ url: 'https://files/pic.png' })) };
    const file = new File(['content'], 'pic.png', { type: 'image/png' });
    const adapter = new CKEditorUploadAdapter(fakeLoader(file), 'images', restService as unknown as RestService);

    const result = await adapter.upload();

    expect(result).toEqual({ default: 'https://files/pic.png' });
  });

  it('posts the file to the file-explorer upload endpoint under the configured container', async () => {
    let capturedBody: FormData | undefined;
    const restService = {
      request: vi.fn((req: { body: FormData }) => {
        capturedBody = req.body;
        return of({ url: 'https://files/pic.png' });
      }),
    };
    const file = new File(['content'], 'pic.png', { type: 'image/png' });
    const adapter = new CKEditorUploadAdapter(fakeLoader(file), 'images', restService as unknown as RestService);

    await adapter.upload();

    expect(restService.request).toHaveBeenCalledWith(
      expect.objectContaining({ method: 'POST', url: '/api/file-explorer/files', params: { containerName: 'images' } }),
      { apiName: 'FileExplorer' },
    );
    // jsdom's FormData does not preserve File reference identity through append()/get(), just content -
    // compare the parts that matter instead of the object reference.
    const uploaded = capturedBody?.get('file') as File;
    expect(uploaded.name).toBe(file.name);
    expect(uploaded.type).toBe(file.type);
    expect(uploaded.size).toBe(file.size);
  });

  it('throws instead of uploading when the loader resolves no file', async () => {
    const restService = { request: vi.fn() };
    const adapter = new CKEditorUploadAdapter(fakeLoader(null), 'images', restService as unknown as RestService);

    await expect(adapter.upload()).rejects.toThrow('No file to upload.');
    expect(restService.request).not.toHaveBeenCalled();
  });

  it('throws when the upload succeeds but the server returns no URL', async () => {
    const restService = { request: vi.fn(() => of({})) };
    const file = new File(['content'], 'pic.png', { type: 'image/png' });
    const adapter = new CKEditorUploadAdapter(fakeLoader(file), 'images', restService as unknown as RestService);

    await expect(adapter.upload()).rejects.toThrow(/did not return a file URL/);
  });

  it('does not throw on abort - there is no server-side cancellation endpoint to call', () => {
    const adapter = new CKEditorUploadAdapter(fakeLoader(null), 'images', {} as RestService);

    expect(() => adapter.abort()).not.toThrow();
  });
});

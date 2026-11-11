import { TestBed } from '@angular/core/testing';
import { ObjectUrlService } from './object-url.service';

describe('ObjectUrlService', () => {
  it('delegates to URL.createObjectURL for the given blob', () => {
    const blob = new Blob(['content'], { type: 'text/plain' });
    const spy = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:mock-url');

    const url = TestBed.inject(ObjectUrlService).get(blob);

    expect(spy).toHaveBeenCalledWith(blob);
    expect(url).toBe('blob:mock-url');
    spy.mockRestore();
  });

  it('revokes only object URLs', () => {
    const spy = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    const service = TestBed.inject(ObjectUrlService);

    service.revoke('blob:mock-url');
    service.revoke('https://example.com/a.png');
    service.revoke(undefined);

    expect(spy).toHaveBeenCalledTimes(1);
    expect(spy).toHaveBeenCalledWith('blob:mock-url');
    spy.mockRestore();
  });
});

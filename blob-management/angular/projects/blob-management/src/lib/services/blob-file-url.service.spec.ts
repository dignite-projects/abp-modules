import { TestBed } from '@angular/core/testing';
import { EnvironmentService } from '@abp/ng.core';
import { BlobManagementFileUrlService, toBlobManagementPublicSize } from './blob-file-url.service';

describe('BlobManagementFileUrlService', () => {
  let service: BlobManagementFileUrlService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [{ provide: EnvironmentService, useValue: { getApiUrl: () => 'https://blobs.example.com/' } }],
    });
    service = TestBed.inject(BlobManagementFileUrlService);
  });

  it('puts a public address on Blob Management\'s host', () => {
    expect(service.toAbsolute('/blobs/media/a.png')).toBe('https://blobs.example.com/blobs/media/a.png');
    expect(service.toAbsolute('blob:x')).toBe('blob:x');
    expect(service.toAbsolute(null)).toBe('');
  });

  it('asks for a picture at a size, keeping the tenant in the query', () => {
    expect(service.sized('/blobs/media/a.png?__tenant=t', 128, 128)).toBe(
      'https://blobs.example.com/blobs/media/a.png?__tenant=t&Width=128&Height=128',
    );
    expect(service.sized('/blobs/media/a.png', 0, 0)).toBe('https://blobs.example.com/blobs/media/a.png');
    expect(service.sized('blob:x', 128)).toBe('blob:x');
  });

  it('rounds a size up to the next one Blob Management serves', () => {
    const sizes = [256, 64, 128, 4096];
    expect(toBlobManagementPublicSize(1, sizes)).toBe(64);
    expect(toBlobManagementPublicSize(96, sizes)).toBe(128);
    expect(toBlobManagementPublicSize(128, sizes)).toBe(128);
    expect(toBlobManagementPublicSize(129, sizes)).toBe(256);
    expect(toBlobManagementPublicSize(100000, sizes)).toBe(4096);
    expect(toBlobManagementPublicSize(64, [])).toBeUndefined();
  });
});

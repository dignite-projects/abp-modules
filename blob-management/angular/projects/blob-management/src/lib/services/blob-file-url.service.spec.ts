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
    expect(service.toAbsolute('/api/blob-management/public/media/a.png')).toBe('https://blobs.example.com/api/blob-management/public/media/a.png');
    expect(service.toAbsolute('blob:x')).toBe('blob:x');
    expect(service.toAbsolute(null)).toBe('');
  });

  it('asks for a picture at a size, keeping the tenant in the query', () => {
    expect(service.sized('/api/blob-management/public/media/a.png?__tenant=t', 128, 128)).toBe(
      'https://blobs.example.com/api/blob-management/public/media/a.png?__tenant=t&Width=128&Height=128',
    );
    expect(service.sized('/api/blob-management/public/media/a.png', 0, 0)).toBe('https://blobs.example.com/api/blob-management/public/media/a.png');
    expect(service.sized('blob:x', 128)).toBe('blob:x');
  });

  it('rounds a size up to the next one Blob Management serves', () => {
    expect(toBlobManagementPublicSize(1)).toBe(64);
    expect(toBlobManagementPublicSize(96)).toBe(128);
    expect(toBlobManagementPublicSize(128)).toBe(128);
    expect(toBlobManagementPublicSize(129)).toBe(256);
    expect(toBlobManagementPublicSize(100000)).toBe(4096);
  });
});

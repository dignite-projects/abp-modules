import { TestBed } from '@angular/core/testing';
import { EnvironmentService } from '@abp/ng.core';
import { DriveFileUrlService, toDrivePublicSize } from './drive-file-url.service';

describe('DriveFileUrlService', () => {
  let service: DriveFileUrlService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [{ provide: EnvironmentService, useValue: { getApiUrl: () => 'https://drive.example.com/' } }],
    });
    service = TestBed.inject(DriveFileUrlService);
  });

  it('puts a public address on the Drive\'s host', () => {
    expect(service.toAbsolute('/api/drive/public/media/a.png')).toBe('https://drive.example.com/api/drive/public/media/a.png');
    expect(service.toAbsolute('blob:x')).toBe('blob:x');
    expect(service.toAbsolute(null)).toBe('');
  });

  it('asks for a picture at a size, keeping the tenant in the query', () => {
    expect(service.sized('/api/drive/public/media/a.png?__tenant=t', 128, 128)).toBe(
      'https://drive.example.com/api/drive/public/media/a.png?__tenant=t&Width=128&Height=128',
    );
    expect(service.sized('/api/drive/public/media/a.png', 0, 0)).toBe('https://drive.example.com/api/drive/public/media/a.png');
    expect(service.sized('blob:x', 128)).toBe('blob:x');
  });

  it('rounds a size up to the next one the Drive serves', () => {
    expect(toDrivePublicSize(1)).toBe(64);
    expect(toDrivePublicSize(96)).toBe(128);
    expect(toDrivePublicSize(128)).toBe(128);
    expect(toDrivePublicSize(129)).toBe(256);
    expect(toDrivePublicSize(100000)).toBe(4096);
  });
});

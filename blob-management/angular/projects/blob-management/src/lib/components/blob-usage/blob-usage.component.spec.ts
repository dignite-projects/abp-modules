import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { BlobManagementUsageComponent } from './blob-usage.component';
import { BlobManagementUsageService } from '../../proxy/usage';

describe('BlobManagementUsageComponent', () => {
  let get: ReturnType<typeof vi.fn>;
  let getUsers: ReturnType<typeof vi.fn>;
  let getClients: ReturnType<typeof vi.fn>;
  let fixture: ComponentFixture<BlobManagementUsageComponent>;

  function render(usage: object) {
    get = vi.fn(() => of(usage));
    getUsers = vi.fn(() => of({ items: [{ userId: 'u1', uploadedBytes: 5, fileCount: 1 }], totalCount: 1 }));
    getClients = vi.fn(() => of({ items: [{ clientId: null, uploadedBytes: 5, fileCount: 1 }] }));
    TestBed.configureTestingModule({
      imports: [BlobManagementUsageComponent, CoreTestingModule.withConfig()],
      providers: [{ provide: BlobManagementUsageService, useValue: { get, getUsers, getClients } }],
    });
    fixture = TestBed.createComponent(BlobManagementUsageComponent);
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  it('reads the tenant and its containers, and nothing else until it is asked for', () => {
    const usage = render({ storedBytes: 50, storageSize: 200, fileCount: 2, containers: [{ containerName: 'media', storedBytes: 50 }] });

    expect(usage.usage()?.containers?.length).toBe(1);
    expect(usage.usedPercent()).toBe(25);
    expect(getUsers).not.toHaveBeenCalled();
    expect(getClients).not.toHaveBeenCalled();
  });

  it('has no share of an allocation when the tenant has no limit', () => {
    expect(render({ storedBytes: 50, storageSize: null }).usedPercent()).toBeNull();
  });

  it('never shows more than the whole allocation', () => {
    expect(render({ storedBytes: 300, storageSize: 200 }).usedPercent()).toBe(100);
  });

  it('reads the users and the clients when they are first shown, once', () => {
    const usage = render({ storedBytes: 0 });

    usage.show('users');
    usage.show('clients');
    usage.show('users');
    usage.show('clients');
    fixture.detectChanges();

    expect(getUsers).toHaveBeenCalledTimes(1);
    expect(getUsers).toHaveBeenCalledWith(expect.objectContaining({ skipCount: 0, maxResultCount: 10 }));
    expect(getClients).toHaveBeenCalledTimes(1);
    expect(usage.users().totalCount).toBe(1);
    expect(usage.clients()?.length).toBe(1);
  });
});

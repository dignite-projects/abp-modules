import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PermissionService } from '@abp/ng.core';
import { of } from 'rxjs';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { BlobManagementUsageComponent } from './blob-usage.component';
import { BlobManagementUsageService } from '../../proxy/usage';

describe('BlobManagementUsageComponent', () => {
  let get: ReturnType<typeof vi.fn>;
  let getUsers: ReturnType<typeof vi.fn>;
  let getClients: ReturnType<typeof vi.fn>;
  let getTenants: ReturnType<typeof vi.fn>;
  let fixture: ComponentFixture<BlobManagementUsageComponent>;

  function render(usage: object, grantedPolicies: string[] = []) {
    get = vi.fn(() => of(usage));
    getUsers = vi.fn(() => of({ items: [{ userId: 'u1', storedBytes: 5, fileCount: 1 }], totalCount: 1 }));
    getClients = vi.fn(() => of({ items: [{ clientId: null, storedBytes: 5, fileCount: 1 }] }));
    getTenants = vi.fn(() =>
      of({
        items: [
          { tenantId: null, storedBytes: 7, fileCount: 2, storageSize: null },
          { tenantId: 't1', tenantName: 'Acme', storedBytes: 5, fileCount: 1, storageSize: 10 },
        ],
        totalCount: 2,
      }),
    );
    TestBed.configureTestingModule({
      imports: [BlobManagementUsageComponent, CoreTestingModule.withConfig()],
      providers: [
        { provide: BlobManagementUsageService, useValue: { get, getUsers, getClients, getTenants } },
        { provide: PermissionService, useValue: { getGrantedPolicy: (policy: string) => grantedPolicies.includes(policy) } },
      ],
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
    expect(getTenants).not.toHaveBeenCalled();
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

  describe('by tenant', () => {
    it('is not offered, and not read, to a caller without the permission - a tenant never holds it', () => {
      const usage = render({ storedBytes: 0 });

      expect(usage.canViewTenants()).toBe(false);
      usage.show('tenants');
      fixture.detectChanges();

      expect(usage.view()).toBe('containers');
      expect(getTenants).not.toHaveBeenCalled();
    });

    it('is read when it is first shown, once, for the host', () => {
      const usage = render({ storedBytes: 0 }, ['BlobManagement.Containers.TenantUsages']);

      usage.show('tenants');
      usage.show('containers');
      usage.show('tenants');
      fixture.detectChanges();

      expect(usage.canViewTenants()).toBe(true);
      expect(getTenants).toHaveBeenCalledTimes(1);
      expect(getTenants).toHaveBeenCalledWith(expect.objectContaining({ skipCount: 0, maxResultCount: 10 }));
      expect(usage.tenants().items?.length).toBe(2);
    });

    it('works out the share of each tenant allocation', () => {
      const usage = render({ storedBytes: 0 });

      expect(usage.percentOf(5, 10)).toBe(50);
      expect(usage.percentOf(30, 10)).toBe(100);
      expect(usage.percentOf(5, null)).toBeNull();
      expect(usage.percentOf(5, undefined)).toBeNull();
    });
  });

  describe('refresh', () => {
    it('reads the containers and the totals again, and nothing else', () => {
      const usage = render({ storedBytes: 0 });

      usage.refresh();

      expect(get).toHaveBeenCalledTimes(2);
      expect(getUsers).not.toHaveBeenCalled();
      expect(getClients).not.toHaveBeenCalled();
    });

    it('reads the clients again when they are the ones shown', () => {
      const usage = render({ storedBytes: 0 });
      usage.show('clients');

      usage.refresh();

      expect(getClients).toHaveBeenCalledTimes(2);
      expect(get).toHaveBeenCalledTimes(1);
    });
  });
});

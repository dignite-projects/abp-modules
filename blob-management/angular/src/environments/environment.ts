import { Environment } from '@abp/ng.core';

const baseUrl = 'http://localhost:4200';

const oAuthConfig = {
  issuer: 'https://localhost:44352/',
  redirectUri: baseUrl,
  clientId: 'Host_App',
  responseType: 'code',
  scope: 'offline_access Host',
  requireHttps: true,
};

export const environment = {
  production: false,
  application: {
    baseUrl,
    name: 'Host',
  },
  oAuthConfig,
  apis: {
    default: {
      url: 'https://localhost:44352',
      rootNamespace: 'Dignite.Abp.BlobManagement.Host',
    },
    AbpAccountPublic: {
      url: oAuthConfig.issuer,
      rootNamespace: 'AbpAccountPublic',
    },
  },
} as Environment;

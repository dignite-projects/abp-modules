import { Environment } from '@abp/ng.core';

const baseUrl = 'http://localhost:4200';

const oAuthConfig = {
  issuer: 'https://localhost:44380/',
  redirectUri: baseUrl,
  clientId: 'Host_App',
  responseType: 'code',
  scope: 'offline_access Host',
  requireHttps: true,
};

export const environment = {
  production: true,
  application: {
    baseUrl,
    name: 'Host',
  },
  oAuthConfig,
  apis: {
    default: {
      url: 'https://localhost:44380',
      rootNamespace: 'Dignite.NotificationCenter.Web.Host',
    },
    AbpAccountPublic: {
      url: oAuthConfig.issuer,
      rootNamespace: 'AbpAccountPublic',
    },
  },
} as Environment;

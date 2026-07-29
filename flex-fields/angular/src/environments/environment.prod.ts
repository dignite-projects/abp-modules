import { Environment } from '@abp/ng.core';

const baseUrl = 'http://localhost:4200';

const oAuthConfig = {
  issuer: 'https://localhost:44330/',
  redirectUri: baseUrl,
  clientId: 'Demo_App',
  responseType: 'code',
  scope: 'offline_access Demo',
  requireHttps: true,
};

export const environment = {
  production: true,
  application: {
    baseUrl,
    name: 'Demo',
  },
  oAuthConfig,
  apis: {
    default: {
      url: 'https://localhost:44330',
      rootNamespace: 'Dignite.Abp.FlexFields.Demo',
    },
    AbpAccountPublic: {
      url: oAuthConfig.issuer,
      rootNamespace: 'AbpAccountPublic',
    },
  },
} as Environment;

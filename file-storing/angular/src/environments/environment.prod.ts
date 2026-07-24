import { Environment } from '@abp/ng.core';

const oAuthConfig = {
  clientId: 'Host_App',
  responseType: 'code',
  scope: 'offline_access Host',
  requireHttps: true,
};

export const environment = {
  production: true,
  remoteEnv: {
    url: '/getEnvConfig',
    mergeStrategy: 'deepmerge',
  },
  application: {
    name: 'Host',
  },
  oAuthConfig,
  apis: {
    default: {
      url: '',
      rootNamespace: 'Dignite.FileExplorer',
    },
  },
} as Environment;

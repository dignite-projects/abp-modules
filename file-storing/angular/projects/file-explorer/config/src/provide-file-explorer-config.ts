import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { FILE_ROUTE_PROVIDERS } from './providers/route.provider';

export function provideFileExplorerConfig(): EnvironmentProviders {
  return makeEnvironmentProviders(FILE_ROUTE_PROVIDERS);
}

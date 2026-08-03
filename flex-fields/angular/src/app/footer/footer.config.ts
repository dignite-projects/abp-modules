import { provideAppInitializer, inject } from '@angular/core';
import { ReplaceableComponentsService } from '@abp/ng.core';
import { eThemeLeptonXComponents } from '@abp/ng.theme.lepton-x';
import { FooterLinksService } from '@volo/ngx-lepton-x.core';
import { FooterComponent } from './footer.component';

function initFooter() {
  const replaceableComponents = inject(ReplaceableComponentsService);
  replaceableComponents.add({
    key: eThemeLeptonXComponents.Footer,
    component: FooterComponent,
  });

  // The line above never actually takes effect: `IfReplaceableTemplateExistsDirective` (in
  // @volo/abp.ng.lepton-x.core) checks this registration exactly once, in ngAfterViewInit, and
  // loses the race against this app initializer - the router's initial navigation renders the
  // layout (and this one-shot check) before app initializers finish. Left in place in case a
  // future ABP/LeptonX upgrade fixes that ordering; the line below is what actually renders today,
  // since `lpx-footer`'s own template drives off this service's observable instead of a one-time
  // check.
  inject(FooterLinksService).setFooterInfo({
    brandName: '',
    brandUrl: '',
    authorName: 'DIGNITE',
    authorUrl: '#',
    links: [
      { text: 'About', link: '' },
      { text: 'Privacy', link: '' },
      { text: 'Contact', link: '' },
    ],
  });
}

export const FOOTER_PROVIDER = [
  provideAppInitializer(() => {
    initFooter();
  }),
];

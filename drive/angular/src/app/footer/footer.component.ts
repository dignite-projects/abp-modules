import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'abp-footer',
  template: `
    <div class="lpx-footbar-container end-0">
      <div class="lpx-footbar">
        <div class="lpx-footbar-copyright">
          <span>{{ currentYear }}© </span>
          <a href="https://leptontheme.com/" target="_blank">Lepton Theme</a>
          <span> by </span>
          <a href="https://volosoft.com/" target="_blank">Volosoft</a>
        </div>
        <div class="lpx-footbar-solo-links">
          <a href="#">About</a>
          <a href="#">Privacy</a>
          <a href="#">Contact</a>
        </div>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class FooterComponent {
  currentYear = new Date().getFullYear();
}

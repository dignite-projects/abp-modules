import { Component } from '@angular/core';

@Component({
  selector: 'app-footer',
  template: `
    <div class="lpx-footbar-container end-0">
      <div class="lpx-footbar">
        <div class="lpx-footbar-copyright">
          <span>DIGNITE</span>
        </div>
        <div class="lpx-footbar-solo-links">
          <a href="#">About</a>
          <a href="#">Privacy</a>
          <a href="#">Contact</a>
        </div>
      </div>
    </div>
  `,
})
export class FooterComponent {}

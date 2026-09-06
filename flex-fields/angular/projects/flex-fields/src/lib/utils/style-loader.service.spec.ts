import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormGroup } from '@angular/forms';
import { LazyLoadService } from '@abp/ng.core';
import { Observable, of, throwError } from 'rxjs';
import { FlexFieldValue } from '../models';
import { SelectControlComponent } from '../field-types/select/select-control.component';
import { SelectSearchComponent } from '../field-types/select/select-search.component';
import {
  DISABLE_FLEX_FIELDS_STYLE_LOADING_TOKEN,
  FlexFieldsStyleLoader,
  NZ_SELECT_STYLE,
} from './style-loader.service';

/** Records what would have been appended to `<head>`, and lets a test decide how the load ends. */
class LazyLoadServiceStub {
  readonly paths: string[] = [];
  result: () => Observable<Event> = () => of(new CustomEvent('load'));

  load(strategy: { path: string }): Observable<Event> {
    this.paths.push(strategy.path);
    return this.result();
  }
}

function fieldValue(): FlexFieldValue {
  return {
    field: {
      id: '1',
      name: 'color',
      displayName: 'Color',
      fieldTypeName: 'Select',
      configuration: {
        'Select.Multiple': true,
        'Select.Options': [{ Text: 'Red', Value: 'red', Selected: false }],
      },
    },
    required: false,
    searchable: true,
  };
}

function render(component: Type<unknown>): void {
  const entity = new FormGroup({ flexFields: new FormGroup({}) });
  const fixture = TestBed.createComponent(component);
  fixture.componentRef.setInput('fields', fieldValue());
  fixture.componentRef.setInput('entity', entity);
  fixture.componentRef.setInput('parentFieldName', 'flexFields');
  fixture.detectChanges();
}

describe('FlexFieldsStyleLoader', () => {
  let lazyLoadService: LazyLoadServiceStub;

  function configure(disabled?: boolean): void {
    lazyLoadService = new LazyLoadServiceStub();
    TestBed.configureTestingModule({
      providers: [
        { provide: LazyLoadService, useValue: lazyLoadService },
        ...(disabled === undefined
          ? []
          : [{ provide: DISABLE_FLEX_FIELDS_STYLE_LOADING_TOKEN, useValue: disabled }]),
      ],
    });
  }

  it('appends the bundle by its fixed file name', () => {
    configure();
    TestBed.inject(FlexFieldsStyleLoader).load(NZ_SELECT_STYLE);
    expect(lazyLoadService.paths).toEqual(['ng-zorro-antd-select.css']);
  });

  it('loads the bundle once even when several components ask for it', () => {
    configure();
    render(SelectControlComponent);
    render(SelectSearchComponent);
    expect(lazyLoadService.paths).toEqual(['ng-zorro-antd-select.css']);
  });

  it('loads nothing when style loading is disabled', () => {
    configure(true);
    render(SelectControlComponent);
    render(SelectSearchComponent);
    expect(lazyLoadService.paths).toEqual([]);
  });

  it('still loads when the token is explicitly false', () => {
    configure(false);
    render(SelectControlComponent);
    expect(lazyLoadService.paths).toEqual(['ng-zorro-antd-select.css']);
  });

  it('reports a missing bundle as a single console error naming the file and the fix', () => {
    configure();
    lazyLoadService.result = () => throwError(() => new CustomEvent('error'));
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined);

    try {
      render(SelectControlComponent);

      expect(consoleError).toHaveBeenCalledTimes(1);
      const message = consoleError.mock.calls[0][0] as string;
      expect(message).toContain('ng-zorro-antd-select.css');
      expect(message).toContain(NZ_SELECT_STYLE.input);
      expect(message).toContain('"inject": false');
    } finally {
      consoleError.mockRestore();
    }
  });

  it('does not retry the report when a second component asks for the failed bundle', () => {
    configure();
    lazyLoadService.result = () => throwError(() => new CustomEvent('error'));
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined);

    try {
      render(SelectControlComponent);
      render(SelectSearchComponent);

      expect(lazyLoadService.paths).toEqual(['ng-zorro-antd-select.css']);
      expect(consoleError).toHaveBeenCalledTimes(1);
    } finally {
      consoleError.mockRestore();
    }
  });
});

import { TestBed } from '@angular/core/testing';
import { provideFlexFields, provideFlexFieldTypes } from '../providers';
import { FieldTypeDefinition } from './field-type-definition';
import { FieldTypeResolver } from './field-type-resolver.service';

const boltOn: FieldTypeDefinition = {
  name: 'CkEditor',
  displayNameKey: 'MyApp::FieldType:RichText',
  indexable: false,
};

describe('FieldTypeResolver', () => {
  it('resolves each built-in by its registration key', () => {
    TestBed.configureTestingModule({ providers: [provideFlexFields()] });
    const resolver = TestBed.inject(FieldTypeResolver);

    expect(resolver.get('TextEdit').displayNameKey).toBe('FlexFields::FieldType:Text');
    expect(resolver.get('TreeView').displayNameKey).toBe('FlexFields::FieldType:Tree');
    expect(resolver.getAll()).toHaveLength(6);
  });

  it('throws on an unregistered key rather than rendering nothing', () => {
    TestBed.configureTestingModule({ providers: [provideFlexFields()] });
    const resolver = TestBed.inject(FieldTypeResolver);

    expect(() => resolver.get('NoSuchType')).toThrow(/NoSuchType/);
    expect(resolver.find('NoSuchType')).toBeUndefined();
  });

  it('is empty, not broken, when nothing has been provided', () => {
    TestBed.configureTestingModule({});
    const resolver = TestBed.inject(FieldTypeResolver);

    expect(resolver.getAll()).toEqual([]);
    expect(() => resolver.get('TextEdit')).toThrow(/provideFlexFields/);
  });

  it('accepts extra types alongside the built-ins', () => {
    TestBed.configureTestingModule({ providers: [provideFlexFields(boltOn)] });
    const resolver = TestBed.inject(FieldTypeResolver);

    expect(resolver.get('CkEditor')).toEqual(boltOn);
    expect(resolver.getAll()).toHaveLength(7);
  });

  it('lets a separate package register on its own', () => {
    TestBed.configureTestingModule({
      providers: [provideFlexFields(), provideFlexFieldTypes(boltOn)],
    });
    const resolver = TestBed.inject(FieldTypeResolver);

    expect(resolver.get('CkEditor')).toEqual(boltOn);
  });

  it('lets a later registration override a built-in of the same name', () => {
    const replacement: FieldTypeDefinition = {
      name: 'TextEdit',
      displayNameKey: 'MyApp::FieldType:MyOwnText',
      indexable: true,
    };
    TestBed.configureTestingModule({
      providers: [provideFlexFields(), provideFlexFieldTypes(replacement)],
    });
    const resolver = TestBed.inject(FieldTypeResolver);

    expect(resolver.get('TextEdit')).toEqual(replacement);
    // Replaced, not duplicated.
    expect(resolver.getAll()).toHaveLength(6);
  });
});

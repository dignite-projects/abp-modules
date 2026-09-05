import { normalizeInlineFieldDefinitions } from './inline-field-definition';

describe('normalizeInlineFieldDefinitions', () => {
  it('reads anything that is not an array as empty', () => {
    expect(normalizeInlineFieldDefinitions(undefined)).toEqual([]);
    expect(normalizeInlineFieldDefinitions(null)).toEqual([]);
    expect(normalizeInlineFieldDefinitions('')).toEqual([]);
    expect(normalizeInlineFieldDefinitions({ name: 'title' })).toEqual([]);
  });

  it('fills every default for a member with nothing in it', () => {
    expect(normalizeInlineFieldDefinitions([{}, null])).toEqual([
      { name: '', displayName: '', description: undefined, fieldTypeName: '', required: false, configuration: {} },
      { name: '', displayName: '', description: undefined, fieldTypeName: '', required: false, configuration: {} },
    ]);
  });

  it('passes a fully-populated camelCase member straight through', () => {
    // camelCase is what the designer writes and what these readers always emit, so a member that
    // arrives camelCase is already correct.
    const stored = [
      {
        name: 'title',
        displayName: 'Title',
        description: 'The headline',
        fieldTypeName: 'Text',
        required: true,
        configuration: { 'Text.CharLimit': 120 },
      },
    ];

    expect(normalizeInlineFieldDefinitions(stored)).toEqual(stored);
  });

  it('reads a PascalCase member into the same camelCase result', () => {
    // What a definition authored server-side from the typed C# configuration classes looks like once
    // EF Core has serialized it with System.Text.Json's default options.
    const stored = [
      {
        Name: 'title',
        DisplayName: 'Title',
        Description: 'The headline',
        FieldTypeName: 'Text',
        Required: true,
        Configuration: { 'Text.CharLimit': 120 },
      },
    ];

    expect(normalizeInlineFieldDefinitions(stored)).toEqual([
      {
        name: 'title',
        displayName: 'Title',
        description: 'The headline',
        fieldTypeName: 'Text',
        required: true,
        configuration: { 'Text.CharLimit': 120 },
      },
    ]);
  });

  it('reads casings mixed across members, and mixed within one member', () => {
    const normalized = normalizeInlineFieldDefinitions([
      { Name: 'title', DisplayName: 'Title', FieldTypeName: 'Text', Required: true },
      { name: 'qty', displayName: 'Quantity', fieldTypeName: 'Number', required: false },
      // One member with each key in whichever casing - camelCase wins per key, never per member.
      { name: 'note', DisplayName: 'Note', fieldTypeName: 'Text', Required: true, Configuration: { a: 1 } },
    ]);

    expect(normalized.map(field => field.name)).toEqual(['title', 'qty', 'note']);
    expect(normalized.map(field => field.displayName)).toEqual(['Title', 'Quantity', 'Note']);
    expect(normalized.map(field => field.fieldTypeName)).toEqual(['Text', 'Number', 'Text']);
    expect(normalized.map(field => field.required)).toEqual([true, false, true]);
    expect(normalized.map(field => field.configuration)).toEqual([{}, {}, { a: 1 }]);
  });
});

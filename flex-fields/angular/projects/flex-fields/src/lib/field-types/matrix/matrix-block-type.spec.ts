import { normalizeMatrixBlockTypes, normalizeMatrixBlockValues } from './matrix-block-type';

describe('normalizeMatrixBlockTypes', () => {
  it('reads anything that is not an array as empty', () => {
    expect(normalizeMatrixBlockTypes(undefined)).toEqual([]);
    expect(normalizeMatrixBlockTypes(null)).toEqual([]);
    expect(normalizeMatrixBlockTypes('')).toEqual([]);
    expect(normalizeMatrixBlockTypes({ name: 'quote' })).toEqual([]);
  });

  it('fills every default for a member with nothing in it, fields included', () => {
    expect(normalizeMatrixBlockTypes([{}])).toEqual([{ name: '', displayName: '', fields: [] }]);
  });

  it('coalesces a non-array fields to empty rather than propagating it', () => {
    expect(normalizeMatrixBlockTypes([{ name: 'quote', displayName: 'Quote', fields: 'nope' }])).toEqual([
      { name: 'quote', displayName: 'Quote', fields: [] },
    ]);
  });

  it('passes a fully-populated camelCase block type through', () => {
    const stored = [
      {
        name: 'quote',
        displayName: 'Quote',
        fields: [
          {
            name: 'text',
            displayName: 'Text',
            description: undefined,
            fieldTypeName: 'Text',
            required: true,
            configuration: { 'Text.CharLimit': 120 },
          },
        ],
      },
    ];

    expect(normalizeMatrixBlockTypes(stored)).toEqual(stored);
  });

  it('reads a PascalCase block type - nested Fields included - into the same camelCase result', () => {
    // What a Matrix field authored server-side from `new MatrixConfiguration { BlockTypes = ... }`
    // looks like after EF Core serializes it with System.Text.Json's default options.
    const stored = [
      {
        Name: 'quote',
        DisplayName: 'Quote',
        Fields: [
          {
            Name: 'text',
            DisplayName: 'Text',
            FieldTypeName: 'Text',
            Required: true,
            Configuration: { 'Text.CharLimit': 120 },
          },
        ],
      },
    ];

    expect(normalizeMatrixBlockTypes(stored)).toEqual([
      {
        name: 'quote',
        displayName: 'Quote',
        fields: [
          {
            name: 'text',
            displayName: 'Text',
            description: undefined,
            fieldTypeName: 'Text',
            required: true,
            configuration: { 'Text.CharLimit': 120 },
          },
        ],
      },
    ]);
  });

  it('reads casings mixed across block types, and mixed within one', () => {
    const normalized = normalizeMatrixBlockTypes([
      { Name: 'quote', DisplayName: 'Quote', Fields: [{ Name: 'text', FieldTypeName: 'Text' }] },
      { name: 'image', displayName: 'Image', fields: [] },
      // A block type named PascalCase whose own fields arrived camelCase, and vice versa.
      { Name: 'video', displayName: 'Video', fields: [{ name: 'url', FieldTypeName: 'Text' }] },
    ]);

    expect(normalized.map(blockType => blockType.name)).toEqual(['quote', 'image', 'video']);
    expect(normalized.map(blockType => blockType.displayName)).toEqual(['Quote', 'Image', 'Video']);
    expect(normalized.map(blockType => blockType.fields.map(field => field.name))).toEqual([
      ['text'],
      [],
      ['url'],
    ]);
  });
});

describe('normalizeMatrixBlockValues', () => {
  it('reads anything that is not an array as empty', () => {
    expect(normalizeMatrixBlockValues(undefined)).toEqual([]);
    expect(normalizeMatrixBlockValues(null)).toEqual([]);
    expect(normalizeMatrixBlockValues('')).toEqual([]);
  });

  it('fills every default for a member with nothing in it', () => {
    expect(normalizeMatrixBlockValues([{}, null])).toEqual([
      { blockTypeName: '', values: {} },
      { blockTypeName: '', values: {} },
    ]);
  });

  it('passes a stored block instance through', () => {
    const stored = [{ blockTypeName: 'quote', values: { text: 'hello', author: 'nobody' } }];
    expect(normalizeMatrixBlockValues(stored)).toEqual(stored);
  });

  it('reads a PascalCase block instance into the same camelCase result', () => {
    const stored = [{ BlockTypeName: 'quote', Values: { text: 'hello', author: 'nobody' } }];

    expect(normalizeMatrixBlockValues(stored)).toEqual([
      { blockTypeName: 'quote', values: { text: 'hello', author: 'nobody' } },
    ]);
  });

  it('reads casings mixed across instances, and mixed within one', () => {
    expect(
      normalizeMatrixBlockValues([
        { BlockTypeName: 'quote', Values: { text: 'hello' } },
        { blockTypeName: 'image', values: { alt: 'nothing' } },
        { BlockTypeName: 'video', values: { url: 'https://example.test' } },
      ]),
    ).toEqual([
      { blockTypeName: 'quote', values: { text: 'hello' } },
      { blockTypeName: 'image', values: { alt: 'nothing' } },
      { blockTypeName: 'video', values: { url: 'https://example.test' } },
    ]);
  });
});

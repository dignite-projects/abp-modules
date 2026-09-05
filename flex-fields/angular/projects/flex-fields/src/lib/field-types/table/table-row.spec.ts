import { normalizeTableRows } from './table-row';

describe('normalizeTableRows', () => {
  it('reads anything that is not an array as empty', () => {
    expect(normalizeTableRows(undefined)).toEqual([]);
    expect(normalizeTableRows(null)).toEqual([]);
    expect(normalizeTableRows('')).toEqual([]);
    expect(normalizeTableRows({ values: {} })).toEqual([]);
  });

  it('fills an empty values bag for a member with nothing in it', () => {
    expect(normalizeTableRows([{}, null])).toEqual([{ values: {} }, { values: {} }]);
  });

  it('passes stored rows through, keeping only values — a row carries no type tag', () => {
    expect(normalizeTableRows([{ values: { title: 'One', qty: 2 } }])).toEqual([
      { values: { title: 'One', qty: 2 } },
    ]);
  });

  it('reads a PascalCase row into the same camelCase result', () => {
    expect(normalizeTableRows([{ Values: { title: 'One', qty: 2 } }])).toEqual([
      { values: { title: 'One', qty: 2 } },
    ]);
  });

  it('reads casings mixed across rows', () => {
    expect(
      normalizeTableRows([{ Values: { title: 'One' } }, { values: { title: 'Two' } }, {}]),
    ).toEqual([{ values: { title: 'One' } }, { values: { title: 'Two' } }, { values: {} }]);
  });
});

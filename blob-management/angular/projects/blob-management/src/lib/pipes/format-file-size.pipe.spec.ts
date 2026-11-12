import { FormatFileSizePipe } from './format-file-size.pipe';

describe('FormatFileSizePipe', () => {
  const pipe = new FormatFileSizePipe();

  it('keeps small sizes in bytes', () => {
    expect(pipe.transform(0)).toBe('0 Bytes');
    expect(pipe.transform(1023)).toBe('1023 Bytes');
    expect(pipe.transform(undefined)).toBe('0 Bytes');
  });

  it('scales larger sizes to one decimal', () => {
    expect(pipe.transform(1536)).toBe('1.5 KB');
    expect(pipe.transform(5 * 1024 * 1024)).toBe('5.0 MB');
  });
});

/*
 * Public API Surface of file-explorer
 */


export * from './lib/components';
export * from './lib/previews';

// DTOs referenced by public component @Input/@Output signatures (e.g. FileExplorerPickerComponent's
// selectFormFile/selectedFileChange) - a consumer needs these to type-check against them.
export type { FileDescriptorDto } from './lib/proxy/dignite/file-explorer/files';

export interface FileTypeIcon {
  type: string;
  icon: string;
}

/** Fallback icon by MIME type (or file extension, for the few types MIME can't distinguish). */
export const IMAGE_TYPE_OPTIONS: readonly FileTypeIcon[] = [
  { type: 'image/jpeg', icon: '' },
  { type: 'image/webp', icon: '' },
  { type: 'image/gif', icon: '' },
  { type: 'image/png', icon: '' },
  { type: 'image/bmp', icon: '' },
  { type: 'audio/ogg', icon: '' },
  { type: 'video/mp4', icon: '' },
  { type: 'application/pdf', icon: 'fa fa-file-pdf-o' },
  { type: 'application/msword', icon: 'fa fa-file-word-o' },
  {
    type: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
    icon: 'fa fa-file-word-o',
  },
  { type: 'application/vnd.ms-powerpoint', icon: 'fa fa-file-powerpoint-o' },
  {
    type: 'application/vnd.openxmlformats-officedocument.presentationml.presentation',
    icon: 'fa fa-file-powerpoint-o',
  },
  { type: 'application/vnd.ms-excel', icon: 'fa fa-file-excel-o' },
  {
    type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    icon: 'fa fa-file-excel-o',
  },
  { type: 'application/x-zip-compressed', icon: 'fa fa-file-archive-o' },
  { type: '7z', icon: 'fa fa-file-archive-o' },
  { type: 'text/plain', icon: 'fa fa-file-text-o' },
  { type: '', icon: 'fa fa-file-o' },
];

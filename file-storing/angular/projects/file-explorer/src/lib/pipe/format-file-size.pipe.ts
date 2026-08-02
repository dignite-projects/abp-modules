import { Pipe, PipeTransform } from '@angular/core';

const UNITS = ['Bytes', 'KB', 'MB', 'GB', 'TB', 'PB', 'EB', 'ZB', 'YB'];

@Pipe({
  name: 'formatFileSize',
  standalone: true,
})
export class FormatFileSizePipe implements PipeTransform {
  transform(bytes: number): string {
    let size = bytes;
    let unitIndex = 0;
    while (size >= 1024 && unitIndex < UNITS.length - 1) {
      size /= 1024;
      unitIndex++;
    }

    return `${size.toFixed(1)} ${UNITS[unitIndex]}`;
  }
}

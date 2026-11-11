import { Pipe, PipeTransform } from '@angular/core';

const UNITS = ['Bytes', 'KB', 'MB', 'GB', 'TB', 'PB'];

/** A byte count for people: `1536` is `1.5 KB`. */
@Pipe({
  name: 'driveFileSize',
})
export class FormatFileSizePipe implements PipeTransform {
  transform(bytes: number | null | undefined): string {
    let size = bytes ?? 0;
    let unitIndex = 0;
    while (size >= 1024 && unitIndex < UNITS.length - 1) {
      size /= 1024;
      unitIndex++;
    }

    return unitIndex === 0 ? `${size} ${UNITS[0]}` : `${size.toFixed(1)} ${UNITS[unitIndex]}`;
  }
}

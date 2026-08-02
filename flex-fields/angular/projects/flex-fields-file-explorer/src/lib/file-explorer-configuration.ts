import { Validators } from '@angular/forms';

/**
 * Configuration of a `FileExplorer` field, shaped for `FormBuilder.group()`.
 *
 * The property names are the stored configuration keys, carried over from the migrated
 * dignite-abp implementation so fields configured there keep working.
 */
export class FileExplorerConfiguration {
  // No fallback container anywhere downstream (the control prompts instead of guessing), so this has
  // to be required here - the point where a misconfiguration is cheapest to catch.
  'FileExplorer.FileContainerName': unknown = ['', [Validators.required]];

  'FileExplorer.UploadFileMultiple': unknown = [false];
}

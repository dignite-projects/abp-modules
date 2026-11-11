```json
//[doc-seo]
{
    "Description": "The Dignite Drive Angular UI (@dignite/ng.drive): drive-file-input, a form control that uploads files to the Drive and holds them as a list of DriveFileDto, and how to replace what it shows under Drive.FileInputComponent."
}
```

# Angular UI

> Drive has not been released yet; see the [overview](index.md).

`@dignite/ng.drive` brings [attaching files](attaching-files.md) to ABP's Angular UI: `drive-file-input`, a form control that uploads files to a user drive, and the proxies of the file API. It has no pages, routes or menu items of its own.

## Installation

Add `@dignite/ng.drive` to the application. Its peer dependencies are packages the application already has: `@abp/ng.core`, `@abp/ng.theme.shared`, `@angular/forms` and Angular itself. Nothing is registered in `app.config.ts` or `app.routes.ts`: import the component where a form uses it.

The proxies call the API named `Drive`. Without an `apis.Drive` entry in the environment, ABP uses `apis.default`, which is right when the Drive is installed in the application's own host; when it runs as [its own service](hosting.md#the-drive-as-its-own-service), point `apis.Drive.url` at it (or at the gateway). Public file addresses are completed with the same URL.

## The File Input

`drive-file-input` attaches files to a form. Its value is the list of the files attached, each a `DriveFileDto`; bind it as any form control, with `formControlName`, `[formControl]` or `ngModel`, or with `[(value)]` outside a form:

````html
<form [formGroup]="form">
  <drive-file-input formControlName="attachments" containerName="attachments" [multiple]="true" />
</form>
````

````ts
import { DriveFileDto, DriveFileInputComponent } from '@dignite/ng.drive';

@Component({
  // ...
  imports: [ReactiveFormsModule, DriveFileInputComponent],
})
export class ArticleFormComponent {
  form = inject(FormBuilder).group({
    attachments: [[] as DriveFileDto[]],
  });
}
````

* `containerName` is the user drive the files are uploaded to. The user needs `Drive.Files.Upload` ([Permissions](permissions.md)); without a container the upload button is disabled.
* `multiple` lets several files be attached; the default is one, and an upload then replaces the file attached.
* The value is the list of `DriveFileDto`. Store each file's `id`, or for a file of a public drive its `url` (relative to the Drive's host - `DriveFileUrlService.toAbsolute` completes it).

The control reads the container's limits and checks the size and the type of each file chosen before sending it; the Drive checks again when it stores it. Files are uploaded one after another, and each is added to the value as soon as it is stored; one that fails does not stop the others. Disabling the form control disables the input.

**Removing a file only takes it out of the value.** The file stays in the Drive: the form may never be saved, and the value it replaces may still point to the file. To delete a file once the change is saved, call `DriveFileService.delete` (the uploader may delete their own file, others need `Drive.Files.Delete`).

## Replacing What It Shows

What the control shows is a [replaceable component](https://abp.io/docs/latest/framework/ui/angular/component-replacement) under the key `eDriveComponents.FileInput` (`'Drive.FileInputComponent'`). A component registered under it replaces the upload button and the list, for example with a picker that chooses from a file library, while the form binding stays in `drive-file-input`: the forms that use it do not change, whichever component is registered.

````ts
import { ReplaceableComponentsService } from '@abp/ng.core';
import { eDriveComponents } from '@dignite/ng.drive';

inject(ReplaceableComponentsService).add({
  key: eDriveComponents.FileInput,
  component: MyFileInputComponent,
});
````

The component receives the control's state as ABP's `REPLACEABLE_DATA`, typed `DriveFileInputReplaceableData`. Its inputs are `value` (the files attached), `containerName`, `multiple` and `disabled`. `value` is two-way: assigning a new list to it reports the list to the form.

````ts
@Component({
  selector: 'my-file-input',
  template: `...`,
})
export class MyFileInputComponent {
  readonly data = inject<DriveFileInputReplaceableData>('REPLACEABLE_DATA' as never);

  attach(file: DriveFileDto) {
    this.data.inputs.value = this.data.inputs.multiple ? [...this.data.inputs.value, file] : [file];
  }
}
````

## Other Pieces

The library also exports the proxies of the file API and their DTOs by name (`DriveFileService`, `DriveContainerService`, `DriveFileDto`, `DriveContainerDto`, and `DriveTicketDto` and `DriveTicketKind` for `DriveFileService.createUploadTicket`), `DriveFileUrlService` (`toAbsolute`, and `sized` for a public picture at one of the sizes the Drive serves), `ObjectUrlService`, and the `driveFileSize` pipe.

## See Also

* [Attaching Files](attaching-files.md)
* [Permissions](permissions.md)

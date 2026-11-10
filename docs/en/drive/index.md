```json
//[doc-seo]
{
    "Description": "Dignite Drive: a per-tenant cloud drive for ABP applications - a BLOB storing provider that keeps an application's bytes in the Drive, plus a user-facing file library."
}
```

# Drive

> **Drive is in development and has not been released yet.** This page says what it is for. The pages that explain how to use it arrive with the first release; the plan is in [issue #379](https://github.com/dignite-projects/abp-modules/issues/379).

Drive is a **per-tenant cloud drive** for ABP applications. It can run inside an application's own process, or once as a service that several applications share.

## Two Ways In

| | From an application | From people |
|---|---|---|
| What it is | A [BLOB Storing](https://abp.io/docs/latest/framework/infrastructure/blob-storing) provider. `container.UseDrive(...)` makes any of the application's own blob containers keep its bytes in the Drive. | A file library: directories, a file picker, and the Drive's own permissions, down to a single file. |
| Who is authenticated | The calling application (a client, or a short-lived ticket), never its users. | The user. |
| Whose rules apply | The application's own: its pipeline contributors and its own authorization run before the bytes reach the Drive. | The Drive's own permissions. |

An application therefore keeps its own file metadata and its own authorization and hands only bytes to the Drive. The Drive adds what an application would otherwise build per project: a storage quota for each tenant, de-duplication of identical content, public read with image sizing, and upload tickets that let a browser send a file straight to the Drive.

## Packages

| Package | Description |
|---|---|
| `Dignite.Abp.BlobStoring.Drive` | The BLOB storing provider. The only package a business module needs. |
| `Dignite.Drive.*` | The Drive itself: domain, application services, HTTP API and client proxies, EF Core and MongoDB providers, and an optional MCP server contribution. |
| `@dignite/ng.drive` | Angular UI: the file picker and the file browser. |

## See Also

* [BLOB Storing](../blob-storing/index.md)

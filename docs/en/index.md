```json
//[doc-seo]
{
    "Description": "Reusable ABP Framework modules from Dignite: BLOB storing pipeline contributors, runtime-defined FlexFields, an extensible notification framework and a shared MCP server."
}
```

# Dignite ABP Modules

Dignite ABP Modules is a set of **reusable modules for the [ABP Framework](https://abp.io)**. They are developed together in [one repository](https://github.com/dignite-projects/abp-modules), released together under one version, and **installed independently**: no module depends on another, so an application takes only the ones it needs.

## The Module List

* [**BLOB Storing**](blob-storing/index.md): Everyday pipeline contributors for [ABP BLOB Storing](https://abp.io/docs/latest/framework/infrastructure/blob-storing): a size cap, a content-type allow-list checked against the real bytes, GZip, and image resize and compress.
* [**FlexFields**](flex-fields/index.md): Runtime-defined fields for any entity: field types, configuration, validation, a per-entity value bag and a query index, with EF Core and MongoDB providers and Angular and MVC UI.
* [**Notifications**](notifications/index.md): An event-driven notification framework with pluggable channels (SignalR, email, device push), plus an optional **Notification Center** with a persistent inbox, subscriptions, a REST API and Angular and MVC UI.
* [**MCP Server**](aspnetcore-mcp/index.md): Hosts one [Model Context Protocol](https://modelcontextprotocol.io) server in an ABP application and lets each module contribute AI-callable tools, resources and prompts to it.

## Requirements

* .NET 10 SDK
* ABP Framework 10.7
* Angular 22, for the Angular UI packages

## Versioning

Every NuGet and npm package in this repository ships the **same version**. The `MAJOR.MINOR` part of that version is the ABP Framework version it is built on, and the `PATCH` part is ours: `10.7.x` targets ABP 10.7, and moving to ABP 10.8 releases `10.8.0`. Breaking changes ship only when `MAJOR.MINOR` moves; a `PATCH` release can bring fixes and new features.

Stable releases are published to NuGet.org and npm; each module's overview lists its packages. The changes of every release are listed in the [CHANGELOG](https://github.com/dignite-projects/abp-modules/blob/main/CHANGELOG.md).

## Source Code

The source code is on [GitHub](https://github.com/dignite-projects/abp-modules) and licensed under [LGPL-3.0-only](https://github.com/dignite-projects/abp-modules/blob/main/LICENSE). Issues and pull requests are welcome; see the [contribution guide](https://github.com/dignite-projects/abp-modules/blob/main/CONTRIBUTING.md) first.

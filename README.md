# VIEApps.Components.Repository

The tiny polyglot component to help POCO objects work with both NoSQL and SQL databases in the same time (just another ORM-ish component) on  .NET Standard 2.x/.NET Core 2.x+

- POCO objects can be stored in both NoSQL database (MongoDB) and SQL database (SQLServer, MySQL, PostgreSQL) at the same time as individual objects or synced objects
- Attributes/Columns are mapped like ActiveRecord acts
- Have built-in extended properties
- Integrated with caching component ([VIEApps.Components.Caching](https://github.com/vieapps/Components.Caching)) to reduce all I/O round trips
- Built-in serializations with JSON and XML
- Fully async with distributed transactions supported (both SQL and NoSQL)

## NuGet

[![NuGet](https://img.shields.io/nuget/v/VIEApps.Components.Repository.svg)](https://www.nuget.org/packages/VIEApps.Components.Repository)

## Configuration
TBD
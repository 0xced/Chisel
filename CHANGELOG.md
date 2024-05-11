# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Unreleased

* The `ChiselGraphIgnore` items now support simple globbing. For example to ignore all packages starting with `System.` in the graph, use the following syntax:

```xml
<ItemGroup>
  <ChiselGraphIgnore Include="System.*" />
</ItemGroup>
```

## [1.0.0][1.0.0] - 2024-04-12

* Fix a crash when MSBuild is running on the desktop .NET Framework

## [1.0.0-rc.2][1.0.0-rc.2] - 2024-03-16

* The wording of some warnings has been improved
* The README has a paragraph on removing the Azure SDK from `Microsoft.Data.SqlClient`
* Readability of Mermaid graphs in dark mode has been improved

## [1.0.0-rc.1][1.0.0-rc.1] - 2024-03-15

Initial release on NuGet

[1.0.0]: https://github.com/0xced/Chisel/compare/1.0.0-rc.2...1.0.0
[1.0.0-rc.2]: https://github.com/0xced/Chisel/compare/1.0.0-rc.1...1.0.0-rc.2
[1.0.0-rc.1]: https://github.com/0xced/Chisel/releases/tag/1.0.0-rc.1

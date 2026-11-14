# RDCore.SDK

The language model and the shared plumbing of [RDCore](https://github.com/rubberduck-vba/RDCore), a language platform for VBA built on the Language Server Protocol and the MS-VBAL specification.

**This is an alpha.** The API will change before 1.0, and a later alpha may break code built against an earlier one.

## What the package holds

| Assembly | What it is |
|---|---|
| `RDCore.SDK` | The RDCore language model (MS-VBAL types, symbols, syntax trees, semantics and diagnostics) and the plumbing every process and extension of the platform shares: hosting, configuration, logging, workspaces, and the server and client app bases. |
| `RDCore.SDK.Protocol` | The messages a client exchanges with the RDCore platform: the `rdcore/*` methods, the platform handshake, and how to start the language server. |

Each assembly carries its French resources (`fr`) beside the English ones.

## Getting started

```bash
dotnet add package RDCore.SDK --prerelease
```

The package targets .NET 10 (`net10.0`).

## Documentation

- [API reference](https://rubberduck-vba.github.io/RDCore.SDK/api/RDCore.SDK.html), generated from the xmldocs of the package's assemblies.
- [RD-VBAL](https://rubberduck-vba.github.io/RDCore/specs/rd-vbal.html), the language specification RDCore implements.
- [Source and issues](https://github.com/rubberduck-vba/RDCore.SDK).

## License

MIT. The license text is in the package and in the [repository](https://github.com/rubberduck-vba/RDCore.SDK/blob/main/LICENSE). A French version of the license is available on request. The package's NOTICE.md names the third-party code it includes, with its license.

The RDCore platform built on this package, the RD-VBA runtime included, is licensed under the GNU GPL v3 or later, in the [RDCore](https://github.com/rubberduck-vba/RDCore) repository.

## Contributing

Contributions are open to individuals, under the [CLA](https://github.com/rubberduck-vba/RDCore.SDK/blob/main/CLA.md). One signature covers every repository of the RDCore platform.

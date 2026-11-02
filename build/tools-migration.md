# Tools extraction

Application, CLI and worker are maintained in [DubUrl.Tools](https://github.com/Seddryck/DubUrl.Tools), extracted from commit 63e5ff87 for [issue #1789](https://github.com/Seddryck/DubUrl/issues/1789).

This repository retains core/provider/authentication libraries, native adapters, library/provider QA, and connector pack/catalog publication. It no longer publishes DubUrl.Application, DubUrl-cli, or DubUrl.Worker. Existing releases remain available; new tool distributions come from Tools' NuGet publication and GitHub Releases.

Merge this cleanup and publish the complete library set before merging Tools. Tools initially pins DubUrl libraries to 0.56.0 and Schema to 0.55.1. Authentication and reusable provider QA projects must be published with the rest of the libraries. The feature release publishes the complete package set before Tools adopts it.

Core's Application friend-assembly grant remains for provider-pack lifecycle orchestration across the package boundary. Core tests no longer load Application's embedded catalog: direct mapping tests register provider modules, and bundled catalog/application round-trip coverage moves to Tools. CLI QA contracts and the signing host move there as well; provider QA retains its independent native authentication contracts.

See [the coordinated cutover guide](https://github.com/Seddryck/DubUrl.Tools/blob/main/docs/extraction.md) for package ownership, NuGet trusted publishing, version baselines and validation requirements. No package should be published concurrently by the two repositories.

# SSAS Multidimensional QA infrastructure

The live contract expects an SSAS Multidimensional instance on `localhost` with the AdventureWorks cube available as `multidim/AdventureWorks`.

The legacy harness did not provision that proprietary service. The provider contract remains discoverable and explicitly skipped until a runner with that fixture is supplied.

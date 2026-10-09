# SSAS Tabular QA infrastructure

The live contract expects an SSAS Tabular instance on `localhost` with the AdventureWorks model available as `tabular/AdventureWorks`.

The legacy harness did not provision that proprietary service. The provider contract remains discoverable and explicitly skipped until a runner with that fixture is supplied.

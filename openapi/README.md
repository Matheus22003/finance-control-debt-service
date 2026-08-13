# OpenAPI contract

`openapi-v1.json` is generated from the running API and must not be edited manually.

After an intentional endpoint or DTO change, run:

```powershell
./scripts/update-openapi-contract.ps1
```

Review and commit the generated diff. CI verifies that the runtime document matches this
snapshot, validates the OpenAPI document, and blocks breaking changes to `/api/v1` in pull
requests. A breaking contract must be introduced under a new API version.

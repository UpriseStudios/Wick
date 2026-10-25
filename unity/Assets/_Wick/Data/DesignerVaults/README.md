# Designer vaults

Hand-made seed vaults for matchmaking (M10 target: 40, four per Vault Level).

1. Build and Test each vault in the in-game editor (Play mode in the Unity Editor; development
   builds let you pick any Vault Level). A successful Test run is its clear-check.
2. Wick > Export Drafts as Designer Vaults copies your drafts here as vault documents.
3. From the repo root: `dotnet run --project tools/seedgen -- designer`. It checks every vault
   against the building rules and its clear-check; a vault without one gets a run from the
   balance bot if it can escape it. Ready vaults go to Data/SeedVaults as d-*.json with
   designer.json, and it prints how many each Vault Level still needs.
4. Wick > Upload Seed Vaults lists them (designer vaults first) with the generated fallbacks.

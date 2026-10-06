# Test fixtures

JSON files in this folder are inputs for the tests in `tests/InventorAddin.Core.Tests`.

Rules:

- Fixtures come from Aidan's personal models only. Never from employer, customer or other company models.
- Export them in Inventor with the **Export Model Data** button on the **Workflow Tools** tab. It writes the extracted document as JSON in the format read by `ModelJson.DeserializeModel`.
- Before committing, check the JSON for anything that should not be public: company names, real part numbers, title block contents, customer names, or file paths that reveal them. Edit those values out or replace them with neutral ones.
- Give each file a name that says what it exercises, for example `part-counterbore-holes.json`.

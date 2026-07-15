# Personal Vault v2 changes

This version keeps Finance Manager and Personal Vault in the same project, with a shared dark theme and cross-over summary on the Finance dashboard.

## Personal Vault

- Added item-specific categories and seeded a wider set of asset categories.
- Added richer fields to support Lucy's old inventory export:
  - Manufacturer
  - Case type
  - Media format
  - Instruction/manual
  - Memory/capacity
  - Owner
  - Release year
  - Boxed
  - For selling
  - Tested
- Added hidden legacy IDs for SQL/import matching:
  - Item `LegacyInventoryId`
  - Item `LegacyPlatformId`
  - Item `LegacyTypeId`
  - Item `LegacyManufacturerId`
  - Item `LegacyCaseTypeId`
  - Item `LegacyFormatId`
  - Item `LegacyInstructionId`
  - Item `LegacyLocationId`
  - Item `OldInventoryId`
  - Type `LegacyTypeId`
  - Platform `LegacyPlatformId`
  - Location `LegacyLocationId`
- The Add/Edit Item screen now has clearer grouped sections:
  - Quick item log
  - Template copying
  - Organisation
  - Add new location
  - Item identifiers
  - Collection details
  - Purchase & insurance
  - Warranty, manual & notes
- Existing items can be used as templates and filtered by type/section.
- Vault dashboard cards show last updated date/time.
- Detail page status badge and collection details are cleaner.

## Finance Manager integration

- Finance Manager now uses the same dark colour system as Personal Vault.
- Both sides default to dark mode.
- Theme toggle is available from Finance Manager and Personal Vault.
- Finance dashboard now shows a Personal Vault overview:
  - Vault item count
  - Vault value
  - Savings goal balance
  - Combined asset + savings view
- Vault value is displayed separately and is not mixed into the savings target.

## Importing Lucy's old inventory

A repeatable SQL import script has been created:

`Database/ImportLucyInventory.sql`

Run the app once first so the v2 schema columns are created. Then run the import script in SSMS against the same database.

The script matches rows using `LegacyInventoryId`, so running it again updates existing imported rows instead of duplicating them.

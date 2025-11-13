# Database Migration Guide

## Migration: Add PathTemplate to ReportType

**Date:** 2025-11-13  
**Migration Script:** `db/migrations/001_add_path_template_to_report_types.sql`

### Overview

This migration moves the path template configuration from SFTP configs to individual report types, providing more flexibility in file path management.

### Changes

1. **report_types table:**
   - Added `path_template` VARCHAR(500) column
   - This allows each report type to define its own download path template

2. **sftp_configs table:**
   - Removed `base_path_template` VARCHAR(500) column
   - SFTP configs now focus only on connection details

### Migration Steps

#### For PostgreSQL/OpenGauss:

```bash
# Connect to your database
psql -U postgres -d lps_gateway

# Run the migration script
\i db/migrations/001_add_path_template_to_report_types.sql
```

#### For Docker PostgreSQL:

```bash
# Copy migration file to container
docker cp db/migrations/001_add_path_template_to_report_types.sql lps-postgres:/tmp/

# Execute migration
docker exec -i lps-postgres psql -U postgres -d lps_gateway -f /tmp/001_add_path_template_to_report_types.sql
```

### Path Template Format

The path template supports the following placeholders:
- `{yyyy}` - 4-digit year (e.g., 2025)
- `{MM}` - 2-digit month (e.g., 01-12)
- `{dd}` - 2-digit day (e.g., 01-31)
- `{HH}` - 2-digit hour (e.g., 00-23)
- `{mm}` - 2-digit minute (e.g., 00-59)
- `{ss}` - 2-digit second (e.g., 00-59)

#### Examples:
- `/data/{yyyy}/{MM}/{dd}` → `/data/2025/11/13`
- `/reports/{yyyy}-{MM}-{dd}/{HH}` → `/reports/2025-11-13/15`
- `/files/{yyyy}/{MM}` → `/files/2025/11`

### Data Migration Notes

**Important:** If you have existing SFTP configurations with `base_path_template` values, you should:

1. Before running the migration, note down the `base_path_template` values from existing SFTP configs
2. Run the migration script
3. Update each report type to set its `path_template` based on the previous SFTP config's template

Example SQL to help with data migration:

```sql
-- View existing base_path_template values before migration
SELECT id, name, base_path_template FROM sftp_configs WHERE base_path_template IS NOT NULL;

-- After migration, update report types with appropriate path templates
UPDATE report_types SET path_template = '/your/path/template/{yyyy}/{MM}/{dd}' WHERE code = 'YOUR_CODE';
```

### Rollback

If you need to rollback this migration:

```sql
-- Rollback script
ALTER TABLE report_types DROP COLUMN IF EXISTS path_template;
ALTER TABLE sftp_configs ADD COLUMN IF NOT EXISTS base_path_template VARCHAR(500) NOT NULL DEFAULT '';
```

**Note:** Rollback will cause data loss for any path templates configured in report types.

### Testing

After migration, verify:

1. The `path_template` column exists in `report_types` table
2. The `base_path_template` column is removed from `sftp_configs` table
3. Report type creation/editing includes path template field
4. File download jobs use the correct path template from report types

### Application Changes

This migration requires the following application code changes (already included in this commit):

- Entity models updated (`ReportType`, `SftpConfig`)
- DTOs updated (`ReportTypeDto`, `SftpConfigDto`)
- Controllers updated (`ReportTypesController`, `SftpConfigsController`)
- Views updated (ReportTypes Index/Create/Edit, SftpConfigs Index/Create/Edit)
- FileDownloadJob updated to use `ReportType.PathTemplate`

### Support

For questions or issues with this migration, please contact the development team or create an issue in the repository.

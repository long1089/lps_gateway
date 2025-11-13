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
-- Rollback script (compatible with OpenGauss/older PostgreSQL)
DO $$ 
BEGIN
    IF EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_name = 'report_types' 
        AND column_name = 'path_template'
    ) THEN
        ALTER TABLE report_types DROP COLUMN path_template;
    END IF;
END $$;

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

---

## Migration: Increase Report Type Code Length

**Date:** 2025-11-13  
**Migration Script:** `db/migrations/002_increase_report_types_code_length.sql`

### Overview

This migration increases the maximum length of the `code` column in the `report_types` table to accommodate longer report type codes.

### Changes

1. **report_types table:**
   - Changed `code` column from VARCHAR(20) to VARCHAR(100)
   - Allows for more descriptive report type codes

### Migration Steps

```bash
# Connect to your database
psql -U postgres -d lps_gateway

# Run the migration script
\i db/migrations/002_increase_report_types_code_length.sql
```

### Rollback

```sql
-- Only rollback if all existing codes are 20 characters or less
ALTER TABLE report_types ALTER COLUMN code TYPE VARCHAR(20);
```

---

## Migration: Initialize Report Schedules

**Date:** 2025-11-13  
**Migration Script:** `db/migrations/003_initialize_report_schedules.sql`

### Overview

This migration adds a missing report type and initializes 19 schedule configurations for all report types based on system requirements.

### Changes

1. **Added Report Type:**
   - `EGF_REALTIME` - 光伏电站实时数据 (Photovoltaic real-time data)

2. **Initialized 19 Schedules:**
   - 6 Monthly schedules (1st of month at 6:00 AM)
   - 5 Daily schedules (specific times)
   - 8 Cron schedules (various intervals)

### Schedule Configuration Summary

#### Monthly Schedules (每月1日上午6点)
Run once per month on the 1st day at 6:00 AM:
- **EFJ_FARM_INFO** - 风电场基础信息表
- **EFJ_FARM_UNIT_INFO** - 风电机组信息表
- **EFJ_WIND_TOWER_INFO** - 测风塔信息表
- **EGF_GF_INFO** - 光伏电站基础信息表
- **EGF_GF_QXZ_INFO** - 光伏气象站信息表
- **EGF_GF_UNIT_INFO** - 光伏逆变器信息表

#### Daily Schedules (每天固定时间)
Run at specific times each day:
- **EFJ_DQ_RESULT_UP** - 场站上报短期预测 (8:00, 16:00)
- **EFJ_DQ_PLAN_UP** - 场站上报日前计划 (8:10)
- **EFJ_NWP_UP** - 场站上报天气预报 (9:00, 17:00)
- **EFJ_OTHER_UP** - 场站上报其他数据 (8:00)
- **EFJ_FIF_THEORY_POWER** - 场站上报理论功率 (8:00)

#### Cron Schedules (定时执行)

**Every 5 Minutes** (每5分钟, 288次/天):
- **EFJ_FARM_UNIT_RUN_STATE** - 风机运行表
- **EFJ_FARM_RUN_CAP** - 单风场所有风机运行表
- **EFJ_FIVE_WIND_TOWER** - 测风塔采集数据表
- **EGF_FIVE_GF_QXZ** - 气象站采集数据表
- **EGF_GF_UNIT_RUN_STATE** - 逆变器运行表

Cron expression: `0 */5 * * * ?`

**Every 15 Minutes** (每15分钟):
- **EFJ_CDQ_RESULT_UP** - 场站上报超短期预测

Cron expression: `0 */15 * * * ?`

**Every Minute** (每分钟):
- **EFJ_REALTIME** - 风电场实时数据
- **EGF_REALTIME** - 光伏电站实时数据

Cron expression: `0 * * * * ?`

### Migration Steps

```bash
# Connect to your database
psql -U postgres -d lps_gateway

# Run the migration script
\i db/migrations/003_initialize_report_schedules.sql
```

### Cron Expression Format

Cron expressions use the following format:
```
秒 分 时 日 月 星期
```

Examples:
- `0 */5 * * * ?` - Every 5 minutes at 00 seconds
- `0 */15 * * * ?` - Every 15 minutes at 00 seconds
- `0 * * * * ?` - Every minute at 00 seconds

### Timezone

All schedules use the `Asia/Shanghai` timezone (UTC+8).

### Notes

- All INSERT statements use `WHERE NOT EXISTS` to prevent duplicate entries
- Migrations are idempotent and can be safely re-run
- Schedules can be manually enabled/disabled via the `enabled` column in the `schedules` table
- Manual schedule triggers are still available through the web UI

### Testing

After migration, verify:

```sql
-- Check total number of schedules
SELECT COUNT(*) FROM schedules;  -- Should be 19

-- View all schedules with report type names
SELECT 
    rt.code, 
    rt.name, 
    s.schedule_type, 
    s.times, 
    s.month_days, 
    s.cron_expression, 
    s.timezone,
    s.enabled
FROM schedules s 
JOIN report_types rt ON s.report_type_id = rt.id 
ORDER BY rt.code;

-- Check for the new report type
SELECT * FROM report_types WHERE code = 'EGF_REALTIME';
```

### Rollback

```sql
-- Remove all initialized schedules
DELETE FROM schedules WHERE report_type_id IN (
    SELECT id FROM report_types WHERE code IN (
        'EFJ_FARM_INFO', 'EFJ_FARM_UNIT_INFO', 'EFJ_FARM_UNIT_RUN_STATE',
        'EFJ_FARM_RUN_CAP', 'EFJ_WIND_TOWER_INFO', 'EFJ_FIVE_WIND_TOWER',
        'EFJ_DQ_RESULT_UP', 'EFJ_CDQ_RESULT_UP', 'EFJ_DQ_PLAN_UP',
        'EFJ_NWP_UP', 'EFJ_OTHER_UP', 'EFJ_FIF_THEORY_POWER', 'EFJ_REALTIME',
        'EGF_GF_INFO', 'EGF_GF_QXZ_INFO', 'EGF_GF_UNIT_INFO',
        'EGF_GF_UNIT_RUN_STATE', 'EGF_FIVE_GF_QXZ', 'EGF_REALTIME'
    )
);

-- Remove the new report type
DELETE FROM report_types WHERE code = 'EGF_REALTIME';
```

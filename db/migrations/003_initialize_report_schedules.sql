-- Migration: Initialize report type schedules
-- Date: 2025-11-13
-- Description: Add initial schedule configurations for all report types

-- ============================================================
-- 添加缺失的报表类型：光伏电站实时数据
-- ============================================================

INSERT INTO report_types (code, name, enabled)
SELECT 'EGF_REALTIME', '光伏电站实时数据', TRUE
WHERE NOT EXISTS(
    SELECT 1 FROM report_types WHERE code = 'EGF_REALTIME'
);

-- ============================================================
-- 初始化调度配置
-- ============================================================

-- 1. 风电场基础信息表 - 每月1号上午6点
-- schedule_type: monthly, month_days: [1], times: ["06:00"]
INSERT INTO schedules (report_type_id, schedule_type, times, month_days, timezone, enabled)
SELECT 
    (SELECT id FROM report_types WHERE code = 'EFJ_FARM_INFO' LIMIT 1),
    'monthly',
    '["06:00"]'::jsonb,
    '[1]'::jsonb,
    'Asia/Shanghai',
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM schedules 
    WHERE report_type_id = (SELECT id FROM report_types WHERE code = 'EFJ_FARM_INFO' LIMIT 1)
);

-- 2. 风电机组信息表 - 每月1号上午6点
INSERT INTO schedules (report_type_id, schedule_type, times, month_days, timezone, enabled)
SELECT 
    (SELECT id FROM report_types WHERE code = 'EFJ_FARM_UNIT_INFO' LIMIT 1),
    'monthly',
    '["06:00"]'::jsonb,
    '[1]'::jsonb,
    'Asia/Shanghai',
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM schedules 
    WHERE report_type_id = (SELECT id FROM report_types WHERE code = 'EFJ_FARM_UNIT_INFO' LIMIT 1)
);

-- 3. 风机运行表 - 5分钟平均值，288次/天 (每5分钟一次)
-- cron_expression: 每5分钟执行一次
INSERT INTO schedules (report_type_id, schedule_type, times, month_days, cron_expression, timezone, enabled)
SELECT 
    (SELECT id FROM report_types WHERE code = 'EFJ_FARM_UNIT_RUN_STATE' LIMIT 1),
    'cron',
    NULL,
    NULL,
    '0 */5 * * * ?',
    'Asia/Shanghai',
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM schedules 
    WHERE report_type_id = (SELECT id FROM report_types WHERE code = 'EFJ_FARM_UNIT_RUN_STATE' LIMIT 1)
);

-- 4. 单风场所有风机运行表 - 5分钟瞬时值，288次/天
INSERT INTO schedules (report_type_id, schedule_type, times, month_days, cron_expression, timezone, enabled)
SELECT 
    (SELECT id FROM report_types WHERE code = 'EFJ_FARM_RUN_CAP' LIMIT 1),
    'cron',
    NULL,
    NULL,
    '0 */5 * * * ?',
    'Asia/Shanghai',
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM schedules 
    WHERE report_type_id = (SELECT id FROM report_types WHERE code = 'EFJ_FARM_RUN_CAP' LIMIT 1)
);

-- 5. 测风塔信息表 - 每月1号上午6点
INSERT INTO schedules (report_type_id, schedule_type, times, month_days, timezone, enabled)
SELECT 
    (SELECT id FROM report_types WHERE code = 'EFJ_WIND_TOWER_INFO' LIMIT 1),
    'monthly',
    '["06:00"]'::jsonb,
    '[1]'::jsonb,
    'Asia/Shanghai',
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM schedules 
    WHERE report_type_id = (SELECT id FROM report_types WHERE code = 'EFJ_WIND_TOWER_INFO' LIMIT 1)
);

-- 6. 测风塔采集数据表 - 5分钟平均值，288次/天
INSERT INTO schedules (report_type_id, schedule_type, times, month_days, cron_expression, timezone, enabled)
SELECT 
    (SELECT id FROM report_types WHERE code = 'EFJ_FIVE_WIND_TOWER' LIMIT 1),
    'cron',
    NULL,
    NULL,
    '0 */5 * * * ?',
    'Asia/Shanghai',
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM schedules 
    WHERE report_type_id = (SELECT id FROM report_types WHERE code = 'EFJ_FIVE_WIND_TOWER' LIMIT 1)
);

-- 7. 场站上报短期预测 - 2次/天，每天8:00、16:00
-- schedule_type: daily, times: ["08:00", "16:00"]
INSERT INTO schedules (report_type_id, schedule_type, times, month_days, timezone, enabled)
SELECT 
    (SELECT id FROM report_types WHERE code = 'EFJ_DQ_RESULT_UP' LIMIT 1),
    'daily',
    '["08:00", "16:00"]'::jsonb,
    NULL,
    'Asia/Shanghai',
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM schedules 
    WHERE report_type_id = (SELECT id FROM report_types WHERE code = 'EFJ_DQ_RESULT_UP' LIMIT 1)
);

-- 8. 场站上报超短期预测 - 每15分钟滚动上报
-- cron_expression: 每15分钟执行一次
INSERT INTO schedules (report_type_id, schedule_type, times, month_days, cron_expression, timezone, enabled)
SELECT 
    (SELECT id FROM report_types WHERE code = 'EFJ_CDQ_RESULT_UP' LIMIT 1),
    'cron',
    NULL,
    NULL,
    '0 */15 * * * ?',
    'Asia/Shanghai',
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM schedules 
    WHERE report_type_id = (SELECT id FROM report_types WHERE code = 'EFJ_CDQ_RESULT_UP' LIMIT 1)
);

-- 9. 场站上报日前计划 - 1次/天，每天8:10
-- schedule_type: daily, times: ["08:10"]
INSERT INTO schedules (report_type_id, schedule_type, times, month_days, timezone, enabled)
SELECT 
    (SELECT id FROM report_types WHERE code = 'EFJ_DQ_PLAN_UP' LIMIT 1),
    'daily',
    '["08:10"]'::jsonb,
    NULL,
    'Asia/Shanghai',
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM schedules 
    WHERE report_type_id = (SELECT id FROM report_types WHERE code = 'EFJ_DQ_PLAN_UP' LIMIT 1)
);

-- 10. 场站上报天气预报 - 2次/天，每天9:00、17:00
-- schedule_type: daily, times: ["09:00", "17:00"]
INSERT INTO schedules (report_type_id, schedule_type, times, month_days, timezone, enabled)
SELECT 
    (SELECT id FROM report_types WHERE code = 'EFJ_NWP_UP' LIMIT 1),
    'daily',
    '["09:00", "17:00"]'::jsonb,
    NULL,
    'Asia/Shanghai',
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM schedules 
    WHERE report_type_id = (SELECT id FROM report_types WHERE code = 'EFJ_NWP_UP' LIMIT 1)
);

-- 11. 场站上报其他数据 - 1次/天，每天8:00
-- schedule_type: daily, times: ["08:00"]
INSERT INTO schedules (report_type_id, schedule_type, times, month_days, timezone, enabled)
SELECT 
    (SELECT id FROM report_types WHERE code = 'EFJ_OTHER_UP' LIMIT 1),
    'daily',
    '["08:00"]'::jsonb,
    NULL,
    'Asia/Shanghai',
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM schedules 
    WHERE report_type_id = (SELECT id FROM report_types WHERE code = 'EFJ_OTHER_UP' LIMIT 1)
);

-- 12. 场站上报理论功率 - 1次/天，每天8:00
-- schedule_type: daily, times: ["08:00"]
INSERT INTO schedules (report_type_id, schedule_type, times, month_days, timezone, enabled)
SELECT 
    (SELECT id FROM report_types WHERE code = 'EFJ_FIF_THEORY_POWER' LIMIT 1),
    'daily',
    '["08:00"]'::jsonb,
    NULL,
    'Asia/Shanghai',
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM schedules 
    WHERE report_type_id = (SELECT id FROM report_types WHERE code = 'EFJ_FIF_THEORY_POWER' LIMIT 1)
);

-- 13. 光伏场站基础信息表 - 每月1号上午6点
INSERT INTO schedules (report_type_id, schedule_type, times, month_days, timezone, enabled)
SELECT 
    (SELECT id FROM report_types WHERE code = 'EGF_GF_INFO' LIMIT 1),
    'monthly',
    '["06:00"]'::jsonb,
    '[1]'::jsonb,
    'Asia/Shanghai',
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM schedules 
    WHERE report_type_id = (SELECT id FROM report_types WHERE code = 'EGF_GF_INFO' LIMIT 1)
);

-- 14. 光伏气象站信息表 - 每月1号上午6点
INSERT INTO schedules (report_type_id, schedule_type, times, month_days, timezone, enabled)
SELECT 
    (SELECT id FROM report_types WHERE code = 'EGF_GF_QXZ_INFO' LIMIT 1),
    'monthly',
    '["06:00"]'::jsonb,
    '[1]'::jsonb,
    'Asia/Shanghai',
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM schedules 
    WHERE report_type_id = (SELECT id FROM report_types WHERE code = 'EGF_GF_QXZ_INFO' LIMIT 1)
);

-- 15. 光伏气象站采集数据 - 5分钟平均值，288次/天
INSERT INTO schedules (report_type_id, schedule_type, times, month_days, cron_expression, timezone, enabled)
SELECT 
    (SELECT id FROM report_types WHERE code = 'EGF_FIVE_GF_QXZ' LIMIT 1),
    'cron',
    NULL,
    NULL,
    '0 */5 * * * ?',
    'Asia/Shanghai',
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM schedules 
    WHERE report_type_id = (SELECT id FROM report_types WHERE code = 'EGF_FIVE_GF_QXZ' LIMIT 1)
);

-- 16. 逆变器运行表 - 5分钟平均值，288次/天
INSERT INTO schedules (report_type_id, schedule_type, times, month_days, cron_expression, timezone, enabled)
SELECT 
    (SELECT id FROM report_types WHERE code = 'EGF_GF_UNIT_RUN_STATE' LIMIT 1),
    'cron',
    NULL,
    NULL,
    '0 */5 * * * ?',
    'Asia/Shanghai',
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM schedules 
    WHERE report_type_id = (SELECT id FROM report_types WHERE code = 'EGF_GF_UNIT_RUN_STATE' LIMIT 1)
);

-- 17. 逆变器信息表 - 每月1号上午6点
INSERT INTO schedules (report_type_id, schedule_type, times, month_days, timezone, enabled)
SELECT 
    (SELECT id FROM report_types WHERE code = 'EGF_GF_UNIT_INFO' LIMIT 1),
    'monthly',
    '["06:00"]'::jsonb,
    '[1]'::jsonb,
    'Asia/Shanghai',
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM schedules 
    WHERE report_type_id = (SELECT id FROM report_types WHERE code = 'EGF_GF_UNIT_INFO' LIMIT 1)
);

-- 18. 风电场实时数据 - 1次/分钟
-- cron_expression: 每分钟执行一次
INSERT INTO schedules (report_type_id, schedule_type, times, month_days, cron_expression, timezone, enabled)
SELECT 
    (SELECT id FROM report_types WHERE code = 'EFJ_REALTIME' LIMIT 1),
    'cron',
    NULL,
    NULL,
    '0 * * * * ?',
    'Asia/Shanghai',
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM schedules 
    WHERE report_type_id = (SELECT id FROM report_types WHERE code = 'EFJ_REALTIME' LIMIT 1)
);

-- 19. 光伏电站实时数据 - 1次/分钟
-- cron_expression: 每分钟执行一次
INSERT INTO schedules (report_type_id, schedule_type, times, month_days, cron_expression, timezone, enabled)
SELECT 
    (SELECT id FROM report_types WHERE code = 'EGF_REALTIME' LIMIT 1),
    'cron',
    NULL,
    NULL,
    '0 * * * * ?',
    'Asia/Shanghai',
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM schedules 
    WHERE report_type_id = (SELECT id FROM report_types WHERE code = 'EGF_REALTIME' LIMIT 1)
);

-- ============================================================
-- 说明
-- ============================================================
-- Cron 表达式格式: 秒 分 时 日 月 星期
-- 0 */5 * * * ?    每5分钟执行一次
-- 0 */15 * * * ?   每15分钟执行一次
-- 0 * * * * ?      每分钟执行一次
--
-- 调度类型说明:
-- - monthly: 每月固定日期的固定时间执行
-- - daily: 每天固定时间执行
-- - cron: 使用 cron 表达式执行
--
-- 时区: Asia/Shanghai (UTC+8)

using LpsGateway.Data.Models;
using SqlSugar;
using System.Data;
using System.Security.Claims;
using System.Text.Json;

namespace LpsGateway.Extensions
{
    public static class SqlSugarClientExtension
    {
        public static void EnabledAuditLog(this SqlSugarClient db, IHttpContextAccessor httpContextAccessor)
        {
            // 配置差异日志审计（直接在 Program.cs 中编写逻辑，无需单独 Helper 类）
            db.Aop.OnDiffLogEvent = it =>
            {
                // 1. 获取表名
                var tableName = it.BeforeData.FirstOrDefault()?.TableName;
                if (!string.IsNullOrEmpty(tableName))
                {
                    tableName = it.AfterData.FirstOrDefault()?.TableName;
                }
                if (tableName == null || tableName.ToLower() == "audit_logs")
                {
                    return;
                }

                // 2. 获取变更前后数据
                var beforeColumns = it.BeforeData?.FirstOrDefault()?.Columns ?? new List<DiffLogColumnInfo>();
                var afterColumns = it.AfterData?.FirstOrDefault()?.Columns ?? new List<DiffLogColumnInfo>();

                // 3. 获取当前用户信息（基于 IHttpContextAccessor）
                var httpContext = httpContextAccessor.HttpContext;
                int? operatorId = null;
                string ipAddress = "未知IP";

                if (httpContext != null)
                {
                    // 已登录用户获取信息
                    if (httpContext.IsAuthenticated())
                    {
                        operatorId = httpContext.GetUserId();
                    }

                    // 获取 IP 和浏览器信息
                    ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "未知IP";
                }

                // 4. 构建字段变更明细
                var beforeColumnDict = beforeColumns.ToDictionary(col => col.ColumnName, col => col.Value == DBNull.Value ? null : col.Value);
                var afterColumnDict = afterColumns.ToDictionary(col => col.ColumnName, col => col.Value == DBNull.Value ? null : col.Value);

                // 5. 构建审计日志实体
                var auditLog = new AuditLog
                {
                    Resource = tableName,
                    Action = it.DiffType.ToString().ToUpper(),
                    CreatedAt = DateTime.Now,
                    UserId = operatorId,
                    IpAddress = ipAddress,
                    Details = new AuditLog.AuditLogFieldChange { OldValue = beforeColumnDict, NewValue = afterColumnDict },
                };

                // 6. 保存审计日志（使用临时连接，避免事务冲突）
                using (var tempDb = db.CopyNew())
                {
                    try
                    {
                        tempDb.Ado.BeginTran(IsolationLevel.ReadUncommitted);
                        tempDb.Insertable(auditLog).ExecuteCommand();
                        tempDb.Ado.CommitTran();
                        Console.WriteLine($"审计日志生成成功：{auditLog.Action} - {auditLog.Resource}");
                    }
                    catch (Exception ex)
                    {
                        tempDb.Ado.RollbackTran();
                        Console.WriteLine($"审计日志保存失败：{ex.Message}");
                    }
                }
            };
        }


        // 需实现的辅助方法（根据项目框架调整）
        private static int GetCurrentUserId(this HttpContext httpContext)
        {
            // 示例：从Session/Token中获取当前用户ID
            var userId = httpContext?.User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
            return int.TryParse(userId, out int id) ? id : 0;
        }

        private static string GetCurrentUserName(this HttpContext httpContext)
        {
            // 示例：获取当前用户名
            return httpContext?.User.Identity?.Name ?? "匿名用户";
        }

        private static string GetClientIpAddress(this HttpContext httpContext)
        {
            // 示例：获取客户端IP
            return httpContext?.Connection.RemoteIpAddress?.ToString() ?? "未知IP";
        }

        private static string GetBrowserInfo(this HttpContext httpContext)
        {
            // 示例：获取浏览器信息
            return httpContext?.Request.Headers["User-Agent"].ToString() ?? "未知浏览器";
        }
    }
}

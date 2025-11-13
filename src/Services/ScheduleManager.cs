    using LpsGateway.Data;
using LpsGateway.Data.Models;
using LpsGateway.Services.Jobs;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using System.Text.Json;

namespace LpsGateway.Services;

/// <summary>
/// 调度管理器接口
/// </summary>
public interface IScheduleManager
{
    /// <summary>
    /// 初始化调度器
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// 启动调度器
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// 停止调度器
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// 重新加载所有调度
    /// </summary>
    Task ReloadSchedulesAsync();

    /// <summary>
    /// 手动触发下载任务
    /// </summary>
    /// <param name="reportTypeId">报表类型ID</param>
    Task TriggerDownloadAsync(int reportTypeId);
}

/// <summary>
/// 调度管理器实现
/// </summary>
public class ScheduleManager : IScheduleManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScheduleManager> _logger;
    private IScheduler? _scheduler;

    public ScheduleManager(
        IServiceProvider serviceProvider,
        ILogger<ScheduleManager> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _logger.LogInformation("初始化调度器");
        
        var factory = new StdSchedulerFactory();
        _scheduler = await factory.GetScheduler();

        // 配置作业工厂以支持依赖注入
        _scheduler.JobFactory = new JobFactory(_serviceProvider);

        _logger.LogInformation("调度器初始化完成");
    }

    /// <inheritdoc />
    public async Task StartAsync()
    {
        if (_scheduler == null)
        {
            throw new InvalidOperationException("调度器未初始化，请先调用 InitializeAsync");
        }

        await _scheduler.Start();
        _logger.LogInformation("调度器已启动");

        // 加载所有调度
        await ReloadSchedulesAsync();
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        if (_scheduler != null)
        {
            await _scheduler.Shutdown();
            _logger.LogInformation("调度器已停止");
        }
    }

    /// <inheritdoc />
    public async Task ReloadSchedulesAsync()
    {
        if (_scheduler == null)
        {
            _logger.LogWarning("调度器未初始化，无法加载调度");
            return;
        }

        _logger.LogInformation("重新加载所有调度");

        // 清除现有作业
        await _scheduler.Clear();

        // 创建作用域来解析 Scoped 服务
        using (var scope = _serviceProvider.CreateScope())
        {
            var scheduleRepository = scope.ServiceProvider.GetRequiredService<IScheduleRepository>();
            
            // 加载所有启用的调度
            var schedules = await scheduleRepository.GetAllAsync();
            var enabledSchedules = schedules.Where(s => s.Enabled).ToList();

            _logger.LogInformation("找到 {Count} 个启用的调度", enabledSchedules.Count);

            foreach (var schedule in enabledSchedules)
            {
                try
                {
                    await AddScheduleJobAsync(schedule);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "添加调度失败: ScheduleId={ScheduleId}", schedule.Id);
                }
            }
        }

        _logger.LogInformation("调度加载完成");
    }

    /// <inheritdoc />
    public async Task TriggerDownloadAsync(int reportTypeId)
    {
        if (_scheduler == null)
        {
            throw new InvalidOperationException("调度器未初始化");
        }

        _logger.LogInformation("手动触发下载任务: ReportTypeId={ReportTypeId}", reportTypeId);

        var jobKey = new JobKey($"manual-download-{reportTypeId}", "manual");
        
        var job = JobBuilder.Create<FileDownloadJob>()
            .WithIdentity(jobKey)
            .UsingJobData("ReportTypeId", reportTypeId)
            .UsingJobData("ScheduleId", 0)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity($"manual-trigger-{reportTypeId}-{DateTime.Now.Ticks}", "manual")
            .StartNow()
            .Build();

        await _scheduler.ScheduleJob(job, trigger);
        
        _logger.LogInformation("手动下载任务已触发: ReportTypeId={ReportTypeId}", reportTypeId);
    }

    /// <summary>
    /// 添加调度作业
    /// </summary>
    private async Task AddScheduleJobAsync(Schedule schedule)
    {
        if (_scheduler == null)
        {
            return;
        }

        var jobKey = new JobKey($"schedule-{schedule.Id}", "scheduled");
        
        var job = JobBuilder.Create<FileDownloadJob>()
            .WithIdentity(jobKey)
            .UsingJobData("ReportTypeId", schedule.ReportTypeId)
            .UsingJobData("ScheduleId", schedule.Id)
            .Build();

        ITrigger trigger;
        
        if (schedule.ScheduleType == "cron" && !string.IsNullOrEmpty(schedule.CronExpression))
        {
            // Cron表达式调度
            trigger = TriggerBuilder.Create()
                .WithIdentity($"trigger-{schedule.Id}", "scheduled")
                .WithCronSchedule(schedule.CronExpression)
                .Build();
        }
        else if (schedule.ScheduleType == "daily" && !string.IsNullOrEmpty(schedule.Times))
        {
            // 每日调度（使用第一个时间创建触发器，实际应为每个时间创建）
            var times = JsonSerializer.Deserialize<List<string>>(schedule.Times);
            if (times != null && times.Any())
            {
                var firstTime = times.First();
                var timeParts = firstTime.Split(':');
                if (timeParts.Length == 2 && 
                    int.TryParse(timeParts[0], out var hour) && 
                    int.TryParse(timeParts[1], out var minute))
                {
                    trigger = TriggerBuilder.Create()
                        .WithIdentity($"trigger-{schedule.Id}", "scheduled")
                        .WithDailyTimeIntervalSchedule(x => x
                            .OnEveryDay()
                            .StartingDailyAt(TimeOfDay.HourAndMinuteOfDay(hour, minute))
                            .WithIntervalInHours(24))
                        .Build();
                }
                else
                {
                    _logger.LogWarning("无效的时间格式: {Time}, ScheduleId={ScheduleId}", firstTime, schedule.Id);
                    return;
                }
            }
            else
            {
                _logger.LogWarning("没有配置时间，ScheduleId={ScheduleId}", schedule.Id);
                return;
            }
        }
        else if (schedule.ScheduleType == "monthly" && !string.IsNullOrEmpty(schedule.MonthDays))
        {
            // 月度调度（简化实现，仅支持Cron表达式）
            var monthDays = JsonSerializer.Deserialize<List<int>>(schedule.MonthDays);
            if (monthDays != null && monthDays.Any())
            {
                var firstDay = monthDays.First();
                var times = !string.IsNullOrEmpty(schedule.Times) ? JsonSerializer.Deserialize<List<string>>(schedule.Times) : null;
                var firstTime = times?.FirstOrDefault() ?? "00:00";
                var timeParts = firstTime.Split(':');
                
                if (timeParts.Length == 2 && 
                    int.TryParse(timeParts[0], out var hour) && 
                    int.TryParse(timeParts[1], out var minute))
                {
                    // 构造Cron表达式: 秒 分 时 日 月 ? 年
                    var cronExpression = $"0 {minute} {hour} {firstDay} * ?";
                    
                    trigger = TriggerBuilder.Create()
                        .WithIdentity($"trigger-{schedule.Id}", "scheduled")
                        .WithCronSchedule(cronExpression)
                        .Build();
                }
                else
                {
                    _logger.LogWarning("无效的时间格式: {Time}, ScheduleId={ScheduleId}", firstTime, schedule.Id);
                    return;
                }
            }
            else
            {
                _logger.LogWarning("没有配置月份日期，ScheduleId={ScheduleId}", schedule.Id);
                return;
            }
        }
        else
        {
            _logger.LogWarning("不支持的调度类型或配置无效: ScheduleType={ScheduleType}, ScheduleId={ScheduleId}", 
                schedule.ScheduleType, schedule.Id);
            return;
        }

        await _scheduler.ScheduleJob(job, trigger);
        
        _logger.LogInformation("已添加调度: ScheduleId={ScheduleId}, Type={ScheduleType}", 
            schedule.Id, schedule.ScheduleType);
    }

    /// <summary>
    /// 自定义作业工厂，支持依赖注入
    /// </summary>
    private class JobFactory : IJobFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public JobFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
        {
            return _serviceProvider.GetRequiredService(bundle.JobDetail.JobType) as IJob
                ?? throw new InvalidOperationException($"无法创建作业实例: {bundle.JobDetail.JobType}");
        }

        public void ReturnJob(IJob job)
        {
            // 不需要处理，由DI容器管理生命周期
        }
    }
}

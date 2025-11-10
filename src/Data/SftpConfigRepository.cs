using LpsGateway.Data.Models;
using SqlSugar;

namespace LpsGateway.Data;

/// <summary>
/// SFTP配置仓储实现
/// </summary>
public class SftpConfigRepository : ISftpConfigRepository
{
    private readonly ISqlSugarClient _db;

    public SftpConfigRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<SftpConfig?> GetByIdAsync(int id)
    {
        return await _db.Queryable<SftpConfig>()
            .Where(s => s.Id == id)
            .FirstAsync();
    }

    public async Task<List<SftpConfig>> GetAllAsync(bool? enabled = null)
    {
        var query = _db.Queryable<SftpConfig>();
        
        if (enabled.HasValue)
        {
            query = query.Where(s => s.Enabled == enabled.Value);
        }
        
        return await query.OrderBy(s => s.Name).ToListAsync();
    }

    public async Task<SftpConfig> CreateAsync(SftpConfig config)
    {
        config.CreatedAt = DateTime.UtcNow;
        config.UpdatedAt = DateTime.UtcNow;
        
        var id = await _db.Insertable(config).ExecuteReturnIdentityAsync();
        config.Id = id;
        return config;
    }

    public async Task<bool> UpdateAsync(SftpConfig config)
    {
        config.UpdatedAt = DateTime.UtcNow;
        
        var result = await _db.Updateable(config)
            .Where(s => s.Id == config.Id)
            .ExecuteCommandAsync();
        
        return result > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var result = await _db.Deleteable<SftpConfig>()
            .Where(s => s.Id == id)
            .ExecuteCommandAsync();
        
        return result > 0;
    }
}

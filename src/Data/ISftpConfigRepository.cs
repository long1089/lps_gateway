using LpsGateway.Data.Models;

namespace LpsGateway.Data;

/// <summary>
/// SFTP配置仓储接口
/// </summary>
public interface ISftpConfigRepository
{
    Task<SftpConfig?> GetByIdAsync(int id);
    Task<List<SftpConfig>> GetAllAsync(bool? enabled = null);
    Task<SftpConfig> CreateAsync(SftpConfig config);
    Task<bool> UpdateAsync(SftpConfig config);
    Task<bool> DeleteAsync(int id);
}

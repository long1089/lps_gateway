using LpsGateway.Data.Models;

namespace LpsGateway.Data;

/// <summary>
/// 文件记录仓储接口
/// </summary>
public interface IFileRecordRepository
{
    /// <summary>
    /// 根据ID获取文件记录
    /// </summary>
    Task<FileRecord?> GetByIdAsync(int id);
    
    /// <summary>
    /// 获取所有文件记录
    /// </summary>
    Task<List<FileRecord>> GetAllAsync();
    
    /// <summary>
    /// 根据状态获取文件记录
    /// </summary>
    Task<List<FileRecord>> GetByStatusAsync(string status);
    
    /// <summary>
    /// 根据报表类型获取文件记录
    /// </summary>
    Task<List<FileRecord>> GetByReportTypeIdAsync(int reportTypeId);
    
    /// <summary>
    /// 根据状态和报表类型获取文件记录
    /// </summary>
    Task<List<FileRecord>> GetByStatusAndReportTypeAsync(string status, int reportTypeId);
    
    /// <summary>
    /// 创建文件记录
    /// </summary>
    Task<int> CreateAsync(FileRecord fileRecord);
    
    /// <summary>
    /// 更新文件记录
    /// </summary>
    Task<bool> UpdateAsync(FileRecord fileRecord);
    
    /// <summary>
    /// 删除文件记录
    /// </summary>
    Task<bool> DeleteAsync(int id);
    
    /// <summary>
    /// 批量获取已下载状态的文件记录（用于文件传输初始化）
    /// </summary>
    Task<List<FileRecord>> GetDownloadedFilesForTransferAsync(int? reportTypeId = null);
}

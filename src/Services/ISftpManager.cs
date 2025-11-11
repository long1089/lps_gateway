namespace LpsGateway.Services;

/// <summary>
/// SFTP管理器接口
/// </summary>
public interface ISftpManager
{
    /// <summary>
    /// 从SFTP服务器下载文件
    /// </summary>
    /// <param name="sftpConfigId">SFTP配置ID</param>
    /// <param name="remoteFilePath">远程文件路径</param>
    /// <param name="localFilePath">本地文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>下载是否成功</returns>
    Task<bool> DownloadFileAsync(int sftpConfigId, string remoteFilePath, string localFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出SFTP服务器上的文件
    /// </summary>
    /// <param name="sftpConfigId">SFTP配置ID</param>
    /// <param name="remotePath">远程路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文件列表</returns>
    Task<List<string>> ListFilesAsync(int sftpConfigId, string remotePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 测试SFTP连接
    /// </summary>
    /// <param name="sftpConfigId">SFTP配置ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>连接是否成功</returns>
    Task<bool> TestConnectionAsync(int sftpConfigId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 解析路径模板（支持 {yyyy}/{MM}/{dd}/{HH}/{mm}）
    /// </summary>
    /// <param name="template">路径模板</param>
    /// <param name="dateTime">日期时间</param>
    /// <returns>解析后的路径</returns>
    string ParsePathTemplate(string template, DateTime dateTime);
}

using LpsGateway.Data;
using Renci.SshNet;
using System.Text;

namespace LpsGateway.Services;

/// <summary>
/// SFTP管理器实现
/// </summary>
public class SftpManager : ISftpManager
{
    private readonly ISftpConfigRepository _sftpConfigRepository;
    private readonly ILogger<SftpManager> _logger;
    private readonly SemaphoreSlim _semaphore;

    public SftpManager(ISftpConfigRepository sftpConfigRepository, ILogger<SftpManager> logger)
    {
        _sftpConfigRepository = sftpConfigRepository;
        _logger = logger;
        _semaphore = new SemaphoreSlim(1, 1); // Default concurrency limit
    }

    /// <inheritdoc />
    public async Task<bool> DownloadFileAsync(int sftpConfigId, string remoteFilePath, string localFilePath, CancellationToken cancellationToken = default)
    {
        var config = await _sftpConfigRepository.GetByIdAsync(sftpConfigId);
        if (config == null)
        {
            _logger.LogError("SFTP配置不存在: {SftpConfigId}", sftpConfigId);
            return false;
        }

        if (!config.Enabled)
        {
            _logger.LogWarning("SFTP配置已禁用: {SftpConfigId}", sftpConfigId);
            return false;
        }

        // 等待信号量以控制并发
        await _semaphore.WaitAsync(cancellationToken);
        
        try
        {
            using var client = CreateSftpClient(config);
            
            _logger.LogInformation("连接到SFTP服务器: {Host}:{Port}", config.Host, config.Port);
            await Task.Run(() => client.Connect(), cancellationToken);

            if (!client.IsConnected)
            {
                _logger.LogError("无法连接到SFTP服务器: {Host}:{Port}", config.Host, config.Port);
                return false;
            }

            _logger.LogInformation("开始下载文件: {RemoteFilePath} -> {LocalFilePath}", remoteFilePath, localFilePath);
            
            // 确保本地目录存在
            var localDir = Path.GetDirectoryName(localFilePath);
            if (!string.IsNullOrEmpty(localDir) && !Directory.Exists(localDir))
            {
                Directory.CreateDirectory(localDir);
            }

            // 流式下载文件
            await using var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await Task.Run(() => client.DownloadFile(remoteFilePath, fileStream), cancellationToken);

            _logger.LogInformation("文件下载完成: {RemoteFilePath} -> {LocalFilePath}", remoteFilePath, localFilePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载文件失败: {RemoteFilePath} -> {LocalFilePath}", remoteFilePath, localFilePath);
            return false;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<List<string>> ListFilesAsync(int sftpConfigId, string remotePath, CancellationToken cancellationToken = default)
    {
        var config = await _sftpConfigRepository.GetByIdAsync(sftpConfigId);
        if (config == null)
        {
            _logger.LogError("SFTP配置不存在: {SftpConfigId}", sftpConfigId);
            return new List<string>();
        }

        if (!config.Enabled)
        {
            _logger.LogWarning("SFTP配置已禁用: {SftpConfigId}", sftpConfigId);
            return new List<string>();
        }

        try
        {
            using var client = CreateSftpClient(config);
            
            _logger.LogInformation("连接到SFTP服务器以列出文件: {Host}:{Port}, 路径: {RemotePath}", config.Host, config.Port, remotePath);
            await Task.Run(() => client.Connect(), cancellationToken);

            if (!client.IsConnected)
            {
                _logger.LogError("无法连接到SFTP服务器: {Host}:{Port}", config.Host, config.Port);
                return new List<string>();
            }

            var files = await Task.Run(() => 
                client.ListDirectory(remotePath)
                    .Where(f => !f.IsDirectory && f.Name != "." && f.Name != "..")
                    .Select(f => f.FullName)
                    .ToList(), 
                cancellationToken);

            _logger.LogInformation("列出 {Count} 个文件，路径: {RemotePath}", files.Count, remotePath);
            return files;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "列出文件失败，路径: {RemotePath}", remotePath);
            return new List<string>();
        }
    }

    /// <inheritdoc />
    public async Task<bool> TestConnectionAsync(int sftpConfigId, CancellationToken cancellationToken = default)
    {
        var config = await _sftpConfigRepository.GetByIdAsync(sftpConfigId);
        if (config == null)
        {
            _logger.LogError("SFTP配置不存在: {SftpConfigId}", sftpConfigId);
            return false;
        }

        try
        {
            using var client = CreateSftpClient(config);
            
            _logger.LogInformation("测试SFTP连接: {Host}:{Port}", config.Host, config.Port);
            await Task.Run(() => client.Connect(), cancellationToken);

            var isConnected = client.IsConnected;
            
            if (isConnected)
            {
                _logger.LogInformation("SFTP连接测试成功: {Host}:{Port}", config.Host, config.Port);
            }
            else
            {
                _logger.LogWarning("SFTP连接测试失败: {Host}:{Port}", config.Host, config.Port);
            }

            return isConnected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SFTP连接测试异常: {Host}:{Port}", config.Host, config.Port);
            return false;
        }
    }

    /// <inheritdoc />
    public string ParsePathTemplate(string template, DateTime dateTime)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        var result = template
            .Replace("{yyyy}", dateTime.ToString("yyyy"))
            .Replace("{MM}", dateTime.ToString("MM"))
            .Replace("{dd}", dateTime.ToString("dd"))
            .Replace("{HH}", dateTime.ToString("HH"))
            .Replace("{mm}", dateTime.ToString("mm"))
            .Replace("{ss}", dateTime.ToString("ss"));

        return result;
    }

    /// <summary>
    /// 创建SFTP客户端
    /// </summary>
    private SftpClient CreateSftpClient(Data.Models.SftpConfig config)
    {
        Renci.SshNet.ConnectionInfo connectionInfo;

        if (config.AuthType == "password")
        {
            // 密码认证
            var password = DecryptPassword(config.PasswordEncrypted);
            connectionInfo = new Renci.SshNet.ConnectionInfo(config.Host, config.Port, config.Username,
                new PasswordAuthenticationMethod(config.Username, password));
        }
        else if (config.AuthType == "key")
        {
            // 私钥认证
            if (string.IsNullOrEmpty(config.KeyPath) || !File.Exists(config.KeyPath))
            {
                throw new InvalidOperationException($"私钥文件不存在: {config.KeyPath}");
            }

            PrivateKeyFile keyFile;
            if (!string.IsNullOrEmpty(config.KeyPassphraseEncrypted))
            {
                var passphrase = DecryptPassword(config.KeyPassphraseEncrypted);
                keyFile = new PrivateKeyFile(config.KeyPath, passphrase);
            }
            else
            {
                keyFile = new PrivateKeyFile(config.KeyPath);
            }

            connectionInfo = new Renci.SshNet.ConnectionInfo(config.Host, config.Port, config.Username,
                new PrivateKeyAuthenticationMethod(config.Username, keyFile));
        }
        else
        {
            throw new InvalidOperationException($"不支持的认证类型: {config.AuthType}");
        }

        var client = new SftpClient(connectionInfo)
        {
            OperationTimeout = TimeSpan.FromSeconds(config.TimeoutSec)
        };

        return client;
    }

    /// <summary>
    /// 解密密码 (简单的Base64解码，生产环境应使用KMS或DPAPI)
    /// </summary>
    private string DecryptPassword(string? encryptedPassword)
    {
        if (string.IsNullOrEmpty(encryptedPassword))
        {
            return string.Empty;
        }

        try
        {
            var bytes = Convert.FromBase64String(encryptedPassword);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            _logger.LogWarning("解密密码失败，使用原始值");
            return encryptedPassword;
        }
    }
}

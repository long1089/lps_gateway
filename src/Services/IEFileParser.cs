namespace LpsGateway.Services;

/// <summary>
/// E 文件解析器接口
/// </summary>
public interface IEFileParser
{
    /// <summary>
    /// 解析并保存 E 文件
    /// </summary>
    /// <param name="fileStream">文件流</param>
    /// <param name="commonAddr">公共地址</param>
    /// <param name="typeId">类型标识</param>
    /// <param name="fileName">文件名</param>
    /// <returns>异步任务</returns>
    Task ParseAndSaveAsync(Stream fileStream, string commonAddr, string typeId, string fileName);
}

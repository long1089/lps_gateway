using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using LPSGateway.Data;
using LPSGateway.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LPSGateway.Tests
{
    public class EFileParserTests
    {
        [Fact]
        public async Task ParseAsync_ValidEFile_CallsRepositoryMethods()
        {
            // Arrange
            var mockRepository = new Mock<IEFileRepository>();
            var mockLogger = new Mock<ILogger<EFileParser>>();
            
            mockRepository.Setup(r => r.IsFileProcessedAsync(It.IsAny<string>()))
                .ReturnsAsync(false);
            mockRepository.Setup(r => r.MarkFileProcessedAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            mockRepository.Setup(r => r.UpsertInfoTableAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
                .Returns(Task.CompletedTask);
            mockRepository.Setup(r => r.InsertRecordsAsync(It.IsAny<string>(), It.IsAny<List<Dictionary<string, object?>>>()))
                .Returns(Task.CompletedTask);

            var parser = new EFileParser(mockRepository.Object, mockLogger.Object);

            // Create test E-file content in GBK encoding
            var gbk = Encoding.GetEncoding("GBK");
            var content = @"<basic_info>
@station_id	TEST001
@station_name	Test Station
#001	value1	value2
#002	value3	-99
";

            var data = gbk.GetBytes(content);

            // Act
            await parser.ParseAsync(data, "test_file_001");

            // Assert
            mockRepository.Verify(r => r.IsFileProcessedAsync("test_file_001"), Times.Once);
            mockRepository.Verify(r => r.UpsertInfoTableAsync("basic_info", It.IsAny<Dictionary<string, string>>()), Times.Once);
            mockRepository.Verify(r => r.InsertRecordsAsync("basic_info", It.IsAny<List<Dictionary<string, object?>>>()), Times.Once);
            mockRepository.Verify(r => r.MarkFileProcessedAsync("test_file_001"), Times.Once);
        }

        [Fact]
        public async Task ParseAsync_AlreadyProcessed_SkipsProcessing()
        {
            // Arrange
            var mockRepository = new Mock<IEFileRepository>();
            var mockLogger = new Mock<ILogger<EFileParser>>();
            
            mockRepository.Setup(r => r.IsFileProcessedAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            var parser = new EFileParser(mockRepository.Object, mockLogger.Object);
            var data = Encoding.UTF8.GetBytes("<test>\n@key\tvalue\n");

            // Act
            await parser.ParseAsync(data, "test_file_002");

            // Assert
            mockRepository.Verify(r => r.IsFileProcessedAsync("test_file_002"), Times.Once);
            mockRepository.Verify(r => r.UpsertInfoTableAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()), Times.Never);
            mockRepository.Verify(r => r.InsertRecordsAsync(It.IsAny<string>(), It.IsAny<List<Dictionary<string, object?>>>()), Times.Never);
        }

        [Fact]
        public async Task ParseAsync_MultipleTablesInFile_ProcessesAll()
        {
            // Arrange
            var mockRepository = new Mock<IEFileRepository>();
            var mockLogger = new Mock<ILogger<EFileParser>>();
            
            mockRepository.Setup(r => r.IsFileProcessedAsync(It.IsAny<string>()))
                .ReturnsAsync(false);
            mockRepository.Setup(r => r.MarkFileProcessedAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            mockRepository.Setup(r => r.UpsertInfoTableAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
                .Returns(Task.CompletedTask);
            mockRepository.Setup(r => r.InsertRecordsAsync(It.IsAny<string>(), It.IsAny<List<Dictionary<string, object?>>>()))
                .Returns(Task.CompletedTask);

            var parser = new EFileParser(mockRepository.Object, mockLogger.Object);

            var content = @"<table1>
@key1	value1
#data1	data2
<table2>
@key2	value2
#data3	data4
";

            var data = Encoding.UTF8.GetBytes(content);

            // Act
            await parser.ParseAsync(data, "test_file_003");

            // Assert
            mockRepository.Verify(r => r.UpsertInfoTableAsync("table1", It.IsAny<Dictionary<string, string>>()), Times.Once);
            mockRepository.Verify(r => r.UpsertInfoTableAsync("table2", It.IsAny<Dictionary<string, string>>()), Times.Once);
            mockRepository.Verify(r => r.InsertRecordsAsync("table1", It.IsAny<List<Dictionary<string, object?>>>()), Times.Once);
            mockRepository.Verify(r => r.InsertRecordsAsync("table2", It.IsAny<List<Dictionary<string, object?>>>()), Times.Once);
        }
    }
}

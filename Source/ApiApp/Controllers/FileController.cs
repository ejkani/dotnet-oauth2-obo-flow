using System.Text;
using ApiApp.Services;
using Azure.Storage.Files.DataLake;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiApp.Controllers;

[Authorize]
[ApiController]
public class FileController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<WeatherForecastController> _logger;

    public FileController(IConfiguration configuration, ILogger<WeatherForecastController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Some Docs:
    /// https://docs.microsoft.com/en-us/azure/storage/blobs/data-lake-storage-directory-file-acl-dotnet
    /// </summary>
    /// <returns></returns>
    [HttpGet("/fileData")]
    public async Task<Stream?> GetDataLakeFile()
    {
        // WARNING: THIS IS A SIMPLISTIC EXAMPLE JUST TO ILLUSTRATE THE OBO FLOW.
        // WARNING: CODE SHOULD BE REWRITTEN FOR PRODUCTION SCENARIOS!!!
        var accessToken = await HttpContext.GetTokenAsync("access_token");

        ArgumentNullException.ThrowIfNull(accessToken);
        
        var tenantId                = _configuration.GetSection("AzureAd:TenantId").Value;
        var clientId                = _configuration.GetSection("AzureAd:ClientId").Value;
        var clientSecret            = _configuration.GetSection("AzureAd:ClientSecret").Value;
        var storageAccountName      = _configuration.GetSection("MyAzureStorage:StorageAccountName").Value;
        var fileSystemContainerName = _configuration.GetSection("MyAzureStorage:StorageContainerName").Value;
        var filePath                = _configuration.GetSection("MyAzureStorage:FilePath").Value;

        DataLakeServiceClient? dataLakeClient = null;
        MyDataLakeServices.GetDataLakeServiceClient(ref dataLakeClient, storageAccountName, clientId, clientSecret, tenantId, accessToken);

        try
        {

            var fsClient = dataLakeClient.GetFileSystemClient(fileSystemContainerName);
            var fileClient = fsClient.GetFileClient(filePath);

            // TODO: Investigate using FileDownloadInfo and using Span<T>.
            //Response<FileDownloadInfo> downloadResponse = await fileClient.ReadAsync();

            return await fileClient.OpenReadAsync();
        }
        catch (Exception e)
        {
            // NB: DO NOT DO THIS IN PRODUCTION
            if (e.Message.Contains("Status: 403"))
            {
                // Just return a string saying we didn't have access to the file itself.
                var msg = $"You do not have access to read the file from storage. {Environment.NewLine}Error message: {e.GetBaseException().Message}";
                var byteArray = Encoding.UTF8.GetBytes(msg);

                return new MemoryStream(byteArray);
            }

            Console.WriteLine(e);
            throw;
        }
    }
}

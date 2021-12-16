using ApiApp.Services;
using Azure.Storage.Files.DataLake;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;

namespace ApiApp.Controllers;

[Authorize]
[ApiController]
[Route("[controller]")]
//[RequiredScope(RequiredScopesConfigurationKey = "AzureAd:Scopes")]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    private readonly ILogger<WeatherForecastController> _logger;

    public WeatherForecastController(ILogger<WeatherForecastController> logger)
    {
        _logger = logger;
    }

    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get()
    {
        return Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = DateTime.Now.AddDays(index),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        })
        .ToArray();
    }

    [HttpGet("/fileData")]
    public Stream GetDataLakeFile()
    {
        var isIt = User.Identity?.IsAuthenticated;

        DataLakeServiceClient? dataLakeClient = null;
        MyDataLakeServices.GetDataLakeServiceClient(ref dataLakeClient, "oauth2oboflowdemost47", "", "", "", "");

        var fsClient = dataLakeClient.GetFileSystemClient("");
        var fileClient = fsClient.GetFileClient("fileName");
        return fileClient.OpenRead();
    }
}

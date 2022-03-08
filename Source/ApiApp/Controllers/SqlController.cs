using ApiApp.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Identity.Client;

namespace ApiApp.Controllers;

[Authorize]
[ApiController]
public class SqlController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SqlController> _logger;

    public SqlController(IConfiguration configuration, ILogger<SqlController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Some Docs:
    /// https://docs.microsoft.com/en-us/azure/storage/blobs/data-lake-storage-directory-file-acl-dotnet
    /// </summary>
    /// <returns></returns>
    [HttpGet("/sqlData")]
    public async Task<List<string>> GetSqlData()
    {
        // WARNING: THIS IS A SIMPLISTIC EXAMPLE JUST TO ILLUSTRATE THE OBO FLOW.
        // WARNING: CODE SHOULD BE REWRITTEN FOR PRODUCTION SCENARIOS!!!
        var accessToken = await HttpContext.GetTokenAsync("access_token");

        ArgumentNullException.ThrowIfNull(accessToken);
        
        var tenantId        = _configuration.GetSection("AzureAd:TenantId").Value;
        var clientId        = _configuration.GetSection("AzureAd:ClientId").Value;
        var clientSecret    = _configuration.GetSection("AzureAd:ClientSecret").Value;
        var synapseServer   = _configuration.GetSection("MySynapseSql:Server").Value;
        var synapseDatabase = _configuration.GetSection("MySynapseSql:Database").Value;
        var synapseScope    = _configuration.GetSection("MySynapseSql:Scope").Value;
        var synapseView     = _configuration.GetSection("MySynapseSql:View").Value;

        IConfidentialClientApplication clnt = ConfidentialClientApplicationBuilder
            .Create(clientId)
            .WithClientSecret(clientSecret)
            .WithAuthority(AadAuthorityAudience.AzureAdMyOrg) // TODO: check if this is right
            .WithTenantId(tenantId)
            .Build();

        UserAssertion ua = new UserAssertion(accessToken);

        AuthenticationResult res;
        try
        {
            var isApp = User.IsAppOnly();

            if (isApp)
            {
                var callerClaims = User.Claims.ToList();
                var servicePrincipalObjectId = User.GetAzureAppServicePrincipalObjectId();

                // Get token to resource on behalf of this Api.
                res = await clnt.AcquireTokenForClient(new[] { synapseScope }).ExecuteAsync();
            }
            else
            {
                // Get token on behalf of user via delegated permissions
                res = await clnt.AcquireTokenOnBehalfOf(new[] { synapseScope }, ua).ExecuteAsync();
            }

            
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        

        var resultList = new List<string>();

        try
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();

            builder.DataSource = synapseServer;
            builder.InitialCatalog = synapseDatabase;


            using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
            {
                // Use the new access token - obtained using on-behalf-of flow with the SQL Database
                connection.AccessToken = res.AccessToken;
                connection.Open();

                _logger.LogInformation("Query data example:");

                using (SqlCommand command = new SqlCommand($"SELECT * from {synapseView}", connection))
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (reader.Read())
                        {
                            resultList.Add(reader.GetString(0)
                                //new TableModel()
                                //{
                                //    Id = reader.GetInt32(0),
                                //    Value = reader.GetString(1)
                                //}
                            );
                        }
                    }
                }
            }
        }
        catch (SqlException e)
        {
            _logger.LogInformation("blah {0}...", e.Message);
        }
        return resultList;
    }
}

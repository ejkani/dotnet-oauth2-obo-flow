using Azure.Core;
using Azure.Identity;
using Azure.Storage.Files.DataLake;

namespace ApiApp.Services
{
    public static class MyDataLakeServices
    {
        /// <summary>
        /// Here is the GitHub Issue requesting the OBO Flow for Azure Storage:
        /// https://github.com/Azure/azure-sdk-for-net/issues/16264
        /// </summary>
        /// <param name="dataLakeServiceClient"></param>
        /// <param name="accountName"></param>
        /// <param name="clientID"></param>
        /// <param name="clientSecret"></param>
        /// <param name="tenantID"></param>
        //public static void GetDataLakeServiceClient(
        //    ref DataLakeServiceClient dataLakeServiceClient,
        //    String accountName, String clientID,
        //    string clientSecret, string tenantID)
        //{
        public static void GetDataLakeServiceClient(
            ref DataLakeServiceClient dataLakeServiceClient,
            String accountName, String clientID,
            string clientSecret, string tenantID, string accessToken)
        {

            //TokenCredential credential = new ClientSecretCredential(tenantID, clientID, clientSecret, new TokenCredentialOptions());
            TokenCredential credential = new OnBehalfOfCredential(
                tenantID,
                clientID,
                clientSecret,
                accessToken);

            //var managedCredential = new ManagedIdentityCredential("clientId");

            string dfsUri = "https://" + accountName + ".dfs.core.windows.net";

            dataLakeServiceClient = new DataLakeServiceClient(new Uri(dfsUri), credential);
        }
    }
}

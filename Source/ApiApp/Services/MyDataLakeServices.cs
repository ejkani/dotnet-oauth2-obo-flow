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
                "30397c79-d7a7-4b10-ae41-24df759a3ea6", 
                "5d15b3a2-cdc6-4020-b538-114d13a65274", 
                "Xml7Q~HquOpS60DDQHMqAp-AWb-tYjmsekBGQ",
                accessToken);

            //var managedCredential = new ManagedIdentityCredential("clientId");

            string dfsUri = "https://" + accountName + ".dfs.core.windows.net";

            dataLakeServiceClient = new DataLakeServiceClient(new Uri(dfsUri), credential);
        }
    }
}

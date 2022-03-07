# 1. OAuth2 OBO Flow - On-behalf-of Flow in DotNet

Showcasing an Azure AD On-Behalf-Of (OBO) OAuth2 Flow.

When an api doesn't own its resources or the access to those, it is sometime handy to be able to forward the request down to the next application on behalf of the user (OAUTH2 OBO Flow). This way the api doesn't need to know what access this user/application has to this api's underlying resources. Some good examples are if the underlying resources are protected by Azure AD, like Azure Sql Server or Azure Data Lake with RBAC (Role Based Access) or ACL (Access Control Lists)

![Obo Sequence Diagram](./Docs/Diagrams/oauth2-obo-flow.drawio.svg)

References:

* <https://docs.microsoft.com/en-us/azure/active-directory/develop/v2-oauth2-on-behalf-of-flow>
* <https://docs.microsoft.com/en-us/azure/active-directory/develop/scenario-web-api-call-api-acquire-token?tabs=aspnetcore>
* <https://github.com/Azure/azure-sdk-for-net/issues/16264>

**NOTE:**

* All steps below are executed from the root folder (the same folder as this README file)

## Todo's

* [X] Set ApiApp's `"accessTokenAcceptedVersion": 2,` in manifest programatically.
* [X] Add `oauth2-obo-flow-demo-api-msi-nn` to Ad Group.

## 1.1. Intro

![Obo Sequence Diagram](./Docs/Images/azure-app-file-demo.png)

### 1.1.1. Admin Consents

> Note that you need to have administration rights in your Tenant to grant admin consent.

When calling underlying services, the user has no way of consenting in use of the api/service.
So in this case when the api is calling the Storage from the API on behalf of the user, we **need to add Admin Consent** to `Azure Storage / user_impersonation`.
The scripts below gives you an example on how to do that programmatically.

If admin consent isn't given, this will typically be your error.

```TXT
OnBehalfOfCredential authentication failed.

The user or administrator has not consented to use the application
with ID '<appId>' named '<appName>'.
Send an interactive authorization request for this user and resource.
```

#### 1.1.1.1. Setting Admin consent

This will be done later, but this is showing where the Admin Consent is set for the Api we are creating.

![Admin Consent UI](./Docs/Images/azure-app-admin-consent.png)

## 1.2. Log In To Azure And Set Your Default Subscription

> Upgrade `az cli` with `az upgrade`, since some validation lies within the tool itself.

### 1.2.1. Get your subscriptions

```Powershell

# ------------------------------------------------------------------
# Run login first, so you can see your subscriptions
# ------------------------------------------------------------------
az login

```

> TIP: If you get a message saying: **`Found multiple accounts with the same username...`** then run **`az account clear`** and then **manually log in** to `portal.azure.com` and choose `use another login`. It could be that you e.g. got both a personal and/or a work/school account.

```Powershell

# ------------------------------------------------------------------
# List your Azure subscription in a more readable manner
# ------------------------------------------------------------------
az account list --query "[].{name:name, subscriptionId:id}"

```

### 1.2.2. Set your subscription to use

```Powershell

# ------------------------------------------------------------------
# Set you Azure Subscription name and preferred resource location
# ------------------------------------------------------------------
$subscriptionName = "<YOUR_AZURE_SUBSCRIPTION_NAME>"

```

```Powershell

# ------------------------------------------------------------------
# Then set the subscription name explicitly.
# ------------------------------------------------------------------
az account set -s "$subscriptionName"

```

```Powershell
# ------------------------------------------------------------------
# Verify that you set the correct subscription
# ------------------------------------------------------------------
az account show

```

## 1.3. Start setting variables we're going to use

### 1.3.1. Create simple functions for setting variables

```Powershell

function setCommonVariables($demoName, $resourceLocation)
{
  $global:location              = $resourceLocation
  $global:myDemoNamePrefix      = $demoName
  $global:myDemoNamePrefixShort = $myDemoNamePrefix -replace "-"
  $global:rand                  = Get-Random -Minimum 10 -Maximum 99
  $global:resourceGroup         = "$myDemoNamePrefix-rg-$rand"
  $global:storageAccountName    = "${myDemoNamePrefixShort}st$rand"
  $global:logWorkspaceName      = "${myDemoNamePrefix}-log-$rand"
  $global:appInsightsName       = "${myDemoNamePrefix}-appi-$rand"
  $global:appPlanName           = "${myDemoNamePrefix}-app-plan-$rand"
  $global:adGroupForAccess      = "$myDemoNamePrefix-adgroup-$rand"
}

function setApiAppVariables($localhostApiAppPort)
{
  $global:apiAppName      = "${myDemoNamePrefix}-api-app-$rand"
  $global:apiAppMsi       = "${myDemoNamePrefix}-api-msi-$rand"
  $global:apiAppLocalUrl  = "https://localhost:$localhostApiAppPort"
  $global:apiAppPublicUrl = "https://$apiAppName.azurewebsites.net"
}

function setWebAppVariables($localhostWebAppPort)
{
  $global:webAppName      = "${myDemoNamePrefix}-web-app-$rand"
  $global:webAppMsi       = "${myDemoNamePrefix}-web-msi-$rand"
  $global:webAppLocalUrl  = "https://localhost:$localhostWebAppPort"
  $global:webAppPublicUrl = "https://$webAppName.azurewebsites.net"
}

function setStorageVariables($containerName, $rootFolder)
{
  # NOTE: The Container(File System) name needs to be lowercase.
  $global:storageContainerName  = $containerName
  $global:storageTestFolder     = $rootFolder
  $global:storageTestFilePath   = "$storageTestFolder/TestFile.json"
  $global:localTestDataFilePath = "./TestData/testData.json"
}

```

### 1.3.2. Execute above functions to set variables

```Powershell

# ------------------------------------------------------------------
# Setup and run these functions
# ------------------------------------------------------------------
setCommonVariables "oauth2-obo-flow-demo" "westeurope"
setApiAppVariables "7050"
setWebAppVariables "7051"
setStorageVariables "myfscontainer" "Folder1"

```

## 1.4. Start creating resources

```Powershell

function createResourceGroup()
{
  az group create -n $resourceGroup -l $location
}

function createAzureStorage()
{
  # ---------------------------------------------------------------------
  # Creating file system and folder structure.
  # Create storage account. Be patient, this could take some seconds.
  # ---------------------------------------------------------------------
  az storage account create `
    --name $storageAccountName `
    --location $location `
    --resource-group $resourceGroup `
    --sku Standard_LRS `
    --kind StorageV2 `
    --enable-hierarchical-namespace true

  # ---------------------------------------------------------------------
  # Create the file system (Container)
  # ---------------------------------------------------------------------
  az storage container create `
      --name $storageContainerName `
      --account-name $storageAccountName `
      --public-access off `
      --resource-group $resourceGroup

  # ---------------------------------------------------------------------
  # Need to install an extension to be able to create folders for now
  # ---------------------------------------------------------------------
  az extension add -n storage-preview

  az storage fs directory create `
      --file-system $storageContainerName `
      --name $storageTestFolder `
      --account-name $storageAccountName

}

createResourceGroup
createAzureStorage

```

## 1.5. Creating Azure AD Group and adding you as a member

```Powershell

function createAzureAdGroup()
{
  # ---------------------------------------------------------------------
  # Create Azure AD Group with members
  # ---------------------------------------------------------------------
  # Create an AD Group to manage ACL Access. TODO: Move to own chapter
  az ad group create --display-name $adGroupForAccess --mail-nickname $adGroupForAccess
  $global:adGroupForAccessObjectId = $(az ad group list --display-name $adGroupForAccess --query "[*].[objectId]" --output tsv)
}

function addCurrentUserToAdGroup()
{
  # Adding you to the created group
  $global:currentUserObjectId = $(az ad signed-in-user show --query "objectId" --output tsv)

  az ad group member add `
      --group $adGroupForAccessObjectId `
      --member-id $currentUserObjectId
}

createAzureAdGroup
addCurrentUserToAdGroup

```

### 1.5.1. (Optional) Remove current user from AD Group

Use these commands when you want to test access.

```Powershell

function removeCurrentUserFromAdGroup()
{
  az ad group member remove `
      --group $adGroupForAccessObjectId `
      --member-id $currentUserObjectId
}

function listMembersOfAdGroup()
{
  # ---------------------------------------------------------------------
  # List members
  # ---------------------------------------------------------------------
  echo "Listing members of Ad Group [$adGroupForAccess]:"
  az ad group member list `
      --group  $adGroupForAccessObjectId `
      --query "[].{objectId:objectId, userPrincipalName:userPrincipalName}" --out table
}

# List before removal
listMembersOfAdGroup

# Remove
removeCurrentUserFromAdGroup

# List after removal
listMembersOfAdGroup

```

## 1.6. Setting Access to Azure Storage files and folders

```Powershell

# ---------------------------------------------------------------------
# Set access to storage folders and files.
# (ACL access and default access)
# ---------------------------------------------------------------------
function setAzureStorageAccess()
{
  # ---------------------------------------------------------------------
  # We are not using RBAC (Role Based Access) but more fine grained
  # access with ACL (Access Control Lists)
  # ---------------------------------------------------------------------
  az storage fs access set `
      --acl "user::rwx,group::r-x,group:${adGroupForAccessObjectId}:r-x,mask::r-x,other::---" `
      --path "/" `
      --file-system $storageContainerName `
      --account-name $storageAccountName

  # ---------------------------------------------------------------------
  # Then set access on the next folder
  # ---------------------------------------------------------------------
  az storage fs access set `
      --acl "user::rwx,group::r-x,group:${adGroupForAccessObjectId}:r-x,mask::r-x,other::---" `
      --path $storageTestFolder `
      --file-system $storageContainerName `
      --account-name $storageAccountName

  # ---------------------------------------------------------------------
  # Now setting default access to all objects created beneath this folder
  # ---------------------------------------------------------------------
  az storage fs access set `
      --acl "user::rwx,group::r-x,group:${adGroupForAccessObjectId}:r-x,mask::r-x,other::---,default:user::rwx,default:group::r-x,default:group:${adGroupForAccessObjectId}:rwx,default:mask::rwx,default:other::---" `
      --path $storageTestFolder `
      --file-system $storageContainerName `
      --account-name $storageAccountName
}

setAzureStorageAccess

```

## 1.7. Adding logging and monitoring

```Powershell

function createLogAndMonitoring()
{
  # ---------------------------------------------------------------------
  # Add logging and monitoring
  # ---------------------------------------------------------------------
  # To access the preview Application Insights Azure CLI commands, you first need to run:
  az extension add -n application-insights

  # Create log workspace. Be patient, this could take some seconds.
  az monitor log-analytics workspace create `
      --resource-group $resourceGroup `
      --workspace-name $logWorkspaceName `
      --location $location

  $logWorkspaceId=$(az monitor log-analytics workspace list --query "[?contains(name, '$logWorkspaceName')].[id]" --output tsv)

  # Now you can run the following to create your Application Insights resource:
  az monitor app-insights component create `
      --app $appInsightsName `
      --location $location `
      --resource-group $resourceGroup `
      --application-type web `
      --kind web `
      --workspace $logWorkspaceId
}

createLogAndMonitoring

```

## 1.8. Create Some App Services

Create an App Service Plan first

```Powershell

function createAppServicePlan()
{
  az appservice plan create `
      --name $appPlanName `
      --resource-group $resourceGroup `
      --is-linux `
      --location $location `
      --sku S1
}

createAppServicePlan

```

Then create the Azure App instances

```Powershell

function createAppServices()
{
  # ---------------------------------------------------------------------
  # NOTE: To list runtimes, use: "az webapp list-runtimes"
  # NOTE: Deploying could fail cause of runtime validations in the cli.
  #       If so, upgrade your Azure Cli with "az upgrade"
  # ---------------------------------------------------------------------
  az webapp create `
    --name $apiAppName `
    --plan $appPlanName `
    --runtime "DOTNET:6.0" `
    --resource-group $resourceGroup

  az webapp create `
    --name $webAppName `
    --plan $appPlanName `
    --runtime "DOTNET:6.0" `
    --resource-group $resourceGroup
}

createAppServices

```

## 1.9. Creating app identities

### 1.9.1. Creating the ApiApp

```Powershell

function createApiAppRegistration()
{
  az ad app create `
      --display-name $apiAppMsi
      # --identifier-uris $apiAppIdentifierUrl # NOTE: You can use fqdn's if you use your verified domain name.
      # --reply-urls $apiAppLocalUrl $apiAppPublicUrl # TODO: Seems like it's not needed. Maybe when enabling login with Swagger UI ...
      

  $global:apiAppObjectId            = $(az ad app list --display-name $apiAppMsi --query "[*].[objectId]" --output tsv)
  $global:apiAppId                  = $(az ad app list --display-name $apiAppMsi --query "[*].[appId]" --output tsv)
  $global:apiAppUserImpersonationId = $(az ad sp show --id $apiAppId --query "oauth2Permissions[?value=='user_impersonation'].id | [0]")

  $apiAppIdentifierUrl = "api://$apiAppId"

  # Update the ApplicationUrl Id, since we don't use a domain name, but need to use the AppId.
  az ad app update `
    --id $apiAppId `
    --identifier-uris $apiAppIdentifierUrl

  # Create the Service Principal (Managed App) on top of
  # of the App Registration, to be able to Grant Api permissions later.
  az ad sp create `
    --id $apiAppId

  $global:apiAppSpObjectId = $(az ad sp list --display-name $apiAppMsi --query "[*].[objectId]" --output tsv)
}

createApiAppRegistration

```

Set Api App Permissions

```Powershell

function setApiAppPermissions()
{
  # Define Azure Storage API variables
  $azureStorageResourceAppId  = "e406a681-f3d4-42a8-90b6-c2b029497af1"
  $azureStoragePermissionId   = "03e0da56-190b-40ad-a80c-ea378c433f7f"

  # Add API Permission: Azure Storage / user_impersonation
  az ad app permission add `
    --id $apiAppId `
    --api $azureStorageResourceAppId `
    --api-permissions "$azureStoragePermissionId=Scope"

  # TODO: Add API Permission: Azure Storage / app_impersonation
  # ...
  

  # Doing the ADMIN CONSENT (You need to have admin rights to do this)
  az ad app permission admin-consent --id $apiAppId

  # NOTE:
  # Should not expose secrets like this in a real environment,
  # but doing it simple in this demo
  $global:apiAppSecret = $(az ad app credential reset --id $apiAppObjectId --credential-description "Demo secret" --append --query "password" --output tsv)
}

setApiAppPermissions

```

### 1.9.2. Creating the WebApp

```Powershell

function createWebAppRegistration()
{
  az ad app create `
      --display-name $webAppMsi
      # --reply-urls <NOT APPLICABLE HERE> # NOTE: Setting reply urls further down, since we need to set the type to "Spa" as well.
      # --identifier-uris $apiAppLocalUrl $apiAppPublicUrl # NOTE: You can use this if you use your domain verified domain name

  $global:webAppObjectId = $(az ad app list --display-name $webAppMsi --query "[*].[objectId]" --output tsv)
  $global:webAppId = $(az ad app list --display-name $webAppMsi --query "[*].[appId]" --output tsv)

  # Create the Service Principal (Managed App) on top of
  # of the App Registration, to be able to Grant Api permissions later.
  az ad sp create `
    --id $webAppId

  # TODO: Uncheck ID Token option when creating web app
  # ...

  # TODO: Remove Scopes/user_impersonation ??
  # ...

   # Define access to the ApiApp
  $userImpersonationPermissionId   = "03e0da56-190b-40ad-a80c-ea378c433f7f"

  # Add API Permission: Azure Storage / user_impersonation
  az ad app permission add `
    --id $webAppId `
    --api $apiAppId `
    --api-permissions "$userImpersonationPermissionId=Scope"

  # Add API Permission: Azure Storage / user_impersonation
  az ad app permission add `
    --id $webAppId `
    --api $apiAppId `
    --api-permissions "$apiAppUserImpersonationId=Scope"

  # Invoking "az ad app permission grant is needed to make the change effective.
  # Grant the permission. Defaults to 1 year.
  az ad app permission grant `
    --id $webAppId `
    --api $apiAppId


  # Updating the Apps created, since the "az webapp" command has some limitations.
  # Creating a Json object to use in our MS Graph Ad-App Patch call.
  $webAppUpdateRequest = @{
      spa = @{
          redirectUris = @(
              "$webAppLocalUrl/authentication/login-callback",
              "$webAppPublicUrl/authentication/login-callback"
          )
      }
  }

  # Create the Json Request Body and escaping the double-quotes
  $webAppUpdateRequestBody = $($webAppUpdateRequest | ConvertTo-Json -Compress -Depth 10) -replace '"', '\"'

  # Need to update the web application via Microsoft Graph in order to set the SPA Redirect Urls
  az rest --method PATCH `
      --uri "https://graph.microsoft.com/v1.0/applications/$webAppObjectId" `
      --headers "Content-Type=application/json" `
      --body $webAppUpdateRequestBody

  # Verify the correct SPA Redirect Url
  az rest --method GET --uri "https://graph.microsoft.com/v1.0/applications/$webAppObjectId"
}

createWebAppRegistration

```

## 1.10. Chose to create apps from scratch or use existing apps from this repo

> The apps are already created in this repository, but if you would like to start fresh, choose `Alt 1`.

### 1.10.1. **Alt 1:** Use existing projects from this repo

#### 1.10.1.1. Updating appsettings when using existing solution from this Git repo

> Note: When running these scripts, make sure your terminal is in the git repo root folder.

```Powershell

function updateExistingSourceCode()
{
  $global:tenantId = $(az account show --query "tenantId" --output tsv)

  # Setting the WebApp first
  $webAppSettings = (Get-Content ("./Source/WebApp/wwwroot/appsettings.json") | ConvertFrom-Json)

  # Set values
  $webAppSettings.AzureAd.Authority   = "https://login.microsoftonline.com/$tenantId"
  $webAppSettings.AzureAd.ClientId    = $webAppId
  $webAppSettings.ApiApp.BaseUrl      = $apiAppLocalUrl
  $webAppSettings.ApiApp.DefaultScope = "api://$apiAppId/.default"

  # Write back the appsettings.json file
  $webAppSettings | ConvertTo-Json -Depth 10 | Out-File "./Source/WebApp/wwwroot/appsettings.json" -Force

  # Then setting the Api App settings
  $apiAppSettings = (Get-Content ("./Source/ApiApp/appsettings.json") | ConvertFrom-Json)

  # Set values
  $apiAppSettings.AzureAd.TenantId                    = $tenantId
  $apiAppSettings.AzureAd.ClientId                    = $apiAppId
  $apiAppSettings.AzureAd.ClientSecret                = $apiAppSecret
  $apiAppSettings.MyAzureStorage.StorageAccountName   = $storageAccountName
  $apiAppSettings.MyAzureStorage.StorageContainerName = $storageContainerName
  $apiAppSettings.MyAzureStorage.FilePath             = $storageTestFilePath

  # Write back the appsettings.json file
  $apiAppSettings | ConvertTo-Json -Depth 10 | Out-File "./Source/ApiApp/appsettings.json" -Force
}

updateExistingSourceCode

```

### 1.10.2. **Alt 2:** Creating the app projects from scratch

If you created the projects from scratch, remember that you also need to write the application code as well (or copy from this repo)...

```Powershell

function createNewAppVsProjects()
{
  cd ./Source

  $global:tenantId = $(az account show --query "tenantId" --output tsv)

  dotnet new webapi `
      -n "ApiApp" `
      --framework net6.0 `
      --auth SingleOrg `
      --client-id $apiAppId `
      --tenant-id $tenantId

  dotnet new blazorwasm `
      -n "WebApp" `
      --framework net6.0 `
      --auth SingleOrg `
      --client-id $webAppId `
      --tenant-id $tenantId

  dotnet new sln
  Rename-Item -Path Source.sln -NewName "$myDemoNamePrefix.sln"
  dotnet sln add "ApiApp"
  dotnet sln add "WebApp"

  # Create launch settings json files
  $webAppLaunchSettings = @{
    profiles = @{
      WebApp = @{
        commandName = "Project"
        dotnetRunMessages = $true
        launchBrowser = $true
        inspectUri = "{wsProtocol}://{url.hostname}:{url.port}/_framework/debug/ws-proxy?browser={browserInspectUri}"
        applicationUrl = "$webAppLocalUrl"
        environmentVariables = @{
          ASPNETCORE_ENVIRONMENT= "Development"
        }
      }
    }
  }

  # Write json file to VS Project
  $webAppLaunchSettings | ConvertTo-Json -Depth 10 | Out-File "./WebApp/Properties/launchSettings.json" -Force

  $apiAppLaunchSettings = @{
    profiles = @{
      WebApp = @{
        commandName = "Project"
        dotnetRunMessages = $true
        launchBrowser = $true
        inspectUri = "{wsProtocol}://{url.hostname}:{url.port}/_framework/debug/ws-proxy?browser={browserInspectUri}"
        applicationUrl = "$apiAppLocalUrl"
        environmentVariables = @{
          ASPNETCORE_ENVIRONMENT= "Development"
        }
      }
    }
  }

  # Write json file to VS Project
  $apiAppLaunchSettings | ConvertTo-Json -Depth 10 | Out-File "./ApiApp/Properties/launchSettings.json" -Force

  cd ..
}

createNewAppVsProjects

```


## 1.11. Create a Json file with test data

```Powershell

function createTestData()
{
  $testData = @(
      @{
        theId = "id1"
        theValue = "value1"
      },
      @{
        theId = "id2"
        theValue = "value2"
      }
  )

  $testData | ConvertTo-Json -Depth 2 | Out-File ( New-Item -Path $localTestDataFilePath -Force )

  # Upload this file to the Azure Storage (Gen2) that has been created.
  az storage fs file upload `
      --source $localTestDataFilePath `
      --file-system $storageContainerName `
      --path $storageTestFilePath `
      --account-name $storageAccountName

  # Show the ACL of the uploaded file
  az storage fs access show -p $storageTestFilePath -f $storageContainerName --account-name $storageAccountName
}

createTestData

```

## 1.12. Publish Web App with zip-deployment

```Powershell

function publishWebApp()
{
  # Publish the code to the created instance
  dotnet publish ./Source/WebApp/WebApp.csproj -c Release

  $workingDir = Get-Location
  $webAppPublishFolder = "$workingDir/Source/WebApp/bin/Release/net6.0/publish/"

  # Create the zip
  $webAppPublishZip = "$workingDir/WebApp-publish.zip"

  if(Test-path $webAppPublishZip) {Remove-item $webAppPublishZip}

  Add-Type -assembly "system.io.compression.filesystem"

  [io.compression.zipfile]::CreateFromDirectory($webAppPublishFolder, $webAppPublishZip)

  # Deploy the zipped package. This sometimes responds with a timeout. Retrying this command usually works.
  az webapp deployment source config-zip `
    -g $resourceGroup `
    -n $webAppName `
    --src $webAppPublishZip
}

publishWebApp

```

## 1.13. Publish Api App with zip-deployment

```Powershell

function publishApiApp()
{
  # Publish the code to the created instance
  dotnet publish ./Source/ApiApp/ApiApp.csproj -c Release

  $workingDir = Get-Location
  $apiAppPublishFolder = "$workingDir/Source/ApiApp/bin/Release/net6.0/publish/"

  # Create the zip
  $apiAppPublishZip = "$workingDir/ApiApp-publish.zip"

  if(Test-path $apiAppPublishZip) {Remove-item $apiAppPublishZip}

  Add-Type -assembly "system.io.compression.filesystem"

  [io.compression.zipfile]::CreateFromDirectory($apiAppPublishFolder, $apiAppPublishZip)

  # Deploy the zipped package. This sometimes responds with a timeout. Retrying this command usually works.
  az webapp deployment source config-zip `
    -g $resourceGroup `
    -n $apiAppName `
    --src $apiAppPublishZip
}

publishApiApp

```

## 1.14. Save used variables

Saving some key variables, so we easily can clean up our demo resources later

```Powershell

function saveDemoData()
{
  $demoDataFilePath = "./demoData.json"

  $demoData = @(
      @{
        resourceGroup = $resourceGroup
        apiAppName    = $apiAppName
        apiAppId      = $apiAppId
        webAppName    = $webAppName
        webAppId      = $webAppId
        adGroupName   = $adGroupForAccess
      }
  )

  $demoData | ConvertTo-Json -Depth 2 | Out-File ( New-Item -Path $demoDataFilePath -Force )
}

saveDemoData

```

### 1.14.1. Deleting resources

```Powershell

function deleteDemoResources()
{
  $demoDataFilePath = "./demoData.json"

  $currentDemoData = (Get-Content ($demoDataFilePath) | ConvertFrom-Json)

  # Delete resources based on demo data
  az group delete --name $currentDemoData.resourceGroup
  az ad app delete --id $currentDemoData.webAppId
  az ad app delete --id $currentDemoData.apiAppId
  az ad group delete --group $currentDemoData.adGroupName
}

deleteDemoResources

```

> This is somewhat lengthy example, but it sets up all the bits and pieces for you when creating apps with auth in Azure. Good luck!

## Continue with Synapse Sql

Continue with [**creating a Synapse Sql OBO FLow**](./synapse-obo-flow.md)

.

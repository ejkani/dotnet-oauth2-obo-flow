# Azure Synapse OAuth2 OnBehalfOf Flow

## Make a helper-function to crate passwords

```Powershell

function createPassword {
    param (
        [Parameter(Mandatory)]
        [ValidateRange(4,[int]::MaxValue)]
        [int] $length,
        [int] $upper = 1,
        [int] $lower = 1,
        [int] $numeric = 1,
        [int] $special = 1
    )
    if($upper + $lower + $numeric + $special -gt $length) {
        throw "number of upper/lower/numeric/special char must be lower or equal to length"
    }
    $uCharSet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
    $lCharSet = "abcdefghijklmnopqrstuvwxyz"
    $nCharSet = "0123456789"
    $sCharSet = "/*-+,!?=()@;:._"
    $charSet = ""
    if($upper -gt 0) { $charSet += $uCharSet }
    if($lower -gt 0) { $charSet += $lCharSet }
    if($numeric -gt 0) { $charSet += $nCharSet }
    if($special -gt 0) { $charSet += $sCharSet }
    
    $charSet = $charSet.ToCharArray()
    $rng = New-Object System.Security.Cryptography.RNGCryptoServiceProvider
    $bytes = New-Object byte[]($length)
    $rng.GetBytes($bytes)
 
    $result = New-Object char[]($length)
    for ($i = 0 ; $i -lt $length ; $i++) {
        $result[$i] = $charSet[$bytes[$i] % $charSet.Length]
    }
    $password = (-join $result)
    $valid = $true
    if($upper   -gt ($password.ToCharArray() | Where-Object {$_ -cin $uCharSet.ToCharArray() }).Count) { $valid = $false }
    if($lower   -gt ($password.ToCharArray() | Where-Object {$_ -cin $lCharSet.ToCharArray() }).Count) { $valid = $false }
    if($numeric -gt ($password.ToCharArray() | Where-Object {$_ -cin $nCharSet.ToCharArray() }).Count) { $valid = $false }
    if($special -gt ($password.ToCharArray() | Where-Object {$_ -cin $sCharSet.ToCharArray() }).Count) { $valid = $false }
 
    if(!$valid) {
         $password = createPassword $length $upper $lower $numeric $special
    }
    return $password
}

# length 20, 1 upper, 1 lower, 1 number, 1 special
# createPassword 32 1 1 1 1

```


## Starting by extending our variables

```Powershell

function setSynapseVariables()
{
  $global:adGroupForSynapseAdmin = "$myDemoNamePrefix-synapseadmingroup-$rand"
  $global:synapseName            = "${myDemoNamePrefix}-syn-$rand"
  $global:synapseDbName          = "demo_ondemand"
  $global:synapseSqlView         = "demo.testdata_json"
  $global:synapseAdminName       = "synapseadmin-$rand"
  $global:synapseGrantScriptName = "synapseSqlGrants.sql"
  $global:synapseAdminPassword   = $(createPassword 32 1 1 1 1)
  $global:synapseFileSystem      = "synapsefs"
}

setSynapseVariables

```


## Creating Azure AD Group for SqlAdmin

```Powershell

function createAzureAdGroupForSynapseAdmin()
{
  # ---------------------------------------------------------------------
  # Create Azure AD Group with members
  # ---------------------------------------------------------------------
  # Create an AD Group to manage ACL Access. TODO: Move to own chapter
  az ad group create --display-name $adGroupForSynapseAdmin --mail-nickname $adGroupForSynapseAdmin
  $global:adGroupForSynapseAdminObjectId = $(az ad group list --display-name $adGroupForSynapseAdmin --query "[*].[objectId]" --output tsv)
}

function addCurrentUserToAdGroupForSynapseAdmin()
{
  # Adding you to the created group
  $global:currentUserObjectId = $(az ad signed-in-user show --query "objectId" --output tsv)

  az ad group member add `
      --group $adGroupForSynapseAdminObjectId `
      --member-id $currentUserObjectId
}

createAzureAdGroupForSynapseAdmin
addCurrentUserToAdGroupForSynapseAdmin

```

## Create Synapse Storage

```Powershell

function createSynapseStorage()
{
  # ---------------------------------------------------------------------
  # Create the file system (Container)
  # ---------------------------------------------------------------------
  az storage container create `
      --name $synapseFileSystem `
      --account-name $storageAccountName `
      --public-access off `
      --resource-group $resourceGroup

}

createSynapseStorage

```

## Create Azure Synapse

```Powershell

function createSynapse()
{
  # Create Synapse Workspace
  az synapse workspace create `
    --name  $synapseName `
    --resource-group $resourceGroup `
    --sql-admin-login-password $synapseAdminPassword `
    --sql-admin-login-user $synapseAdminName `
    --storage-account $storageAccountName `
    --file-system $synapseFileSystem `
    --location $location

  $global:synapseWorkspaceDev = $(az synapse workspace show --name $synapseName --resource-group $resourceGroup --query "connectivityEndpoints.dev" --output tsv)
  $global:synapseSqlName = $(az synapse workspace show --name $synapseName --resource-group $resourceGroup --query "connectivityEndpoints.sql" --output tsv)
  $global:synapseSqlOnDemandName = $(az synapse workspace show --name $synapseName --resource-group $resourceGroup --query "connectivityEndpoints.sqlOnDemand" --output tsv)

  # Extract your IP address by calling the Synapse Dev endpoint
  $curlResponse = $(curl -sb -H "Accept: application/json" "$synapseWorkspaceDev") | ConvertFrom-Json -Depth 2
  $clientIpAddress = $curlResponse.message -replace "Client Ip address : "

  # Set firewall rule for your current IP
  az synapse workspace firewall-rule create `
    --end-ip-address $clientIpAddress `
    --start-ip-address $clientIpAddress `
    --name "Allow Client IP" `
    --resource-group $resourceGroup `
    --workspace-name $synapseName

    az synapse sql ad-admin show `
      --resource-group $resourceGroup `
      --workspace-name $synapseName

    # Now it it possible to log into the Synapse Sql Server,
    # e.g. via Azure Data Studio.

    # # TODO: This does not work p.t. 2021-12-21
    # az synapse sql ad-admin create `
    #   --display-name $synapseAdminName `
    #   --object-id $adGroupForSynapseAdminObjectId `
    #   --resource-group $resourceGroup `
    #   --workspace-name $synapseName

    # Add Azure Sql to the Api Permissions
    $azureSqlResourceAppId = "022907d3-0f1b-48f7-badc-1ba6abab6d66"
    $azureSqlPermissionId   = "03e0da56-190b-40ad-a80c-ea378c433f7f"
    $azureSqlPermissionType = "Scope"

    # Add API Permission: Azure Storage / user_impersonation
  az ad app permission add `
    --id $apiAppId `
    --api $azureSqlResourceAppId `
    --api-permissions "$azureSqlPermissionId=$azureSqlPermissionType"

  # Doing the ADMIN CONSENT (You need to have admin rights to do this)
  az ad app permission admin-consent --id $apiAppId

}

createSynapse

```

## Update Solution With Synapse properties

```Powershell

function updateSourceCodeWithSynapseProperties()
{
  # Setting the WebApp first
  $apiAppSettings = (Get-Content ("./Source/ApiApp/appsettings.json") | ConvertFrom-Json)

  # Set values
  $apiAppSettings.MySynapseSql.Server = $synapseSqlOnDemandName
  $apiAppSettings.MySynapseSql.Database = $synapseDbName
  $apiAppSettings.MySynapseSql.View = $synapseSqlView

  # Write back the appsettings.json file
  $apiAppSettings | ConvertTo-Json -Depth 10 | Out-File "./Source/ApiApp/appsettings.json" -Force
}

updateSourceCodeWithSynapseProperties

```

## Create Sql Script to grant our AD Group access

```Powershell

function createSynapseSqlGrantScript()
{
    $testFileFullDfsPath = "https://${storageAccountName}.dfs.core.windows.net/${storageContainerName}/${storageTestFilePath}"
    $global:sqlGrantScript = "
    -- Log in to this sql server db
    -- sql server:   $synapseSqlName
    -- sql database: $synapseSqlOnDemandName
    -- Log in with your Azure AD user and run the below script

    IF NOT EXISTS(SELECT * FROM sys.databases WHERE name = '${synapseDbName}') CREATE DATABASE [${synapseDbName}]

    -- Set collation
    IF NOT EXISTS(SELECT c.Collation FROM (SELECT DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS Collation) c  
    WHERE c.Collation = 'Latin1_General_100_BIN2_UTF8')
    BEGIN
        alter database current collate Latin1_General_100_BIN2_UTF8
    END
    GO

    -- Grant regular users access
    CREATE USER [${adGroupForSynapseAdmin}] FROM EXTERNAL PROVIDER;
    ALTER ROLE db_datareader ADD MEMBER [${adGroupForSynapseAdmin}];
    GO

    -- https://docs.microsoft.com/en-us/azure/synapse-analytics/sql/query-json-files

    CREATE SCHEMA demo
    CREATE OR ALTER VIEW demo.testdata_json 
        AS
          select top 10 *
    from openrowset(
            bulk '${$testFileFullDfsPath}',
            format = 'csv',
            fieldterminator ='0x0b',
            fieldquote = '0x0b'
        ) with (doc nvarchar(max)) as rows

    GO

    -- Select from the VIew
    -- select * from demo.testdata_json

    -- TODO:
    -- SELECT * FROM OPENROWSET(BULK '${$testFileFullDfsPath}', 
    -- FORMAT = 'CSV') AS DATA

    "

    $sqlGrantScript |  Out-File ( New-Item -Path $synapseGrantScriptName -Force )
}

createSynapseSqlGrantScript

```

# Sql Server OAuth2 OnBehalfOf Flow

## Starting by extending our variables

```Powershell

function setSqlVariables()
{
  $global:adGroupForSqlAdmin = "$myDemoNamePrefix-sqladmingroup-$rand"
  $global:sqlServerName      = "${myDemoNamePrefix}-sql-$rand"
  $global:sqlDbName          = "demodb"
  $global:sqlServerAdminName = "sqladmin-$rand"
  $global:sqlGrantScriptName = "sqlGrants.sql"
}

setSqlVariables

```

## Creating Azure AD Group for SqlAdmin

```Powershell

function createAzureAdGroupForSqlAdmin()
{
  # ---------------------------------------------------------------------
  # Create Azure AD Group with members
  # ---------------------------------------------------------------------
  # Create an AD Group to manage ACL Access. TODO: Move to own chapter
  az ad group create --display-name $adGroupForSqlAdmin --mail-nickname $adGroupForSqlAdmin
  $global:adGroupForSqlAdminObjectId = $(az ad group list --display-name $adGroupForSqlAdmin --query "[*].[objectId]" --output tsv)
}

function addCurrentUserToAdGroupForSqlAdmin()
{
  # Adding you to the created group
  $global:currentUserObjectId = $(az ad signed-in-user show --query "objectId" --output tsv)

  az ad group member add `
      --group $adGroupForSqlAdminObjectId `
      --member-id $currentUserObjectId
}

createAzureAdGroupForSqlAdmin
addCurrentUserToAdGroupForSqlAdmin

```

## Create Sql Server and Database

```Powershell

function createSqlServerAndDatabase()
{
    az sql server create `
        --name $sqlServerName `
        --resource-group $resourceGroup `
        --enable-ad-only-auth `
        --external-admin-principal-type Group `
        --external-admin-name $sqlServerAdminName `
        --external-admin-sid $adGroupForSqlAdminObjectId
    
    az sql db create `
        --name $sqlDbName `
        --resource-group $resourceGroup `
        --server $sqlServerName `
        --service-objective S0
}

createSqlServerAndDatabase

```

## Create Sql Script to grant our AD Group access

```Powershell

function createSqlGrantScript()
{
    $global:sqlGrantScript = "
    -- Log in to this sql server db
    -- sql server:   $sqlServerName.database.windows.net
    -- sql database: $sqlDbName
    -- Log in with your Azure AD user and run the below script

    -- Grant regular users access
    CREATE USER [${adGroupForAccess}] FROM EXTERNAL PROVIDER;
    ALTER ROLE db_datareader ADD MEMBER [${adGroupForAccess}];
    GO
    "

    $sqlGrantScript |  Out-File ( New-Item -Path $sqlGrantScriptName -Force )
}

createSqlGrantScript

```

## CBBB

```Powershell
```
## CBBB

```Powershell
```
## CBBB

```Powershell
```
## CBBB

```Powershell
```
## CBBB

```Powershell
```

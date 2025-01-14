// --------------------------------------------------------------------------------------------------------------
// Main bicep file that deploys UI CA App, expecting a lot of existing resources....
// --------------------------------------------------------------------------------------------------------------
// You can test it with these commands:
//   Deploy with existing resources specified in a parameter file:
//     az deployment group create -n manual --resource-group rg_smart_flow_ui_test --template-file 'main.bicep' --parameters main-existing.bicepparam
// --------------------------------------------------------------------------------------------------------------
targetScope = 'resourceGroup'

// you can supply a full application name, or you don't it will append resource tokens to a default suffix
@description('Full Application Name (supply this or use default of prefix+token)')
param applicationName string = ''
@description('If you do not supply Application Name, this prefix will be combined with a token to create a unique applicationName')
param applicationPrefix string = 'ai_doc'

@description('The environment code (i.e. dev, qa, prod)')
param environmentName string = 'dev'
@description('Environment name used by the azd command (optional)')
param azdEnvName string = ''

@description('Primary location for all resources')
param location string = resourceGroup().location

// --------------------------------------------------------------------------------------------------------------
// You need an existing monitoring environment
// --------------------------------------------------------------------------------------------------------------
@description('The name of an existing Log Analytics Workspace')
param existing_LogAnalytics_Name string
@description('The resource group of an existing Log Analytics Workspace')
param existing_LogAnalytics_ResourceGroupName string
@description('If you provide this is will be used instead of creating a new App Insights')
param existing_AppInsights_Name string

// --------------------------------------------------------------------------------------------------------------
// You need an existing Container Registry
// --------------------------------------------------------------------------------------------------------------
@description('If you provide this is will be used instead of creating a new Registry')
param existing_ACR_Name string
@description('If you provide this is will be used instead of creating a new Registry')
param existing_ACR_ResourceGroupName string

// --------------------------------------------------------------------------------------------------------------
// You need an existing Managed Identity
// --------------------------------------------------------------------------------------------------------------
@description('Existing Managed Identity to use')
param existing_Identity_Name string

// --------------------------------------------------------------------------------------------------------------
// You need an existing Key Vault
// --------------------------------------------------------------------------------------------------------------
@description('Existing Key Vault to use')
param existing_KeyVault_Name string

// --------------------------------------------------------------------------------------------------------------
// You need an existing Container App Environment
// --------------------------------------------------------------------------------------------------------------
@description('If you provide this is will be used instead of creating a new Container App Environment')
param existing_ManagedAppEnv_Name string
param existing_ManagedAppEnv_WorkloadProfile_Name string

// --------------------------------------------------------------------------------------------------------------
// You need an existing Cosmos DB
// --------------------------------------------------------------------------------------------------------------
@description('Existing CosmosDb to use')
param existing_Cosmos_Name string

// --------------------------------------------------------------------------------------------------------------
// You need an existing OpenAI Deploy
// --------------------------------------------------------------------------------------------------------------
@description('Name of an existing Cognitive Services account to use')
param existing_OpenAI_Name string
@description('Name of ResourceGroup for an existing  Cognitive Services account to use')
param existing_OpenAI_ResourceGroupName string

// --------------------------------------------------------------------------------------------------------------
// You need an existing Storage Account
// --------------------------------------------------------------------------------------------------------------
@description('Name of an existing Storage account to use')
param existing_StorageAccount_Name string

// --------------------------------------------------------------------------------------------------------------
// You need an existing Storage Account
// --------------------------------------------------------------------------------------------------------------
@description('Name of an existing Search Service to use')
param existing_SearchService_Name string

// --------------------------------------------------------------------------------------------------------------
// You need an existing network
// --------------------------------------------------------------------------------------------------------------
@description('If you provide this is will be used instead of creating a new VNET')
param existing_Vnet_Name string = ''
@description('If you provide this is will be used instead of creating a new VNET')
param vnetPrefix string = '10.2.0.0/16'
@description('If new VNET, this is the Subnet name for the private endpoints')
param subnet1Name string = ''
@description('If new VNET, this is the Subnet addresses for the private endpoints, i.e. 10.2.0.0/26, must have a size of at least /23')
param subnet1Prefix string = '10.2.0.0/23'
@description('If new VNET, this is the Subnet name for the application')
param subnet2Name string = ''
@description('If new VNET, this is the Subnet addresses for the application, i.e. 10.2.2.0/23, must have a size of at least /23')
param subnet2Prefix string = '10.2.2.0/23'

// --------------------------------------------------------------------------------------------------------------
// UI Application Switches
// --------------------------------------------------------------------------------------------------------------
param backendExists bool
@secure()
param backendDefinition object

param useManagedIdentityResourceAccess bool = true
@description('Name of the text embedding model deployment')
param azureEmbeddingDeploymentName string = 'text-embedding'
@description('Name of the chat GPT deployment')
param azureChatGptStandardDeploymentName string = 'gpt-4o'

// --------------------------------------------------------------------------------------------------------------
// Parameter used as a variable
// --------------------------------------------------------------------------------------------------------------
param runDateTime string = utcNow()

// --------------------------------------------------------------------------------------------------------------
// -- Variables -------------------------------------------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------
var resourceToken = toLower(uniqueString(resourceGroup().id, location))
var resourceGroupName = resourceGroup().name

// if user supplied a full application name, use that, otherwise use default prefix and a unique token
var appName = applicationName != '' ? applicationName : '${applicationPrefix}_${resourceToken}'

var deploymentSuffix = '-${runDateTime}'

// if this bicep was called from AZD, then it needs this tag added to the resource group (at a minimum) to deploy successfully...
var azdTag = azdEnvName != '' ? { 'azd-env-name': azdEnvName } : {}

var commonTags = {
  LastDeployed: runDateTime
  Application: appName
  ApplicationName: applicationName
  Environment: environmentName
}
var tags = union(commonTags, azdTag)

// --------------------------------------------------------------------------------------------------------------
// -- Generate Resource Names -----------------------------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------
module resourceNames 'resourcenames.bicep' = {
  name: 'resource-names${deploymentSuffix}'
  params: {
    applicationName: appName
    environmentName: environmentName
  }
}

// --------------------------------------------------------------------------------------------------------------
// -- Existing Monitoring Resources -----------------------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------
module logAnalytics './core/monitor/loganalytics.bicep' = {
  name: 'law${deploymentSuffix}'
  params: {
    existingLogAnalyticsName: existing_LogAnalytics_Name
    existingLogAnalyticsRgName: existing_LogAnalytics_ResourceGroupName
    existingApplicationInsightsName: existing_AppInsights_Name
  }
}

// --------------------------------------------------------------------------------------------------------------
// -- Existing Identity Resource --------------------------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------
module managedIdentity './core/iam/identity.bicep' = {
  name: 'identity${deploymentSuffix}'
  params: {
    existingIdentityName: existing_Identity_Name
  }
}

// --------------------------------------------------------------------------------------------------------------
// -- Existing VNET Resource ------------------------------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------
module vnet './core/networking/vnet.bicep' = {
  name: 'vnet${deploymentSuffix}'
  params: {
    location: location
    existingVirtualNetworkName: existing_Vnet_Name
    vnetAddressPrefix: vnetPrefix
    subnet1Name: !empty(subnet1Name) ? subnet1Name : resourceNames.outputs.vnetPeSubnetName
    subnet1Prefix: subnet1Prefix
    subnet2Name: !empty(subnet2Name) ? subnet2Name : resourceNames.outputs.vnetAppSubnetName
    subnet2Prefix: subnet2Prefix
  }
}

// --------------------------------------------------------------------------------------------------------------
// -- Existing KeyVault Resource --------------------------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------
module keyVault './core/security/keyvault.bicep' = {
  name: 'keyvault${deploymentSuffix}'
  params: {
    existingKeyVaultName: existing_KeyVault_Name
  }
}

// --------------------------------------------------------------------------------------------------------------
// -- Existing Container Registry Resource ----------------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------
module containerRegistry './core/host/containerregistry.bicep' = {
  name: 'containerregistry${deploymentSuffix}'
  params: {
    existingRegistryName: existing_ACR_Name
    existing_ACR_ResourceGroupName: existing_ACR_ResourceGroupName
  }
}

// --------------------------------------------------------------------------------------------------------------
// -- Existing Cosmos Resources ---------------------------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------
module cosmos './core/database/cosmosdb.bicep' = {
  name: 'cosmos${deploymentSuffix}'
  params: {
    existingAccountName: existing_Cosmos_Name
    databaseName: 'ChatHistory'
  }
}

// --------------------------------------------------------------------------------------------------------------
// -- Existing OpenAI Resources ---------------------------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------
module azureOpenAi './core/ai/cognitive-services.bicep' = {
  name: 'openai${deploymentSuffix}'
  params: {
    existing_CogServices_Name: existing_OpenAI_Name
    existing_CogServices_RG_Name: existing_OpenAI_ResourceGroupName
  }
}

// --------------------------------------------------------------------------------------------------------------
// -- Existing Container App Environment Resource ---------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------
module managedEnvironment './core/host/managedEnvironment.bicep' = {
  name: 'caenv${deploymentSuffix}'
  params: {
    existingEnvironmentName: existing_ManagedAppEnv_Name
    location: location
    logAnalyticsWorkspaceName: logAnalytics.outputs.logAnalyticsWorkspaceName
    logAnalyticsRgName: resourceGroupName
  }
}

// --------------------------------------------------------------------------------------------------------------
// -- Existing Container App Environment Resource ---------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------
var storageAccountContainerName = 'content'
var dataProtectionKeysContainerName = 'dataprotectionkeys'
module storageAccount './core/storage/storage-account.bicep' = {
  name: 'storage${deploymentSuffix}'
  params: {
    name: existing_StorageAccount_Name
    containers: [
        storageAccountContainerName
        dataProtectionKeysContainerName
    ]
  }
}

// --------------------------------------------------------------------------------------------------------------
// -- Existing Search Service Resource --------------------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------
module searchService './core/search/search-services.bicep' = {
  name: 'search${deploymentSuffix}'
  params: {
    existingSearchServiceName: existing_SearchService_Name
  }
}

// --------------------------------------------------------------------------------------------------------------
// -- Is this needed or desired? --------------------------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------
// module dashboard './app/dashboard-web.bicep' = {
//   name: 'dashboard${deploymentSuffix}'
//   params: {
//     name: '${abbrs.portalDashboards}${resourceToken}'
//     applicationInsightsName: monitoring.outputs.applicationInsightsName
//     location: location
//     tags: tags
//   }
// }

// --------------------------------------------------------------------------------------------------------------
// -- UI Application Definition ---------------------------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------
module app './app/app.bicep' = {
  name: 'app${deploymentSuffix}'
  params: {
    name: resourceNames.outputs.containerAppUIName
    location: location
    tags: tags
    applicationInsightsName: logAnalytics.outputs.applicationInsightsName
    containerAppsEnvironmentName: managedEnvironment.outputs.name
    containerAppsEnvironmentWorkloadProfileName:  existing_ManagedAppEnv_WorkloadProfile_Name
    containerRegistryName: containerRegistry.outputs.name
    containerRegistryResourceGroup: containerRegistry.outputs.resourceGroupName
    exists: backendExists
    identityName: managedIdentity.outputs.managedIdentityName
    clientId: ''
    clientIdScope: ''
    clientSecretSecretName: ''
    tokenStoreSasSecretName: ''
    appDefinition: {
      settings: (union(
        array(backendDefinition.settings),
        [
          {
            name: 'acrpassword'
            value: 'https://${keyVault.outputs.name}${environment().suffixes.keyvaultDns}/secrets/${registry.outputs.registrySecretName}'
            secretRef: 'acrpassword'
            secret: true
          }
          {
            name: 'AzureStorageAccountEndpoint'
            value: storageAccount.outputs.primaryEndpoints.blob
          }
          {
            name: 'AzureStorageContainer'
            value: storageAccountContainerName
          }
          {
            name: 'AzureSearchServiceEndpoint'
            value: searchService.outputs.endpoint
          }
          {
            name: 'AOAIStandardServiceEndpoint'
            value: azureOpenAi.outputs.endpoint
          }
          {
            name: 'AOAIStandardChatGptDeployment'
            value: azureChatGptStandardDeploymentName
          }
          {
            name: 'AOAIEmbeddingsDeployment'
            value: azureEmbeddingDeploymentName
          }
          {
            name: 'EnableDataProtectionBlobKeyStorage'
            value: string(false)
          }
          {
            name: 'UseManagedIdentityResourceAccess'
            value: string(useManagedIdentityResourceAccess)
          }
          {
            name: 'UseManagedManagedIdentityClientId'
            value: managedIdentity.outputs.managedIdentityClientId
          }
        ],
        (useManagedIdentityResourceAccess)
          ? [
              {
                name: 'CosmosDBEndpoint'
                value: cosmos.outputs.endpoint
              }
            ]
          : [
              {
                name: 'CosmosDBConnectionString'
                value: 'https://${keyVault.outputs.name}${environment().suffixes.keyvaultDns}/secrets/${cosmos.outputs.connectionStringSecretName}'
                secretRef: 'cosmosdbconnectionstring'
                secret: true
              }
              {
                name: 'AzureStorageAccountConnectionString'
                value: 'https://${keyVault.outputs.name}${environment().suffixes.keyvaultDns}/secrets/${storageAccount.outputs.storageAccountConnectionStringSecretName}'
                secretRef: 'azurestorageconnectionstring'
                secret: true
              }
              {
                name: 'AzureSearchServiceKey'
                value: 'https://${keyVault.outputs.name}${environment().suffixes.keyvaultDns}/secrets/${search.outputs.searchKeySecretName}'
                secretRef: 'azuresearchservicekey'
                secret: true
              }
              {
                name: 'AOAIStandardServiceKey'
                value: 'https://${keyVault.outputs.name}${environment().suffixes.keyvaultDns}/secrets/${azureOpenAi.outputs.cognitiveServicesKeySecretName}'
                secretRef: 'aoaistandardservicekey'
                secret: true
              }
            ]
      ))
    }
  }
}

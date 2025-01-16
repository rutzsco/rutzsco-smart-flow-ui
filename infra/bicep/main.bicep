// --------------------------------------------------------------------------------------------------------------
// Main bicep file that deploys UI CA App, expecting a lot of existing resources....
// --------------------------------------------------------------------------------------------------------------
// You can test it with these commands:
//   Deploy with existing resources specified in a parameter file:
//     az deployment group create -n manual-ui --resource-group rg-ai-docs-114-dev --template-file 'main.bicep' --parameters main.existing.bicepparam
// --------------------------------------------------------------------------------------------------------------
targetScope = 'resourceGroup'

// you can supply a full application name, or you don't it will append resource tokens to a default suffix
@description('Full Application Name (supply this or use default of prefix+token)')
param applicationName string = ''
// @description('If you do not supply Application Name, this prefix will be combined with a token to create a unique applicationName')
// param applicationPrefix string = 'ai_doc'

@description('The environment code (i.e. dev, qa, prod)')
param environmentName string = 'dev'
@description('Environment name used by the azd command (optional)')
param azdEnvName string = ''

@description('Primary location for all resources')
param location string = resourceGroup().location

// --------------------------------------------------------------------------------------------------------------
// You need an existing monitoring environment
// --------------------------------------------------------------------------------------------------------------
@description('Name of an existing Log Analytics Workspace to use')
param existing_LogAnalytics_Name string
@description('Name of ResourceGroup for an existing Log Analytics Workspace')
param existing_LogAnalytics_ResourceGroupName string
@description('Name of an existing App Insights to use')
param existing_AppInsights_Name string

// --------------------------------------------------------------------------------------------------------------
// You need an existing Container Registry
// --------------------------------------------------------------------------------------------------------------
@description('Name of an existing Container Registry to use')
param existing_ACR_Name string
@description('Name of ResourceGroup for an existing Container Registry')
param existing_ACR_ResourceGroupName string

// --------------------------------------------------------------------------------------------------------------
// You need an existing Managed Identity
// --------------------------------------------------------------------------------------------------------------
@description('Name of an existing Managed Identity to use')
param existing_Identity_Name string

// --------------------------------------------------------------------------------------------------------------
// You need an existing Key Vault
// --------------------------------------------------------------------------------------------------------------
@description('Name of an existing Key Vault to use')
param existing_KeyVault_Name string

// --------------------------------------------------------------------------------------------------------------
// You need an existing Container App Environment
// --------------------------------------------------------------------------------------------------------------
@description('Name of an existing Container App Environment to use')
param existing_ManagedAppEnv_Name string
@description('Name of ResourceGroup for an existing Container App Environment')
param existing_ManagedAppEnv_ResourceGroupName string
//param existing_ManagedAppEnv_WorkloadProfile_Name string

// --------------------------------------------------------------------------------------------------------------
// You need an existing Cosmos DB
// --------------------------------------------------------------------------------------------------------------
@description('Name of an existing CosmosDb to use')
param existing_Cosmos_Name string

// --------------------------------------------------------------------------------------------------------------
// You need an existing OpenAI Deploy
// --------------------------------------------------------------------------------------------------------------
@description('Name of an existing Cognitive Services account to use')
param existing_OpenAI_Name string
@description('Name of ResourceGroup for an existing Cognitive Services account')
param existing_OpenAI_ResourceGroupName string

// --------------------------------------------------------------------------------------------------------------
// You need an existing Document Intelligence Deploy
// --------------------------------------------------------------------------------------------------------------
@description('Name of an existing Document Intelligence Service to use')
param existing_DocumentIntelligence_Name string
@description('Name of ResourceGroup for an existing Document Intelligence Service')
param existing_DocumentIntelligence_RG_Name string

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
@description('Name of an existing VNET to use')
param existing_Vnet_Name string = ''
@description('This is the existing VNET prefix')
param vnetPrefix string = '10.2.0.0/16'
@description('This is the existing Subnet name for the private endpoints')
param subnet1Name string = ''
@description('This is the existing Subnet addresses for the private endpoints, i.e. 10.2.0.0/26, must have a size of at least /23')
param subnet1Prefix string = '10.2.0.0/23'
@description('This is the existing Subnet name for the application')
param subnet2Name string = ''
@description('This is the existing Subnet addresses for the application, i.e. 10.2.2.0/23, must have a size of at least /23')
param subnet2Prefix string = '10.2.2.0/23'

// --------------------------------------------------------------------------------------------------------------
// UI Application Switches
// --------------------------------------------------------------------------------------------------------------
param backendExists string = 'false'
param useManagedIdentityResourceAccess string = 'true'
// @description('Name of the text embedding model deployment')
// param azureEmbeddingDeploymentName string = 'text-embedding'
// @description('Name of the chat GPT deployment')
// param azureChatGptStandardDeploymentName string = 'gpt-4o'

// --------------------------------------------------------------------------------------------------------------
// Parameter used as a variable
// --------------------------------------------------------------------------------------------------------------
param runDateTime string = utcNow()

// --------------------------------------------------------------------------------------------------------------
// -- Variables -------------------------------------------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------
// var resourceToken = toLower(uniqueString(resourceGroup().id, location))
var resourceGroupName = resourceGroup().name

// if user supplied a full application name, use that, otherwise use default prefix and a unique token
var appName = applicationName // != '' ? applicationName : '${applicationPrefix}_${resourceToken}'

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

var backendExistsBool = toLower(backendExists) == 'true'

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
  name: 'existing_law${deploymentSuffix}'
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
  name: 'existing_identity${deploymentSuffix}'
  params: {
    existingIdentityName: existing_Identity_Name
  }
}

// --------------------------------------------------------------------------------------------------------------
// -- Existing VNET Resource ------------------------------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------
module vnet './core/networking/vnet.bicep' = {
  name: 'existing_vnet${deploymentSuffix}'
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
  name: 'existing_keyvault${deploymentSuffix}'
  params: {
    existingKeyVaultName: existing_KeyVault_Name
  }
}

// --------------------------------------------------------------------------------------------------------------
// -- Existing Container Registry Resource ----------------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------
module containerRegistry './core/host/containerregistry.bicep' = {
  name: 'existing_containerregistry${deploymentSuffix}'
  params: {
    existingRegistryName: existing_ACR_Name
    existing_ACR_ResourceGroupName: existing_ACR_ResourceGroupName
  }
}

// --------------------------------------------------------------------------------------------------------------
// -- Existing Cosmos Resources ---------------------------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------
module cosmos './core/database/cosmosdb.bicep' = {
  name: 'existing_cosmos${deploymentSuffix}'
  params: {
    existingAccountName: existing_Cosmos_Name
    databaseName: 'ChatHistory'
  }
}

// --------------------------------------------------------------------------------------------------------------
// -- Existing OpenAI Resources ---------------------------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------
module azureOpenAi './core/ai/cognitive-services.bicep' = {
  name: 'existing_openai${deploymentSuffix}'
  params: {
    existing_CogServices_Name: existing_OpenAI_Name
    existing_CogServices_RG_Name: existing_OpenAI_ResourceGroupName
    textEmbedding: {
      DeploymentName: 'text-embedding'
      ModelName: 'text-embedding-ada-002'
      ModelVersion: '2'
      DeploymentCapacity: 30
    }
    chatGpt_Standard: {
      DeploymentName: 'gpt-4o'
      ModelName: 'gpt-4o'
      ModelVersion: '2024-05-13'
      DeploymentCapacity: 10
    }
    chatGpt_Premium: {
      DeploymentName: 'gpt-4o'
      ModelName: 'gpt-4o'
      ModelVersion: '2024-05-13'
      DeploymentCapacity: 10
    }
  }
}

// --------------------------------------------------------------------------------------------------------------
// -- Existing Container App Environment Resource ---------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------
module managedEnvironment './core/host/managedEnvironment.bicep' = {
  name: 'existing_ca_env${deploymentSuffix}'
  params: {
    existingEnvironmentName: existing_ManagedAppEnv_Name
    existingEnvironmentResourceGroup: existing_ManagedAppEnv_ResourceGroupName
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
  name: 'existing_storage${deploymentSuffix}'
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
  name: 'existing_search${deploymentSuffix}'
  params: {
    existingSearchServiceName: existing_SearchService_Name
  }
}

// --------------------------------------------------------------------------------------------------------------
// -- Existing Document Intelligence Resource -------------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------
module documentIntelligence './core/ai/document-intelligence.bicep' = {
  name: 'existing_doc_intelligence${deploymentSuffix}'
  params: {
    existing_CogServices_Name: existing_DocumentIntelligence_Name
    existing_CogServices_RG_Name: existing_DocumentIntelligence_RG_Name
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
var settings = [
  { name: 'AOAIStandardServiceEndpoint', value: azureOpenAi.outputs.endpoint }
  { name: 'AOAIPremiumServiceEndpoint', value: azureOpenAi.outputs.endpoint }
  { name: 'AOAIStandardChatGptDeployment', value: 'gpt-4o' }
  { name: 'AOAIPremiumChatGptDeployment', value: 'gpt-4o' }
  { name: 'AOAIEmbeddingsDeployment', value: 'text-embedding' }

  { name: 'AzureSearchServiceEndpoint', value: searchService.outputs.endpoint }

  { name: 'CosmosDbEndpoint', value: cosmos.outputs.endpoint }
  { name: 'CosmosDbDatabaseName', value: 'ChatHistory' }
  { name: 'CosmosDbCollectionName', value: 'ChatTurn' }

  { name: 'DocumentUploadStrategy', value: 'AzureNative' }
  { name: 'EnableDataProtectionBlobKeyStorage', value: 'true' }
  { name: 'StorageAccountName', value: storageAccount.outputs.name }
  { name: 'AzureStorageAccountEndPoint', value: 'https://${storageAccount.outputs.name}.blob.${environment().suffixes.storage}' }
  { name: 'ContentStorageContainer', value: storageAccount.outputs.containerNames[0].name }
  { name: 'AzureStorageUserUploadContainer', value: 'content' }

  { name: 'UserAssignedManagedIdentityClientId', value: managedIdentity.outputs.managedIdentityClientId }
  { name: 'UseManagedIdentityResourceAccess', value: useManagedIdentityResourceAccess }
  { name: 'ProfileFileName', value: 'profiles' }

  { name: 'ShowCollectionsSelection', value: 'true' }
  { name: 'ShowFileUploadSelection', value: 'true' }

  { name: 'AnalysisApiEndpoint', value: 'https://${resourceNames.outputs.containerAppAPIName}.${managedEnvironment.outputs.defaultDomain}' }
  { name: 'AnalysisApiKey', secretRef: 'apikey' }
  { name: 'AzureDocumentIntelligenceEndpoint', value: documentIntelligence.outputs.endpoint }
  { name: 'ApiKey', secretRef: 'apikey' }
  { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: logAnalytics.outputs.appInsightsConnectionString }
  { name: 'AZURE_CLIENT_ID', value: managedIdentity.outputs.managedIdentityClientId }
]
module app './app/app.bicep' = {
  name: 'app${deploymentSuffix}'
  params: {
    name: resourceNames.outputs.containerAppUIName
    location: location
    tags: tags
    applicationInsightsName: logAnalytics.outputs.applicationInsightsName
    managedEnvironmentName: managedEnvironment.outputs.name
    managedEnvironmentRg: existing_ManagedAppEnv_ResourceGroupName
    containerRegistryName: containerRegistry.outputs.name
    imageName: resourceNames.outputs.containerAppUIName
    exists: backendExistsBool
    identityName: managedIdentity.outputs.managedIdentityName
    env: settings
    secrets: {
      cosmos: 'https://${keyVault.outputs.name}${environment().suffixes.keyvaultDns}/secrets/${cosmos.outputs.connectionStringSecretName}'
      aikey: 'https://${keyVault.outputs.name}${environment().suffixes.keyvaultDns}/secrets/${azureOpenAi.outputs.cognitiveServicesKeySecretName}'
      searchkey: 'https://${keyVault.outputs.name}${environment().suffixes.keyvaultDns}/secrets/${searchService.outputs.searchKeySecretName}'
      docintellikey: 'https://${keyVault.outputs.name}${environment().suffixes.keyvaultDns}/secrets/${documentIntelligence.outputs.keyVaultSecretName}'
      apikey: 'https://${keyVault.outputs.name}${environment().suffixes.keyvaultDns}/secrets/api-key'
    }
  }
}

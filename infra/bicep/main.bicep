// --------------------------------------------------------------------------------------------------------------
// Main bicep file that deploys EVERYTHING for the application, with optional parameters for existing resources.
// --------------------------------------------------------------------------------------------------------------
// You can test it with these commands:
//   Most basic of test commands:
//     az deployment group create -n manual --resource-group rg_smart_flow_test --template-file 'main.bicep' --parameters environmentName=dev applicationName=myApp
//   Deploy with existing resources specified in a parameter file:
//     az deployment group create -n manual --resource-group rg_smart_flow_test --template-file 'main.bicep' --parameters main-complete-existing.bicepparam
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
// Need an existing monitoring environment
// --------------------------------------------------------------------------------------------------------------
@description('If you provide this is will be used instead of creating a new Workspace')
param existing_LogAnalytics_Name string
@description('If you provide this is will be used instead of creating a new App Insights')
param existing_AppInsights_Name string

// --------------------------------------------------------------------------------------------------------------
// Need an existing Container Registry
// --------------------------------------------------------------------------------------------------------------
@description('If you provide this is will be used instead of creating a new Registry')
param existing_ACR_Name string = ''
@description('If you provide this is will be used instead of creating a new Registry')
param existing_ACR_ResourceGroupName string = ''

// --------------------------------------------------------------------------------------------------------------
// Need an existing Managed Identity
// --------------------------------------------------------------------------------------------------------------
@description('Existing Managed Identity to use')
param existing_identity_name string

// --------------------------------------------------------------------------------------------------------------
// Need an existing Key Vault
// --------------------------------------------------------------------------------------------------------------
@description('Existing Key Vault to use')
param existingKeyVaultName string

// --------------------------------------------------------------------------------------------------------------
// Need an existing Cosmos DB
// --------------------------------------------------------------------------------------------------------------
@description('Existing CosmosDb to use')
param existingCosmosName string

// --------------------------------------------------------------------------------------------------------------
// Need an existing OpenAI Deploy
// --------------------------------------------------------------------------------------------------------------
@description('Name of an existing Cognitive Services account to use')
param existingCogServicesName string
@description('Name of ResourceGroup for an existing  Cognitive Services account to use')
param existingCogServicesResourceGroup string = resourceGroup().name

// --------------------------------------------------------------------------------------------------------------
// Existing network info
// --------------------------------------------------------------------------------------------------------------
@description('If you provide this is will be used instead of creating a new VNET')
param existingVnetName string = ''
@description('If you provide this is will be used instead of creating a new VNET')
param vnetPrefix string = '10.2.0.0/16'
@description('If new VNET, this is the Subnet name for the private endpoints')
param subnet1Name string = ''
@description('If new VNET, this is the Subnet addresses for the private endpoints, i.e. 10.2.0.0/26') //Provided subnet must have a size of at least /23
param subnet1Prefix string = '10.2.0.0/23'
@description('If new VNET, this is the Subnet name for the application')
param subnet2Name string = ''
@description('If new VNET, this is the Subnet addresses for the application, i.e. 10.2.2.0/23') // Provided subnet must have a size of at least /23
param subnet2Prefix string = '10.2.2.0/23'

// --------------------------------------------------------------------------------------------------------------
// Personal info
// --------------------------------------------------------------------------------------------------------------
@description('My IP address for network access')
param myIpAddress string = ''
@description('Id of the user executing the deployment')
param principalId string = ''

// --------------------------------------------------------------------------------------------------------------
// Other deployment switches
// --------------------------------------------------------------------------------------------------------------
@description('Should resources be created with public access?')
param publicAccessEnabled bool = true
@description('Create DNS Zones?')
param createDnsZones bool = true
@description('Add Role Assignments for the user assigned identity?')
param addRoleAssignments bool = true
@description('Should we run a script to dedupe the KeyVault secrets? (fails on private networks right now)')
param deduplicateKVSecrets bool = false
@description('Set this if you want to append all the resource names with a unique token')
param appendResourceTokens bool = false


param backendExists bool
@secure()
param backendDefinition object

// @description('Workload profiles for the Container Apps environment')
// param containerAppEnvironmentWorkloadProfiles array = []

// @description('Name of the Container Apps Environment workload profile to use for the app')
// param appContainerAppEnvironmentWorkloadProfileName string

// param useManagedIdentityResourceAccess bool = true

// param virtualNetworkName string = ''
// param virtualNetworkResourceGroupName string = ''
// param containerAppSubnetName string = ''
// @description('Address prefix for the container app subnet')
// param containerAppSubnetAddressPrefix string = ''
// param privateEndpointSubnetName string = ''
// @description('Address prefix for the private endpoint subnet')
// param privateEndpointSubnetAddressPrefix string = ''

// @description('Name of the text embedding model deployment')
// param azureEmbeddingDeploymentName string = 'text-embedding'
// param azureEmbeddingModelName string = 'text-embedding-ada-002'
// param embeddingDeploymentCapacity int = 30
// @description('Name of the chat GPT deployment')
// param azureChatGptStandardDeploymentName string = 'chat'
// @description('Name of the chat GPT model. Default: gpt-35-turbo')
// @allowed(['gpt-35-turbo', 'gpt-4', 'gpt-35-turbo-16k', 'gpt-4-16k', 'gpt-4o'])
// param azureOpenAIChatGptStandardModelName string = 'gpt-35-turbo'
// param azureOpenAIChatGptStandardModelVersion string = '0613'
// @description('Capacity of the chat GPT deployment. Default: 10')
// param chatGptStandardDeploymentCapacity int = 10
// @description('Name of the chat GPT deployment')
// param azureChatGptPremiumDeploymentName string = 'chat-gpt4'
// @description('Name of the chat GPT model. Default: gpt-35-turbo')
// @allowed(['gpt-35-turbo', 'gpt-4', 'gpt-35-turbo-16k', 'gpt-4-16k', 'gpt-4o'])
// param azureOpenAIChatGptPremiumModelName string = 'gpt-4o'
// param azureOpenAIChatGptPremiumModelVersion string = '2024-05-13'
// @description('Capacity of the chat GPT deployment. Default: 10')
// param chatGptPremiumDeploymentCapacity int = 10


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
    resourceToken: appendResourceTokens ? resourceToken : ''
  }
}

// --------------------------------------------------------------------------------------------------------------
// -- Existing Monitoring Resources ------------------------------------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------
module logAnalytics './core/monitor/loganalytics.bicep' = {
  name: 'law${deploymentSuffix}'
  params: {
    existingLogAnalyticsName: existing_LogAnalytics_Name
    existingApplicationInsightsName: existing_AppInsights_Name
  }
}

// --------------------------------------------------------------------------------------------------------------
// -- Existing Identity Resource ------------------------------------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------
module managedIdentity './core/iam/identity.bicep' = {
  name: 'identity${deploymentSuffix}'
  params: {
    existingIdentityName: existing_identity_name
  }
}

// --------------------------------------------------------------------------------------------------------------
// -- Existing VNET Resource ------------------------------------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------
module vnet './core/networking/vnet.bicep' = {
  name: 'vnet${deploymentSuffix}'
  params: {
    location: location
    existingVirtualNetworkName: existingVnetName
    vnetAddressPrefix: vnetPrefix
    subnet1Name: !empty(subnet1Name) ? subnet1Name : resourceNames.outputs.vnetPeSubnetName
    subnet1Prefix: subnet1Prefix
    subnet2Name: !empty(subnet2Name) ? subnet2Name : resourceNames.outputs.vnetAppSubnetName
    subnet2Prefix: subnet2Prefix
  }
}

// --------------------------------------------------------------------------------------------------------------
// -- Existing KeyVault Resource ------------------------------------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------
module keyVault './core/security/keyvault.bicep' = {
  name: 'keyvault${deploymentSuffix}'
  params: {
    existingKeyVaultName: existingKeyVaultName
  }
}

// --------------------------------------------------------------------------------------------------------------
// -- Existing Container Registry Resource ------------------------------------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------
module containerRegistry './core/host/containerregistry.bicep' = {
  name: 'containerregistry${deploymentSuffix}'
  params: {
    existingRegistryName: existing_ACR_Name
    existing_ACR_ResourceGroupName: existing_ACR_ResourceGroupName
  }
}

// --------------------------------------------------------------------------------------------------------------
// -- Existing Cosmos Resources ------------------------------------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------
module cosmos './core/database/cosmosdb.bicep' = {
  name: 'cosmos${deploymentSuffix}'
  params: {
    existingAccountName: existingCosmosName
    databaseName: 'ChatHistory'
  }
}

// --------------------------------------------------------------------------------------------------------------
// -- Existing OpenAI Resources ------------------------------------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------
module azureOpenAi './core/ai/cognitive-services.bicep' = {
  name: 'openai${deploymentSuffix}'
  params: {
    existing_CogServices_Name: existingCogServicesName
    existing_CogServices_RG_Name: existingCogServicesResourceGroup
  }
}

// module appsEnv './app/apps-env.bicep' = {
//   name: 'apps-env${deploymentSuffix}'
//   params: {
//     name: '${abbrs.appManagedEnvironments}${resourceToken}'
//     location: location
//     tags: tags
//     applicationInsightsName: monitoring.outputs.applicationInsightsName
//     logAnalyticsWorkspaceName: monitoring.outputs.logAnalyticsWorkspaceName
//     containerAppSubnetId: ''
//     containerAppEnvironmentWorkloadProfiles: containerAppEnvironmentWorkloadProfiles
//   }
// }

var storageAccountContainerName = 'content'
var dataProtectionKeysContainerName = 'dataprotectionkeys'

module storageAccount './core/storage/storage-account.bicep' = {
  name: 'storage${deploymentSuffix}'
  params: {
    name: existing_StorageAccount_Name
    location: location
    tags: tags
    deploymentSuffix: deploymentSuffix
    keyVaultName: keyVault.outputs.name
    containers: [
      {
        name: storageAccountContainerName
      }
      {
        name: dataProtectionKeysContainerName
      }
    ]
    publicNetworkAccess: 'Enabled'
    allowBlobPublicAccess: true
    privateEndpointSubnetId: ''
    privateEndpointName: ''
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
    useManagedIdentityResourceAccess: useManagedIdentityResourceAccess
    managedIdentityPrincipalId: managedIdentity.outputs.identityPrincipalId
  }
}

// module search './app/search-services.bicep' = {
//   name: 'search${deploymentSuffix}'
//   params: {
//     location: location
//     keyVaultName: keyVault.outputs.name
//     name: '${abbrs.searchSearchServices}${resourceToken}'
//     deploymentSuffix: deploymentSuffix
//     publicNetworkAccess: 'enabled'
//     privateEndpointSubnetId: ''
//     privateEndpointName: ''
//     useManagedIdentityResourceAccess: useManagedIdentityResourceAccess
//     managedIdentityPrincipalId: managedIdentity.outputs.identityPrincipalId
//   }
// }

module cognitiveSecret './shared/keyvault-cognitive-secret.bicep' = {
  name: 'openai-secret${deploymentSuffix}'
  params: {
    cognitiveServiceName: azureOpenAi.outputs.name
    cognitiveServiceResourceGroup: azureOpenAi.outputs.resourceGroupName
    keyVaultName: keyVault.outputs.name
    name: azureOpenAi.outputs.cognitiveServicesKeySecretName
  }
}

// module dashboard './app/dashboard-web.bicep' = {
//   name: 'dashboard${deploymentSuffix}'
//   params: {
//     name: '${abbrs.portalDashboards}${resourceToken}'
//     applicationInsightsName: monitoring.outputs.applicationInsightsName
//     location: location
//     tags: tags
//   }
// }

var appDefinition = {
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
        value: search.outputs.endpoint
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
        value: managedIdentity.outputs.identityClientId
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

module app './app/app.bicep' = {
  name: 'app${deploymentSuffix}'
  params: {
    name: '${abbrs.appContainerApps}backend-${resourceToken}'
    location: location
    tags: tags
    applicationInsightsName: monitoring.outputs.applicationInsightsName
    containerAppsEnvironmentName: appsEnv.outputs.name
    containerAppsEnvironmentWorkloadProfileName: appContainerAppEnvironmentWorkloadProfileName
    containerRegistryName: registry.outputs.name
    containerRegistryResourceGroup: registry.outputs.resourceGroupName
    exists: backendExists
    appDefinition: appDefinition
    identityName: managedIdentity.outputs.identityName
    clientId: ''
    clientIdScope: ''
    clientSecretSecretName: ''
    tokenStoreSasSecretName: ''
  }
}

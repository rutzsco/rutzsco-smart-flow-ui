@description('The location for all resources.')
param location string = resourceGroup().location

@description('Base name for all resources')
param baseName string = 'smartflow'

@description('Environment name (dev, test, prod)')
@allowed([
  'dev'
  'test'
  'prod'
])
param environmentName string = 'dev'

// Generate unique names for resources
var uniqueSuffix = uniqueString(resourceGroup().id)
var searchServiceName = '${baseName}-search-${environmentName}-${uniqueSuffix}'
var storageAccountName = toLower('${take(replace(replace(baseName, '-', ''), '_', ''), 10)}${take(environmentName, 3)}${take(uniqueSuffix, 8)}')
var cosmosDbAccountName = '${baseName}-cosmos-${environmentName}-${uniqueSuffix}'
var containerAppEnvName = '${baseName}-env-${environmentName}'
var containerAppName = '${baseName}-app-${environmentName}'

// Azure AI Search Service
resource searchService 'Microsoft.Search/searchServices@2023-11-01' = {
  name: searchServiceName
  location: location
  sku: {
    name: environmentName == 'prod' ? 'standard' : 'basic'
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
  }
  identity: {
    type: 'SystemAssigned'
  }
}

// Storage Account
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
  }
}

// Blob Service for Storage Account
resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    cors: {
      corsRules: [
        {
          allowedOrigins: ['*']
          allowedMethods: ['GET', 'POST', 'PUT']
          allowedHeaders: ['*']
          exposedHeaders: ['*']
          maxAgeInSeconds: 3600
        }
      ]
    }
  }
}

// Container in Blob Service
resource container 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: 'documents'
  properties: {
    publicAccess: 'None'
  }
}

// Role assignment for search service to access storage
resource searchStorageBlobDataReaderRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, searchService.id, 'storageDataReader')
  scope: storageAccount
  properties: {
    principalId: searchService.identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1') // Storage Blob Data Reader role ID
    principalType: 'ServicePrincipal'
  }
  dependsOn: [
    searchService
    storageAccount
  ]
}

// Cosmos DB Account
resource cosmosDbAccount 'Microsoft.DocumentDB/databaseAccounts@2023-11-15' = {
  name: cosmosDbAccountName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    capabilities: [
      {
        name: 'EnableServerless'
      }
    ]
    databaseAccountOfferType: 'Standard'
  }
}

// Cosmos DB Database
resource cosmosDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2023-11-15' = {
  parent: cosmosDbAccount
  name: 'SmartFlowDB'
  properties: {
    resource: {
      id: 'SmartFlowDB'
    }
  }
}

// Cosmos DB Container
resource cosmosContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-11-15' = {
  parent: cosmosDatabase
  name: 'Documents'
  properties: {
    resource: {
      id: 'Documents'
      partitionKey: {
        paths: [
          '/id'
        ]
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        includedPaths: [
          {
            path: '/*'
          }
        ]
      }
    }
  }
}

// Container App Environment
resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: containerAppEnvName
  location: location
  properties: {
    zoneRedundant: false
  }
}

// Container App
resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: containerAppName
  location: location
  properties: {
    managedEnvironmentId: containerAppEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 80
        allowInsecure: false
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
    }
    template: {
      containers: [
        {
          name: 'smart-flow-app'
          image: 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest' // Replace with your actual image
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'SEARCH_SERVICE_ENDPOINT'
              value: searchService.properties.hostingMode == 'default' ? 'https://${searchService.name}.search.windows.net' : ''
            }
            {
              name: 'SEARCH_SERVICE_KEY'
              value: listAdminKeys(searchService.id, searchService.apiVersion).primaryKey
            }
            {
              name: 'STORAGE_CONNECTION_STRING'
              value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${listKeys(storageAccount.id, storageAccount.apiVersion).keys[0].value};EndpointSuffix=core.windows.net'
            }
            {
              name: 'COSMOS_ENDPOINT'
              value: cosmosDbAccount.properties.documentEndpoint
            }
            {
              name: 'COSMOS_KEY'
              value: listKeys(cosmosDbAccount.id, cosmosDbAccount.apiVersion).primaryMasterKey
            }
            {
              name: 'COSMOS_DATABASE'
              value: cosmosDatabase.name
            }
            {
              name: 'COSMOS_CONTAINER'
              value: cosmosContainer.name
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 10
      }
    }
  }
}

// Outputs
output searchServiceName string = searchService.name
output searchServiceEndpoint string = 'https://${searchService.name}.search.windows.net'
output storageAccountName string = storageAccount.name
output cosmosDbAccountName string = cosmosDbAccount.name
output containerAppUrl string = containerApp.properties.configuration.ingress.fqdn

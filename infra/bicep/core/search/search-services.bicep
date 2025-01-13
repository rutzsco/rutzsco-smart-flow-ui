param name string
param location string = resourceGroup().location
param tags object = {}

param sku object = {
  name: 'standard'
  //name: 'basic'
}

@description('Ip Address to allow access to the Azure Search Service')
param myIpAddress string = ''
param partitionCount int = 1
@allowed([
  'enabled'
  'disabled'
])
param publicNetworkAccess string
param replicaCount int = 1

param privateEndpointSubnetId string
param privateEndpointName string
param managedIdentityId string

// --------------------------------------------------------------------------------------------------------------
var resourceGroupName = resourceGroup().name
var searchKeySecretName = 'search-key'

// --------------------------------------------------------------------------------------------------------------
resource search 'Microsoft.Search/searchServices@2024-06-01-preview' = {
  name: name
  location: location
  tags: tags
  identity:{
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityId}': {}
    }
  }
  properties: {
    networkRuleSet: publicNetworkAccess == 'enabled' 
    ? {}
    : {
      bypass: 'AzurePortal'
      ipRules: [
        {
          value: myIpAddress
        }
      ]
    }
    partitionCount: partitionCount
    publicNetworkAccess: publicNetworkAccess
    replicaCount: replicaCount
    authOptions: {
      aadOrApiKey: {
        aadAuthFailureMode: 'http401WithBearerChallenge'
      }
    }
  }
  sku: sku
}

module privateEndpoint '../networking/private-endpoint.bicep' =
  if (!empty(privateEndpointSubnetId)) {
    name: '${name}-private-endpoint'
    params: {
      location: location
      privateEndpointName: privateEndpointName
      groupIds: ['searchService']
      targetResourceId: search.id
      subnetId: privateEndpointSubnetId
    }
  }


output id string = search.id
output endpoint string = 'https://${name}.search.windows.net/'
output name string = search.name
output resourceGroupName string = resourceGroupName
output searchKeySecretName string = searchKeySecretName
output keyVaultSecretName string = searchKeySecretName
output privateEndpointId string = empty(privateEndpointSubnetId) ? '' : privateEndpoint.outputs.privateEndpointId
output privateEndpointName string = privateEndpointName

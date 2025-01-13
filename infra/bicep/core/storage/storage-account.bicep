param name string
param location string = resourceGroup().location
param tags object = {}

//param publicNetworkAccess bool
param privateEndpointSubnetId string
param privateEndpointBlobName string
param privateEndpointQueueName string
param privateEndpointTableName string
@description('Provide the IP address to allow access to the Azure Container Registry')
param myIpAddress string = ''

param containers string[] = []
param kind string = 'StorageV2'
param minimumTlsVersion string = 'TLS1_2'
param sku object = { name: 'Standard_LRS' }

resource storage 'Microsoft.Storage/storageAccounts@2022-05-01' = {
  name: name
  location: location
  tags: tags
  kind: kind
  sku: sku
  properties: {
    minimumTlsVersion: minimumTlsVersion
    allowBlobPublicAccess: false
    publicNetworkAccess: 'Disabled' // publicNetworkAccess ? 'Enabled' : 'Disabled'
    supportsHttpsTrafficOnly: true
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Deny' // publicNetworkAccess ? 'Allow' : 'Deny'
      ipRules: empty(myIpAddress)
        ? []
        : [
            {
              value: myIpAddress
            }
          ]
    }
  }

  resource blobServices 'blobServices' = if (!empty(containers)) {
    name: 'default'
    resource container 'containers' = [
      for container in containers: {
        name: container
        properties: {
          publicAccess: 'None'
        }
      }
    ]
  }
}

module privateEndpointBlob '../networking/private-endpoint.bicep' = if (!empty(privateEndpointSubnetId)) {
  name: '${name}-blob-private-endpoint'
  params: {
    location: location
    privateEndpointName: privateEndpointBlobName
    groupIds: [
      'blob'
    ]
    targetResourceId: storage.id
    subnetId: privateEndpointSubnetId
  }
}

module privateEndpointTable '../networking/private-endpoint.bicep' =
  if (!empty(privateEndpointSubnetId)) {
    name: '${name}-table-private-endpoint'
    params: {
      location: location
      privateEndpointName: privateEndpointTableName
      groupIds: [
        'table'
      ]
      targetResourceId: storage.id
      subnetId: privateEndpointSubnetId
    }
  }

  module privateEndpointQueue '../networking/private-endpoint.bicep' =
  if (!empty(privateEndpointSubnetId)) {
    name: '${name}-queue-private-endpoint'
    params: {
      location: location
      privateEndpointName: privateEndpointQueueName
      groupIds: [
        'queue'
      ]
      targetResourceId: storage.id
      subnetId: privateEndpointSubnetId
    }
  }

output name string = storage.name
output id string = storage.id
output primaryEndpoints object = storage.properties.primaryEndpoints
output containerNames array = [
  for (name, i) in containers: {
    name: name
    url: '${storage.properties.primaryEndpoints.blob}/${name}'
  }
]
output privateEndpointBlobName string = privateEndpointBlobName
output privateEndpointTableName string = privateEndpointTableName
output privateEndpointQueueName string = privateEndpointQueueName

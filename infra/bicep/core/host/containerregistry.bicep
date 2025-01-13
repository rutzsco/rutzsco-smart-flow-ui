@description('Provide an existing name of an Azure Container Registry if using pre-existing one')
param existingRegistryName string = ''
@description('Provide resource group name for an existing Azure Container Registry if using pre-existing one')
param existing_ACR_ResourceGroupName string = ''
@description('Provide a globally unique name of your Azure Container Registry for a new server')
param newRegistryName string = ''
@description('Provide a tier of your Azure Container Registry.')
param acrSku string = ''


@description('Control public access to resources')
param publicAccessEnabled bool = true
@description('Provide the IP address to allow access to the Azure Container Registry')
param myIpAddress string = ''
param privateEndpointSubnetId string = ''
param privateEndpointName string = ''

param location string = resourceGroup().location
param tags object = {}

var useExistingResource = !empty(existingRegistryName)

resource existingContainerRegistry 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' existing = if (useExistingResource) {
  name: existingRegistryName
  scope: resourceGroup(existing_ACR_ResourceGroupName)
}

resource newContainerRegistry 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' = if (!useExistingResource) {
  name: newRegistryName
  location: location
  tags: tags
  sku: {
    name: acrSku
  }
  properties: {
    adminUserEnabled: true // when this is set to false, then the docker push action fails...
    publicNetworkAccess: 'Enabled'
    networkRuleSet: {
      defaultAction: publicAccessEnabled ? 'Allow' : 'Deny'
      ipRules: empty(myIpAddress) ? [] : [
        {
          action: 'Allow'
          value: myIpAddress
        }
      ]
    }
  }
}

module privateEndpoint '../networking/private-endpoint.bicep' =
  if (!useExistingResource) {
    name: '${newRegistryName}-private-endpoint'
    params: {
      location: location
      privateEndpointName: privateEndpointName
      groupIds: ['registry']
      targetResourceId: newContainerRegistry.id
      subnetId: privateEndpointSubnetId
    }
  }


@description('Output the name for later use')
output name string = useExistingResource ? existingContainerRegistry.name : newContainerRegistry.name
@description('Output the login server property for later use')
output loginServer string = useExistingResource ? existingContainerRegistry.properties.loginServer : newContainerRegistry.properties.loginServer
output privateEndpointName string = privateEndpointName

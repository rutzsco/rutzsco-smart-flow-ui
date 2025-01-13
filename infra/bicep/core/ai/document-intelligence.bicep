param existing_CogServices_Name string
param existing_CogServices_RG_Name string
param name string
param location string = resourceGroup().location
param pe_location string = location
param tags object = {}
//param deployments array = []
param kind string = 'FormRecognizer'
param publicNetworkAccess string
param sku object = {
  name: 'S0'
}
param privateEndpointSubnetId string
param privateEndpointName string
@description('Provide the IP address to allow access to the Azure Container Registry')
param myIpAddress string = ''
param managedIdentityId string

// --------------------------------------------------------------------------------------------------------------
var resourceGroupName = resourceGroup().name
var cognitiveServicesKeySecretName = 'form-recognizer-services-key'

// --------------------------------------------------------------------------------------------------------------
resource existingAccount 'Microsoft.CognitiveServices/accounts@2023-05-01' existing =
  if (!empty(existing_CogServices_Name)) {
    scope: resourceGroup(existing_CogServices_RG_Name)
    name: existing_CogServices_Name
  }

// todo: switch to 'br/public:avm/res/cognitive-services/account:0.7.2'
resource account 'Microsoft.CognitiveServices/accounts@2023-05-01' =
  if (empty(existing_CogServices_Name)) {
    name: name
    location: location
    tags: tags
    kind: kind
    identity:{
      type: 'UserAssigned'
      userAssignedIdentities: {
        '${managedIdentityId}': {}
      }
    }
    properties: {
      publicNetworkAccess: publicNetworkAccess
      networkAcls: {
        defaultAction: publicNetworkAccess == 'Enabled' ? 'Allow' : 'Deny'
        ipRules: empty(myIpAddress) ? [] : [
          {
            value: myIpAddress
          }
        ]
      }
      customSubDomainName: name
    }
    sku: sku
  }

module privateEndpoint '../networking/private-endpoint.bicep' =
  if (empty(existing_CogServices_Name) && !empty(privateEndpointSubnetId)) {
    name: '${name}-private-endpoint'
    params: {
      location: pe_location
      privateEndpointName: privateEndpointName
      groupIds: ['account']
      targetResourceId: account.id
      subnetId: privateEndpointSubnetId
    }
  }

output endpoint string = !empty(existing_CogServices_Name)
  ? existingAccount.properties.endpoint
  : account.properties.endpoint
output id string = !empty(existing_CogServices_Name) ? existingAccount.id : account.id
output name string = !empty(existing_CogServices_Name) ? existingAccount.name : account.name
output resourceGroupName string = !empty(existing_CogServices_Name) ? existing_CogServices_RG_Name : resourceGroupName
output keyVaultSecretName string = cognitiveServicesKeySecretName
output privateEndpointName string = privateEndpointName

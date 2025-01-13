param name string
param location string = resourceGroup().location
param tags object = {}

param environmentId string
param registryName string
param managedIdentityId string
param containerName string
param targetPort int
param imageName string
param imageTag string

// var cleanAppName = replace(replace(name, '-', ''), '_', '')

// var containerImage = toLower('${registryName}.azurecr.io/${imageName}:${imageTag}')
// var acrPullDefinitionId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'
// var acrpullCaName = guid(registry.id, cleanAppName, acrPullDefinitionId)

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
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
    environmentId: environmentId
    configuration: {
      ingress: {
        targetPort: targetPort
        external: true
      }
      registries: [
        {
          identity: managedIdentityId
          server: '${registryName}.azurecr.io'
        }
      ]
    }
    template: {
      containers: [
        {
          name: containerName
          image: '${imageName}:${imageTag}'
          resources: {
              cpu: json('0.5')
              memory: '1.0Gi'
          }
          probes: [
            {
              type: 'startup'
              httpGet: {
                path: '/health'
                port: 5000
              }
              initialDelaySeconds: 3
              periodSeconds: 1
            }
            {
              type: 'readiness'
              httpGet: {
                path: '/health'
                port: 5000
              }
              initialDelaySeconds: 3
              periodSeconds: 5
            }
            {
              type: 'liveness'
              httpGet: {
                path: '/health'
                port: 5000
              }
              initialDelaySeconds: 7
              periodSeconds: 5
            }
          ]
        }
      ]
    }
  }
}

// output id string = containerApp.id
output name string = containerApp.name
output id string = containerApp.id
output ip string = containerApp.properties.outboundIpAddresses[0]
output fqdn string = containerApp.properties.configuration.ingress.fqdn

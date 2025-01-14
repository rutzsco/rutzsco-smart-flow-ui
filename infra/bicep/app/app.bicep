param name string
param location string = resourceGroup().location
param tags object = {}
param containerRegistryName string
param imageName string
param managedEnvironmentName string
param managedEnvironmentRg string
param applicationInsightsName string
param exists bool
// @secure()
// param appDefinition object
param identityName string

param clientId string = ''
param clientIdScope string = ''
param clientSecretSecretName string = ''
param tokenStoreSasSecretName string = ''

@description('The secrets required for the container, with the key being the secret name and the value being the key vault URL')
@secure()
param secrets object = {}
@description('The environment variables for the container')
param env array = []

//var appSettingsArray = filter(array(appDefinition.settings), i => i.name != '')
// var secrets = map(
//   filter(appSettingsArray, i => i.?secret != null),
//   i => {
//     name: i.name
//     value: i.value
//     secretRef: i.?secretRef ?? take(replace(replace(toLower(i.name), '_', '-'), '.', '-'), 32)
//   }
// )
// var env = map(
//   filter(appSettingsArray, i => i.?secret == null),
//   i => {
//     name: i.name
//     value: i.value
//   }
// )
var port = 8080

// resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' existing = {
//   scope: resourceGroup(containerRegistryResourceGroup)
//   name: containerRegistryName
// }

resource containerAppEnvironmentResource 'Microsoft.App/managedEnvironments@2024-10-02-preview' existing = {
  name: managedEnvironmentName
  scope: resourceGroup(managedEnvironmentRg)
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: applicationInsightsName
}

resource userIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-07-31-preview' existing = {
  name: identityName
}

module fetchLatestImage './fetch-container-image.bicep' = {
  name: '${imageName}-fetch-image'
  params: {
    exists: exists
    name: imageName
  }
}

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  tags: union(tags, { 'azd-service-name': 'web' })
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppEnvironmentResource.id
    configuration: {
      ingress: {
        external: true
        targetPort: port
        transport: 'auto'
      }
      secrets: [for secret in items(secrets): {
        name: secret.key
        identity: userIdentity.id
        keyVaultUrl: secret.value
      }]
      registries: [
        {
          identity: userIdentity.id
          server: '${containerRegistryName}.azurecr.io'
        }
      ]
    }
    template: {
      containers: [
        {
          image: fetchLatestImage.outputs.?containers[?0].?image ?? 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
          name: 'main'
          env: union(
            [
              {
                name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
                value: applicationInsights.properties.ConnectionString
              }
              {
                name: 'PORT'
                value: '${port}'
              }
            ],
            env
            // map(
            //   secrets,
            //   secret => {
            //     name: secret.name
            //     secretRef: secret.secretRef
            //   }
            // )
          )
          resources: {
            cpu: json('1.0')
            memory: '2.0Gi'
          }
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/healthz/live'
                port: port
              }
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/healthz/ready'
                port: port
              }
            }
            {
              type: 'Startup'
              httpGet: {
                path: '/healthz/startup'
                port: port
              }
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 10
      }
    }
    //  workloadProfileName: containerAppsEnvironmentWorkloadProfileName
  }
}

module appAuthorization './app-authorization.bicep' =
  if (!empty(clientId)) {
    name: 'app-authorization'
    params: {
      appName: containerApp.name
      clientId: clientId
      clientIdScope: clientIdScope
      clientSecretSecretName: clientSecretSecretName
      tokenStoreSasSecretName: tokenStoreSasSecretName
    }
  }

output defaultDomain string = containerAppEnvironmentResource.properties.defaultDomain
output name string = containerApp.name
output uri string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
output id string = containerApp.id

param name string
param location string = resourceGroup().location
param tags object = {}
param containerRegistryName string
param imageName string
param managedEnvironmentName string
param managedEnvironmentRg string
param applicationInsightsName string
param exists bool
param identityName string
param appContainerName string = 'app'
param clientId string = ''
param clientIdScope string = ''
param clientSecretSecretName string = ''
param tokenStoreSasSecretName string = ''
param deploymentSuffix string = ''

@description('The secrets required for the container, with the key being the secret name and the value being the key vault URL')
@secure()
param secrets object = {}
@description('The environment variables for the container')
param env array = []
param secretArray array = []

param port int = 8080

// --------------------------------------------------------------------------------------------------------------
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

module fetchLatestImage '../core/host/fetch-container-image.bicep' = {
  name: 'app-fetch-image${deploymentSuffix}'
  params: {
    exists: exists
    name: imageName
  }
}

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  tags: union(tags, { 'azd-service-name': 'ui' })
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
          name: appContainerName
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
            env,
            map(
              secretArray,
              secret => {
                name: secret.name
                secretRef: secret.secretRef
              }
            )
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

// --------------------------------------------------------------------------------------------------------------
// Outputs
// --------------------------------------------------------------------------------------------------------------
output id string = containerApp.id
output name string = containerApp.name
output defaultDomain string = containerAppEnvironmentResource.properties.defaultDomain
output uri string = 'https://${containerApp.properties.configuration.ingress.fqdn}'

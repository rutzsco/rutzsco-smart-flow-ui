// --------------------------------------------------------------------------------------------------------------
// Parameter file with many existing resources specified
// --------------------------------------------------------------------------------------------------------------
using 'main.bicep'

param applicationName = 'llaz114'
param location = 'eastus2'
param environmentName = 'dev'
param existing_LogAnalytics_Name = 'llaz114-log-dev'
param existing_LogAnalytics_ResourceGroupName = 'rg-ai-docs-114-dev'
param existing_AppInsights_Name = 'llaz114-appi-dev'
param existing_ACR_Name = 'llaz114crdev'
param existing_ACR_ResourceGroupName = 'rg-ai-docs-114-dev'
param existing_Identity_Name = 'llaz114-app-id'
param existing_KeyVault_Name = 'llaz114kvdev'
param existing_ManagedAppEnv_Name = 'llaz114-cae-dev'
param existing_ManagedAppEnv_ResourceGroupName = 'rg-ai-docs-114-dev'
param existing_Cosmos_Name = 'llaz114-cosmos-dev'
param existing_OpenAI_Name = 'llaz114-cog-dev'
param existing_OpenAI_ResourceGroupName = 'rg-ai-docs-114-dev'
param existing_DocumentIntelligence_Name = 'llaz114-cog-fr-dev'
param existing_DocumentIntelligence_RG_Name = 'rg-ai-docs-114-dev'
param existing_StorageAccount_Name = 'llaz114stdev'
param existing_SearchService_Name = 'llaz114-srch-dev'
param existing_Vnet_Name= 'llaz114-vnet-dev'
param vnetPrefix = '10.2.0.0/16'
param subnet1Name = 'snet-prv-endpoint'
param subnet1Prefix = '10.2.0.64/26'
param subnet2Name = 'snet-app'
param subnet2Prefix = '10.2.2.0/23'
param backendExists = false
// param backendDefinition = {
//   settings: []
// }

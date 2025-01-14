// --------------------------------------------------------------------------------------------------------------
// Parameter file with many existing resources specified
// --------------------------------------------------------------------------------------------------------------
using 'main.bicep'

param applicationName = 'ai_ui_doc_review'
param location = 'eastus2'
param environmentName = 'dev'
param existing_LogAnalytics_Name = ''
param existing_LogAnalytics_ResourceGroupName = ''
param existing_AppInsights_Name = ''
param existing_ACR_Name = ''
param existing_ACR_ResourceGroupName = ''
param existing_Identity_Name = ''
param existing_KeyVault_Name = ''
param existing_ManagedAppEnv_Name = ''
param existing_ManagedAppEnv_WorkloadProfile_Name = 'ui'
param existing_Cosmos_Name = ''
param existing_OpenAI_Name = ''
param existing_OpenAI_ResourceGroupName = ''
param existing_StorageAccount_Name = ''
param existing_SearchService_Name = ''
param existing_Vnet_Name= ''
param vnetPrefix = '10.2.0.0/16'
param subnet1Name = 'snet-prv-endpoint'
param subnet1Prefix = '10.2.0.64/26'
param subnet2Name = 'snet-app'
param subnet2Prefix = '10.2.2.0/23'
param backendExists = false
param backendDefinition = {
  settings: []
}

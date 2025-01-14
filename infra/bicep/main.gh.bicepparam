// --------------------------------------------------------------------------------------------------------------
// The most minimal parameters you need - everything else is defaulted
// --------------------------------------------------------------------------------------------------------------
using 'main.bicep'

param applicationName = '#{APP_NAME}#'
param location = '#{RESOURCEGROUP_LOCATION}#'
param environmentName = '#{envCode}#'
param existing_LogAnalytics_Name = '#{LOGANALYTICS_NAME}#'
param existing_LogAnalytics_ResourceGroupName = '#{LOGANALYTICS_RESOURCEGROUPNAME}#'
param existing_AppInsights_Name = '#{APPINSIGHTS_NAME}#'
param existing_ACR_Name = '#{ACR_NAME}#'
param existing_ACR_ResourceGroupName = '#{ACR_RESOURCEGROUPNAME}#'
param existing_Identity_Name = '#{IDENTITY_NAME}#'
param existing_KeyVault_Name = '#{KEYVAULT_NAME}#'
param existing_ManagedAppEnv_Name = '#{MANAGEDAPPENV_NAME}#'
param existing_ManagedAppEnv_WorkloadProfile_Name = '#{MANAGEDAPPENV_WORKLOADPROFILE_NAME}#'
param existing_Cosmos_Name = '#{COSMOS_NAME}#'
param existing_OpenAI_Name = '#{OPENAI_NAME}#'
param existing_OpenAI_ResourceGroupName = '#{OPENAI_RESOURCEGROUPNAME}#'
param existing_StorageAccount_Name = '#{STORAGEACCOUNT_NAME}#'
param existing_SearchService_Name = '#{SEARCHSERVICE_NAME}#'
param existing_Vnet_Name= '#{VNET_NAME}#'
param vnetPrefix = '10.2.0.0/16'
param subnet1Name = 'snet-prv-endpoint'
param subnet1Prefix = '10.2.0.64/26'
param subnet2Name = 'snet-app'
param subnet2Prefix = '10.2.2.0/23'
param backendExists = false

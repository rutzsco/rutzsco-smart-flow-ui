// --------------------------------------------------------------------------------------------------------------
// The most minimal parameters you need - everything else is defaulted
// --------------------------------------------------------------------------------------------------------------
using 'main.bicep'

param applicationName = '#{APP_NAME_NO_DASHES}#'
param location = '#{RESOURCEGROUP_LOCATION}#'
param environmentName = '#{envCode}#'

param existing_LogAnalytics_ResourceGroupName = '#{RESOURCEGROUP_PREFIX}#-#{envCode}#'
param existing_ACR_ResourceGroupName = '#{RESOURCEGROUP_PREFIX}#-#{envCode}#'
param existing_ManagedAppEnv_ResourceGroupName = '#{RESOURCEGROUP_PREFIX}#-#{envCode}#'
param existing_OpenAI_ResourceGroupName = '#{RESOURCEGROUP_PREFIX}#-#{envCode}#'
// param existing_DocumentIntelligence_RG_Name = '#{RESOURCEGROUP_PREFIX}#-#{envCode}#'

param existing_KeyVault_Name = '#{APP_NAME_NO_DASHES}#kv#{envCode}#'
param existing_StorageAccount_Name = '#{APP_NAME_NO_DASHES}#st#{envCode}#'
param existing_ACR_Name = '#{APP_NAME_NO_DASHES}#cr#{envCode}#'

param existing_LogAnalytics_Name = '#{APP_NAME_NO_DASHES}#-log-#{envCode}#'
param existing_AppInsights_Name = '#{APP_NAME_NO_DASHES}#-appi-#{envCode}#'
param existing_Identity_Name = '#{APP_NAME_NO_DASHES}#-app-id'
param existing_ManagedAppEnv_Name = '#{APP_NAME_NO_DASHES}#-cae-#{envCode}#'
param existing_Cosmos_Name = '#{APP_NAME_NO_DASHES}#-cosmos-#{envCode}#'
param existing_OpenAI_Name = '#{APP_NAME_NO_DASHES}#-cog-#{envCode}#'
param existing_DocumentIntelligence_Name = '#{APP_NAME_NO_DASHES}#-cog-fr-#{envCode}#'
param existing_SearchService_Name = '#{APP_NAME_NO_DASHES}#-srch-#{envCode}#'

param existing_Vnet_Name= '#{APP_NAME_NO_DASHES}#-vnet-#{envCode}#'
param vnetPrefix = '10.2.0.0/16'
param subnet1Name = 'snet-prv-endpoint'
param subnet1Prefix = '10.2.0.64/26'
param subnet2Name = 'snet-app'
param subnet2Prefix = '10.2.2.0/23'

param backendExists = '#{backendExists}#'

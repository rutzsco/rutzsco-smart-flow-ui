// --------------------------------------------------------------------------------------------------------------
// The most minimal parameters you need - everything else is defaulted
// --------------------------------------------------------------------------------------------------------------
using 'main.bicep'

param applicationName = '#{appNameLower}#'                   // from the variable group
param location = '#{location}#'                              // from the var_common file
param environmentName = '#{environmentNameLower}#'           // from the pipeline inputs
param existing_LogAnalytics_Name = '#{LogAnalytics_Name}#'
param existing_LogAnalytics_ResourceGroupName = '#{LogAnalytics_ResourceGroupName}#'
param existing_AppInsights_Name = '#{AppInsights_Name}#'
param existing_ACR_Name = '#{ACR_Name}#'
param existing_ACR_ResourceGroupName = '#{ACR_ResourceGroupName}#'
param existing_Identity_Name = '#{Identity_Name}#'
param existing_KeyVault_Name = '#{KeyVault_Name}#'
param existing_ManagedAppEnv_Name = '#{ManagedAppEnv_Name}#'
param existing_ManagedAppEnv_WorkloadProfile_Name = '#{ManagedAppEnv_WorkloadProfile_Name}#'
param existing_Cosmos_Name = '#{Cosmos_Name}#'
param existing_OpenAI_Name = '#{OpenAI_Name}#'
param existing_OpenAI_ResourceGroupName = '#{OpenAI_ResourceGroupName}#'
param existing_StorageAccount_Name = '#{StorageAccount_Name}#'
param existing_SearchService_Name = '#{SearchService_Name}#'
param existing_Vnet_Name= '#{Vnet_Name}#'
param vnetPrefix = '10.2.0.0/16'
param subnet1Name = 'snet-prv-endpoint'
param subnet1Prefix = '10.2.0.64/26'
param subnet2Name = 'snet-app'
param subnet2Prefix = '10.2.2.0/23'
param backendExists = false

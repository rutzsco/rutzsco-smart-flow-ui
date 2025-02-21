// --------------------------------------------------------------------------------------------------------------
// The most minimal parameters you need - everything else is defaulted
// --------------------------------------------------------------------------------------------------------------
using 'main.bicep'

param applicationName = '#{appNameLower}#'                   // from the variable group
param location = '#{location}#'                              // from the var_common file
param environmentName = '#{environmentNameLower}#'           // from the pipeline inputs

param existing_LogAnalytics_ResourceGroupName = '#{ResourceGroupName}#'
param existing_ACR_ResourceGroupName = '#{ResourceGroupName}#'
param existing_ManagedAppEnv_ResourceGroupName = '#{ResourceGroupName}#'
param existing_OpenAI_ResourceGroupName = '#{ResourceGroupName}#'
// param existing_DocumentIntelligence_RG_Name = '#{ResourceGroupName}#'

param existing_LogAnalytics_Name = '#{appNameNoDashesLower}#-log-#{environmentNameLower}#'
param existing_AppInsights_Name = '#{appNameNoDashesLower}#-appi-#{environmentNameLower}#'
param existing_Identity_Name = '#{appNameNoDashesLower}#-app-id'
param existing_ManagedAppEnv_Name = '#{appNameNoDashesLower}#-cae-#{environmentNameLower}#'
param existing_Cosmos_Name = '#{appNameNoDashesLower}#-cosmos-#{environmentNameLower}#'
param existing_OpenAI_Name = '#{appNameNoDashesLower}#-cog-#{environmentNameLower}#'
param existing_DocumentIntelligence_Name = '#{appNameNoDashesLower}#-cog-fr-#{environmentNameLower}#'
param existing_SearchService_Name = '#{appNameNoDashesLower}#-srch-#{environmentNameLower}#'

param existing_StorageAccount_Name = '#{appNameNoDashesLower}#st#{environmentNameLower}#'
param existing_ACR_Name = '#{appNameNoDashesLower}#cr#{environmentNameLower}#'
param existing_KeyVault_Name = '#{appNameNoDashesLower}#kv#{environmentNameLower}#'

param existing_Vnet_Name= '#{appNameNoDashesLower}#-vnet-#{environmentNameLower}#'
param vnetPrefix = '10.2.0.0/16'
param subnet1Name = 'snet-prv-endpoint'
param subnet1Prefix = '10.2.0.64/26'
param subnet2Name = 'snet-app'
param subnet2Prefix = '10.2.2.0/23'

param backendExists = '#{backendExists}#'

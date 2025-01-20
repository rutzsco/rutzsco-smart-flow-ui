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
param existing_DocumentIntelligence_RG_Name = '#{ResourceGroupName}#'

param existing_LogAnalytics_Name = '#{appNameLower}#-log-dev'
param existing_AppInsights_Name = '#{appNameLower}#-appi-dev'
param existing_ACR_Name = '#{appNameLower}#crdev'
param existing_Identity_Name = '#{appNameLower}#-app-id'
param existing_KeyVault_Name = '#{appNameNoDashesLower}#kvdev'
param existing_ManagedAppEnv_Name = '#{appNameLower}#-cae-dev'
param existing_Cosmos_Name = '#{appNameLower}#-cosmos-dev'
param existing_OpenAI_Name = '#{appNameLower}#-cog-dev'
param existing_DocumentIntelligence_Name = '#{appNameLower}#-cog-fr-dev'
param existing_StorageAccount_Name = '#{appNameLower}#stdev'
param existing_SearchService_Name = '#{appNameLower}#-srch-dev'

param existing_Vnet_Name= '#{appNameLower}#-vnet-dev'
param vnetPrefix = '10.2.0.0/16'
param subnet1Name = 'snet-prv-endpoint'
param subnet1Prefix = '10.2.0.64/26'
param subnet2Name = 'snet-app'
param subnet2Prefix = '10.2.2.0/23'

param backendExists = '#{backendExists}#'

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

param existing_LogAnalytics_Name = '#{appNameNoDashesLower}#-log-dev'
param existing_AppInsights_Name = '#{appNameNoDashesLower}#-appi-dev'
param existing_Identity_Name = '#{appNameNoDashesLower}#-app-id'
param existing_ManagedAppEnv_Name = '#{appNameNoDashesLower}#-cae-dev'
param existing_Cosmos_Name = '#{appNameNoDashesLower}#-cosmos-dev'
param existing_OpenAI_Name = '#{appNameNoDashesLower}#-cog-dev'
param existing_DocumentIntelligence_Name = '#{appNameNoDashesLower}#-cog-fr-dev'
param existing_SearchService_Name = '#{appNameNoDashesLower}#-srch-dev'

param existing_StorageAccount_Name = '#{appNameNoDashesLower}#stdev'
param existing_ACR_Name = '#{appNameNoDashesLower}#crdev'
param existing_KeyVault_Name = '#{appNameNoDashesLower}#kvdev'

param existing_Vnet_Name= '#{appNameNoDashesLower}#-vnet-dev'
param vnetPrefix = '10.2.0.0/16'
param subnet1Name = 'snet-prv-endpoint'
param subnet1Prefix = '10.2.0.64/26'
param subnet2Name = 'snet-app'
param subnet2Prefix = '10.2.2.0/23'

param backendExists = '#{backendExists}#'

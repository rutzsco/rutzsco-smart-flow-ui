# Chat Application 

## Resource Deployment

The applications azure resourcees can be deployed with main.bicep file located in the `infra` folder of this repository. The main.bicep file is used to define and deploy Azure infrastructure resources in a declarative way. The following azure resources will be created:

- Azure Container Apps (Application Hosting)
- Storage Account (Blob)
- CosmosDB (NO SQL Application Database)
- Azure AI Search (Vector Database)
- Azure Function (Generate index)
- Key Vault (Store secrets)
- Managed Identity (Retrieve secrets)
- Azure OpenAI (Human language interpretation)

***REFERENCE***
- https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/deploy-vscode
- https://marketplace.visualstudio.com/items?itemName=ms-azuretools.vscode-bicep
- https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd?tabs=winget-windows%2Cbrew-mac%2Cscript-linux&pivots=os-windows

## Code Deployment

### BUILD

```bash
docker login <ACRNAME>.azurecr.io
```
```bash
cd app

docker build . -t custom-chat-copilot-sk-base/chat-app
```

### DEPLOY

#### Azure Developer CLI

**NOTE**: You will need to specify the following variables command if you want to use an API deploy and it's services.  You can get most of these from the azd environment for that API deploy.

```shell
azd env set AZURE_ENV_NAME="<value>"
azd env set APP_NAME_NO_DASHES="<value>"
azd env set AZURE_RESOURCE_GROUP="rg-smartflow-<envName>"
azd env set AZURE_LOCATION="eastus2"
azd env set ENVIRONMENT_NAME="<envName>"
azd env set AZURE_SUBSCRIPTION_ID="<subscription_id>"
azd env set ADMIN_CLIENT_ID="<your_user_client_id>"
azd env set MY_IP="<your_ip_address>"
azd env set AI_ENDPOINT="https://<APP_NAME_NO_DASHES>-cog-dev.openai.azure.com/"
azd env set AI_SEARCH_ENDPOINT="https://<APP_NAME_NO_DASHES>-srch-<envName>.search.windows.net/"
azd env set API_CONTAINER_APP_FQDN="<APP_NAME_NO_DASHES>-ca-api-<envName>.<some_value>.eastus2.azurecontainerapps.io"
azd env set API_KEY="<some_value>"
azd env set AZURE_CONTAINER_REGISTRY_ENDPOINT="<APP_NAME_NO_DASHES>cr<envName>.azurecr.io"
azd env set AZURE_CONTAINER_REGISTRY_NAME="<APP_NAME_NO_DASHES>cr<envName>"
azd env set COSMOS_ENDPOINT="https://<APP_NAME_NO_DASHES>-cosmos-<envName>.documents.azure.com:443/"
azd env set DOCUMENT_INTELLIGENCE_ENDPOINT="https://<APP_NAME_NO_DASHES>-cog-fr-<envName>.cognitiveservices.azure.com/"
azd env set MANAGED_IDENTITY_NAME="<APP_NAME_NO_DASHES>-app-id"
azd env set SERVICE_UI_RESOURCE_EXISTS="true"
azd env set STORAGE_ACCOUNT_CONTAINER="data"
azd env set STORAGE_ACCOUNT_NAME="<APP_NAME_NO_DASHES>st<envName>"
```

Run the following command to build, provision & deploy the application.

```bash
azd up
```

#### Manual

```bash
docker tag custom-chat-copilot-sk-base/chat-app <ACRNAME>.azurecr.io/custom-chat-copilot-sk-base/chat-app:<VERSION>
```

```bash
docker push <ACRNAME>.azurecr.io/custom-chat-copilot-sk-base/chat-app:<VERSION>
```

```bash
az containerapp update --name <APPLICATION_NAME> --resource-group <RESOURCE_GROUP_NAME> --image <IMAGE_NAME>
```

## Application Settings Documentation

### Sample Settings file

***appsettings.Development.json***

```bash
{
  "AzureStorageUserUploadContainer": "content",
  "AzureStorageAccountConnectionString": "",
  "AzureSearchServiceEndpoint": "https://<SERVICENAME>.search.windows.net",
  "AzureSearchServiceKey": "<APIKEY>",
  "AOAIPremiumServiceEndpoint": "https://<SERVICENAME>.openai.azure.com/",
  "AOAIPremiumServiceKey": "<APIKEY>",
  "AOAIPremiumChatGptDeployment": "gpt-4",
  "AOAIStandardServiceEndpoint": "https://<SERVICENAME>.azure-api.net/",
  "AOAIStandardServiceKey": "<APIKEY>",
  "AOAIStandardChatGptDeployment": "chatgpt16k",
  "AOAIEmbeddingsDeployment": "text-embedding",
  "CosmosDBConnectionString": "AccountEndpoint=https://rutzsco-chat-copilot-demo.documents.azure.com:443/;AccountKey=<APIKEY>;",
  "IngestionPipelineAPI": "https://<SERVICENAME>.azurewebsites.net/",
  "IngestionPipelineAPIKey": "<APIKEY>",
  "EnableDataProtectionBlobKeyStorage" : "false"
}
```

This documentation outlines the various application settings used in the configuration of Azure services and other APIs.

### Azure Storage

#### `AzureStorageUserUploadContainer`

- **Description**: The name of the container in Azure Blob Storage where user uploads are stored.
- **Value**: `"content"`

#### `AzureStorageAccountConnectionString`
- **Description**: Connection string for the Azure Storage account, containing authentication information and storage endpoint details.
- **Value**: `"DefaultEndpointsProtocol=https;AccountName=<SERVICENAME>;AccountKey=...;EndpointSuffix=core.windows.net"`

### Azure Search Service

#### `AzureSearchServiceEndpoint`
- **Description**: The endpoint URL for the Azure Search Service.
- **Value**: `"https://<SERVICENAME>.search.windows.net"`

#### `AzureSearchServiceKey`
- **Description**: The primary administrative API key for the Azure Search Service.
- **Value**: `"<APIKEY>"`

### Azure OpenAI Services

#### `AOAIPremiumServiceEndpoint`
- **Description**: The endpoint URL for the Azure OpenAI Premium services.
- **Value**: `"https://<SERVICENAME>.openai.azure.com/"`

#### `AOAIPremiumServiceKey`
- **Description**: The authentication key for accessing the Azure OpenAI Premium services.
- **Value**: `"<APIKEY>"`

#### `AOAIPremiumChatGptDeployment`
- **Description**: The specific deployment of ChatGPT model used in the Azure OpenAI Premium services.
- **Value**: `"gpt-4"`

#### `AOAIStandardServiceEndpoint`
- **Description**: The endpoint URL for the Azure OpenAI Standard services.
- **Value**: `"https://<SERVICENAME>.openai.azure.com/"`

#### `AOAIStandardServiceKey`
- **Description**: The authentication key for accessing the Azure OpenAI Standard services.
- **Value**: `"f4471e39c00e4dfd86ae15bc3bcf68b1"`

#### `AOAIStandardChatGptDeployment`
- **Description**: The specific deployment of ChatGPT model used in the Azure OpenAI Standard services.
- **Value**: `"chatgpt16k"`

#### `AOAIEmbeddingsDeployment`
- **Description**: The specific deployment of the text embedding model used in the Azure OpenAI services.
- **Value**: `"text-embedding"`

### Cosmos DB

#### `CosmosDBConnectionString`
- **Description**: Connection string for accessing Azure Cosmos DB, including authentication information and endpoint details.
- **Value**: `"AccountEndpoint=https://<SERVICENAME>.documents.azure.com:443/;AccountKey=...;"`

### Ingestion Pipeline API

#### `IngestionPipelineAPI`
- **Description**: The endpoint URL for the ingestion pipeline API.
- **Value**: `"https://<SERVICENAME>.azurewebsites.net/"`

#### `IngestionPipelineAPIKey`
- **Description**: The API key for authenticating requests to the ingestion pipeline.
- **Value**: `"<APIKEY>"`

### Additional Settings

#### `EnableDataProtectionBlobKeyStorage`
- **Description**: Boolean flag to enable or disable blob key storage under the data protection mechanism.
- **Value**: `"false"`


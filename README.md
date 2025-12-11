# Smart Flow UI Application

This project is a demonstration of how to create a simple UI that builds on top of an existing SmartFlow landing zone.  This UI is really just a shell and will call the API defined in the SmartFlow API project to run the agentic workflows.

## Local Agents

### Agent Profiles

Agents are defined through **profiles**, which specify the agent’s configuration, behavior, and underlying capabilities. Profiles can represent either:

* **General chat agents** using LLMs (Large Language Models)
* **RAG (Retrieval-Augmented Generation) agents** powered by Azure AI Search

Each agent runs **inline within the UI application**, enabling a seamless user experience.

#### RAG Agents

RAG agents are configured via `RAGSettings`, which allow you to link an agent to a specific Azure AI Search index. This index serves as the **knowledge base** for that agent.

An agent consists of:

* A **system prompt**, which defines the agent’s domain, tone, and behavioral instructions
* An optional **knowledge base**, specified via a RAG index, to enhance responses with domain-specific context

**Sample Configuration**
```json
[
  {
    "Name": "General",
    "Id": "General",
    "Approach": "Chat",
    "SecurityModel": "None",
    "AllowFileUpload": true,
    "ChatSystemMessageFile": "ChatSimpleSystemPrompt",
    "SampleQuestions": [
      "Write a function in C# that will invoke a rest API",
      "Explain why popcorn pops to a kid who loves watching it in the microwave."
    ]
  },
  {
    "Name": "Auto Service Advisor",
    "Id": "AutoServiceAdvisor",
    "Approach": "RAG",
    "SecurityModel": "None",
    "SecurityModelGroupMembership": [ "LocalDevUser" ],
    "SampleQuestions": [
      "How do I change the oil?",
      "What are the different maintenance intervals?",
      "What is the air filter part number?"
    ],
    "RAGSettings": {
      "DocumentRetrievalSchema": "KwiecienV2",
      "DocumentRetrievalEmbeddingsDeployment": "text-embedding",
      "DocumentRetrievalIndexName": "manuals-auto-ci-20240528182950",
      "ChatSystemMessageFile": "RAGChatSystemPrompt",
      "StorageContianer": "manuals-auto-chunks",
      "CitationUseSourcePage": true,
      "DocumentRetrievalDocumentCount": 15,
      "UseSemanticRanker": true,
      "SemanticConfigurationName": "Default"
    }
  }
]
```

---

### Creating SmartFlow API Profiles

The prompts that are defined in the SmartFlow API should be represented as profiles in the [app/SmartFlowUI/backend/Services/Profile/profiles.json](./app/SmartFlowUI/backend/Services/Profile/profiles.json) file. You can add new prompts or modify existing ones to suit your needs.

Here's an example that is part of the initial API deploy:

![Profile Example](./docs/images/ProfileExample.png)

The `Auto Body Damage Analysis` is one of the samples provided with the base SmartFlow API. We've added it here with an approach of `ENDPOINTASSISTANTTASK`.  The three keys that are critical here are the `APIEndpointSetting`, `APIEndpointKeySetting`, and `AllowFileUpload`. The `AllowFileUpload` key is a boolean value that indicates whether or not you want to allow the user to upload files for this profile.

The values that you put in the `APIEndpointSetting` and `APIEndpointKeySetting` need to have corresponding values in the appsettings.json file or the User Secrets, like this:

![AppSettings Example](./docs/images/AppSettingsExample.png)

When the app is loaded and runs, it will process the `profiles.json` file and create a list of profiles that are available to the user.  The user can select one of those profiles and call those actions.

![UI Example](./docs/images/UI-Example.png)

---

### Using this Docker Image in other projects

If you have an existing SmartFlow application and want to pull this standard image into the UI container app, we have published the image as a public package in this repository. You can pull that package into your application and then use the Storage Account or Key Based Profile settings ([see below](#creating-smartflow-api-profiles)) to configure the profiles how you want to use it.

You can access this profile via the link on this page, or using a command like this:

```bash
docker pull ghcr.io/msft-mfg-ai/smart-flow-ui/smartflowui:latest
```

---


## Alternate Profile Sources

Once the UI is deployed, you can also change the profiles defined by specifying one of two alternate locations for the profile data. In that way, you can update the profiles supplied without having to redeploy the application each time.

The profiles are loaded in priority order, starting with the Storage Account, then looking for a ProfileConfiguration environment key, then defaulting finally to the `profiles.json` file.

### 1. Storage Account Profile

If you create an environment key named `ProfileConfigurationBlobStorageContainer`, the application will look in that container for a file named `profiles.json`.  This file will be used to override the profiles defined in the `profiles.json` file in the application.

### 2. Environment Key Profile

If you create an environment key named `ProfileConfiguration`, the application will use that data to override the profiles defined in the `profiles.json` file in the application. It is expected that the key value would contain a JSON string that is contains exactly what would be in the `profiles.json` file. However, that data must be Base 64 encoded as it will be Base64 decoded when it is read.

Create your `profiles.json` file locally, then use a tool like [https://base64.guru/converter/encode/text](https://base64.guru/converter/encode/text) (or some other tool you have access to) to encode the file.  Create an environment key named `ProfileConfiguration` and paste the Base 64 encoded string into the value.

![B64 Example](./docs/images/Base64Encoding.png)

### 3. Profiles.Json File Profile

Lastly, if neither of the previous two options are specified, the application will use the values in `profiles.json` file to configure the application.

---

Once you have your profiles configured, you should be able to test any of the APIs you have deployed in the SmartFlow API application.

---

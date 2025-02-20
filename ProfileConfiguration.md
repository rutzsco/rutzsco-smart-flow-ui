# Profile Configuration

## Smart Flow Plug In

### Profile Example

```json
  {
    "Name": "Demo Task Flow",
    "Id": "DemoTaskFlow",
    "Approach": "ENDPOINTASSISTANTTASK",
    "SecurityModel": "None",
    "SecurityModelGroupMembership": [ "LocalDevUser" ],
    "SampleQuestions": [
      "Run Task"
    ],
    "AssistantEndpointSettings": {
      "APIEndpointSetting": "DemoTaskFlowAPIEndpoint",
      "APIEndpointKeySetting": "DemoTaskFlowAPIEndpointKey",
      "AllowFileUpload": true
    }
  },
```

### Settings

```json
  {
  "DemoTaskFlowAPIEndpoint": "https://<URL>/api/task/<WORKFLOW_NAME>",
  "DemoTaskFlowAPIEndpointKey": "<API_KEY>",
  }
```

# M365 Agent Adapter

This folder contains the integration layer for exposing SmartFlow UI agents to Microsoft 365 Copilot chat.

## Overview

The `M365AgentAdapter` class bridges the existing SmartFlow chat services with the Microsoft 365 Agent framework, allowing your agents to be used within Microsoft 365 Copilot experiences.

## Components

### M365AgentAdapter.cs
The main adapter class that:
- Inherits from `AgentApplication` (Microsoft.Agents.Builder)
- Handles conversation lifecycle events (member added, messages)
- Routes messages through the SmartFlow `IChatService`
- Manages error handling and logging
- Integrates with the ProfileService to use configured profiles

### M365AgentExtensions.cs
Extension methods for configuration:
- `AddM365AgentServices()` - Registers M365 agent services
- `MapM365AgentEndpoints()` - Maps agent endpoints to the application

## Setup

### 1. Service Registration (Already Done)

The M365 agent services are already registered in `Program.cs`:

```csharp
// Add M365 Agent services
builder.Services.AddM365AgentServices();
```

### 2. Endpoint Mapping (Already Done)

The M365 agent endpoints are already mapped in `Program.cs`:

```csharp
// Map M365 Agent endpoints
app.MapM365AgentEndpoints();
```

### 3. Add Configuration (Required)

Add the following to your `appsettings.json`:

```json
{
  "M365Agent": {
    "AppId": "your-bot-app-id",
    "AppPassword": "your-bot-app-password"
  }
}
```

For local development, you can add this to `appsettings.local.json` (which is gitignored).

## How It Works

1. **Incoming Messages**: M365 Copilot sends messages to `/api/m365/messages` endpoint
2. **Profile Selection**: The adapter uses the first available profile from ProfileService
3. **User Context**: Creates a `UserInformation` object from the M365 user context
4. **Chat Request**: Builds a `ChatRequest` with the user's message and conversation history
5. **Processing**: Routes the request through the injected `IChatService`
6. **Streaming Response**: Collects streaming chunks from the chat service
7. **Response**: Sends the complete response back to M365 Copilot

## Integration Details

### Profile Configuration

The adapter automatically:
- Retrieves available profiles from `ProfileService`
- Uses the first available profile (you can modify this in `ProcessMessageAsync`)
- Respects profile settings like approach, RAG settings, etc.

### User Mapping

M365 user information is mapped as follows:
- **UserId**: From `turnContext.Activity.From.Id`
- **UserName**: From `turnContext.Activity.From.Name`
- **SessionId**: From `turnContext.Activity.Conversation.Id`
- **Groups**: Defaults to `["M365Users"]`

### Conversation History

The adapter maintains conversation state through:
- **ConversationId**: Mapped from M365 conversation ID
- **ChatId**: Generated or parsed from conversation ID
- **ChatTurnId**: Generated for each turn

## Customization

### Selecting a Different Profile

Modify `ProcessMessageAsync` to select a specific profile:

```csharp
// Instead of using the first profile
var profile = profileInfo.Profiles.FirstOrDefault();

// Use a specific profile by ID
var profile = profileInfo.Profiles.FirstOrDefault(p => p.Id == "your-profile-id");

// Or use a specific profile by name
var profile = profileInfo.Profiles.FirstOrDefault(p => p.Name == "your-profile-name");
```

### Adding File Upload Support

The adapter can be extended to support file uploads from M365:

```csharp
private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
{
    var attachments = turnContext.Activity.Attachments;
    // Process attachments and convert to FileSummary objects
}
```

### Custom Welcome Message

Modify the `WelcomeMessageAsync` method to customize the greeting:

```csharp
var welcomeMessage = "Welcome to SmartFlow! Choose from these options: ...";
```

## Testing

### Local Testing with Bot Framework Emulator

1. Install [Bot Framework Emulator](https://github.com/Microsoft/BotFramework-Emulator/releases)
2. Run your application
3. In the emulator, connect to `http://localhost:<port>/api/m365/messages`
4. Start chatting to test the integration

### Testing with Teams

1. Create a Bot registration in Azure Portal
2. Configure the messaging endpoint to point to your deployed app: `https://your-app.azurewebsites.net/api/m365/messages`
3. Create a Teams app manifest with the bot registration
4. Sideload the app in Teams

## Deployment

1. Deploy your application to Azure App Service or another hosting platform
2. Configure your Bot registration messaging endpoint: `https://your-domain/api/m365/messages`
3. Add your bot App ID and password to configuration
4. Ensure the endpoint is accessible (not behind auth for bot framework traffic)

## Required NuGet Packages

The following packages are already included in the project:
- `Microsoft.Agents.Authentication.Msal`
- `Microsoft.Agents.Hosting.AspNetCore`

## Troubleshooting

### "No profiles configured" error

- Ensure your `appsettings.json` has at least one profile configured
- Check that the ProfileService is properly loading profiles

### Messages not being received

- Verify the messaging endpoint is correctly configured in Azure Bot registration
- Check that the endpoint is publicly accessible
- Ensure AppId and AppPassword match your bot registration

### Authentication errors

- Verify your AppId and AppPassword are correct
- Check that the bot registration is active in Azure Portal

## References

- [Microsoft Agents SDK Documentation](https://github.com/microsoft/Agents)
- [Microsoft 365 Copilot Extensibility](https://learn.microsoft.com/microsoft-365-copilot)
- [Bot Framework](https://dev.botframework.com/)


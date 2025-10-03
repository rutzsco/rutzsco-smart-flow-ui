# Azure Bot Registration Setup Guide

This guide walks you through creating an Azure Bot registration for the M365 Agent integration.

## Prerequisites

- Azure subscription
- Azure CLI installed (optional, can use Azure Portal)
- Your SmartFlow UI application deployed (or ngrok for local testing)

## Steps

### 1. Create a Bot Registration in Azure Portal

1. Go to [Azure Portal](https://portal.azure.com)
2. Click "Create a resource"
3. Search for "Azure Bot"
4. Click "Create"

### 2. Configure Basic Settings

- **Bot handle**: Choose a unique name (e.g., `smartflow-bot`)
- **Subscription**: Select your subscription
- **Resource group**: Create new or use existing
- **Pricing tier**: Choose appropriate tier (F0 for free)
- **Microsoft App ID**: 
  - Select "Create new Microsoft App ID"
  - Choose "Multi Tenant" for broader access

### 3. Create App Registration

After creating the bot, you need to get the App ID and create a client secret:

1. Go to the bot resource in Azure Portal
2. Click on "Configuration" in the left menu
3. Note the "Microsoft App ID" - this is your `AppId`
4. Click "Manage" next to Microsoft App ID
5. This opens the App Registration in Azure AD

### 4. Create Client Secret

1. In the App Registration, click "Certificates & secrets"
2. Click "New client secret"
3. Add a description (e.g., "SmartFlow M365 Agent")
4. Choose expiration period
5. Click "Add"
6. **IMPORTANT**: Copy the secret value immediately - this is your `AppPassword`
7. You won't be able to see it again!

### 5. Configure Messaging Endpoint

1. Go back to your Bot resource
2. Click "Configuration"
3. Set the "Messaging endpoint":
   - **Production**: `https://your-app-domain.com/api/m365/messages`
   - **Local Testing**: `https://your-ngrok-url.ngrok.io/api/m365/messages`
4. Click "Apply"

### 6. Update Application Configuration

Add these values to your `appsettings.json` or `appsettings.local.json`:

```json
{
  "M365Agent": {
    "AppId": "YOUR-APP-ID-FROM-STEP-3",
    "AppPassword": "YOUR-CLIENT-SECRET-FROM-STEP-4"
  }
}
```

### 7. Test with Bot Framework Emulator

1. Download [Bot Framework Emulator](https://github.com/Microsoft/BotFramework-Emulator/releases)
2. Run your SmartFlow UI application locally
3. Open Bot Framework Emulator
4. Click "Open Bot"
5. Enter bot URL: `http://localhost:5000/api/m365/messages` (adjust port)
6. Enter Microsoft App ID and password
7. Click "Connect"
8. Send a test message

## Configure for Microsoft Teams

### 1. Enable Teams Channel

1. In your Bot resource, click "Channels"
2. Click on "Microsoft Teams" icon
3. Accept the terms
4. Click "Save"

### 2. Create Teams App Manifest

Create a `manifest.json`:

```json
{
  "$schema": "https://developer.microsoft.com/json-schemas/teams/v1.16/MicrosoftTeams.schema.json",
  "manifestVersion": "1.16",
  "version": "1.0.0",
  "id": "YOUR-APP-ID",
  "packageName": "com.smartflow.m365agent",
  "developer": {
    "name": "Your Company",
    "websiteUrl": "https://your-website.com",
    "privacyUrl": "https://your-website.com/privacy",
    "termsOfUseUrl": "https://your-website.com/terms"
  },
  "name": {
    "short": "SmartFlow Assistant",
    "full": "SmartFlow AI Assistant"
  },
  "description": {
    "short": "AI-powered assistant for your workflows",
    "full": "SmartFlow AI Assistant helps you with intelligent conversations and task automation"
  },
  "icons": {
    "outline": "outline.png",
    "color": "color.png"
  },
  "accentColor": "#FFFFFF",
  "bots": [
    {
      "botId": "YOUR-APP-ID",
      "scopes": ["personal", "team", "groupchat"],
      "supportsFiles": false,
      "isNotificationOnly": false
    }
  ],
  "permissions": [
    "identity",
    "messageTeamMembers"
  ],
  "validDomains": []
}
```

### 3. Create App Package

1. Create two icon files:
   - `color.png` - 192x192 pixels
   - `outline.png` - 32x32 pixels
2. Put `manifest.json` and both icon files in a folder
3. Zip the folder (not the folder itself, just the files inside)
4. Name it `smartflow-teams-app.zip`

### 4. Sideload to Teams

1. Open Microsoft Teams
2. Click on "Apps" in the left sidebar
3. Click "Manage your apps"
4. Click "Upload an app"
5. Select "Upload a custom app"
6. Choose your `smartflow-teams-app.zip`
7. Click "Add"

## Configure for M365 Copilot

To integrate with M365 Copilot as a plugin:

1. Follow Microsoft's documentation for [Building message extensions for Copilot](https://learn.microsoft.com/microsoft-365-copilot/extensibility/overview-message-extension-bot)
2. Update your Teams manifest to include messaging extensions
3. Configure the appropriate scopes and commands

## Troubleshooting

### "Unauthorized" errors
- Verify AppId and AppPassword are correct
- Check that the bot registration is active
- Ensure the messaging endpoint is publicly accessible

### Messages not reaching the bot
- Verify the messaging endpoint URL is correct
- Check application logs for errors
- Test with Bot Framework Emulator first

### "No profiles configured" error
- Ensure your ProfileService is properly configured
- Check that at least one profile exists in your profiles.json

### Connection timeout
- Verify your application is running
- Check firewall settings
- For local testing, ensure ngrok is running and configured correctly

## Local Testing with ngrok

For local development:

1. Install [ngrok](https://ngrok.com/)
2. Run ngrok: `ngrok http 5000` (adjust port)
3. Copy the HTTPS URL (e.g., `https://abc123.ngrok.io`)
4. Update bot messaging endpoint to: `https://abc123.ngrok.io/api/m365/messages`
5. Run your application locally
6. Test with Bot Framework Emulator or Teams

## Security Considerations

- **Never commit** AppPassword to source control
- Use Azure Key Vault for production secrets
- Configure proper authentication on your messaging endpoint
- Implement rate limiting if exposed publicly
- Monitor bot usage and implement quotas

## Next Steps

- [Configure custom commands for Teams](https://learn.microsoft.com/microsoftteams/platform/bots/how-to/conversations/command-menu)
- [Add adaptive cards for rich responses](https://learn.microsoft.com/microsoftteams/platform/task-modules-and-cards/cards/cards-reference)
- [Implement proactive messaging](https://learn.microsoft.com/microsoftteams/platform/bots/how-to/conversations/send-proactive-messages)
- [Add authentication](https://learn.microsoft.com/microsoftteams/platform/bots/how-to/authentication/auth-flow-bot)

## Resources

- [Azure Bot Service Documentation](https://learn.microsoft.com/azure/bot-service/)
- [Teams Bot Documentation](https://learn.microsoft.com/microsoftteams/platform/bots/what-are-bots)
- [M365 Copilot Extensibility](https://learn.microsoft.com/microsoft-365-copilot/extensibility/)
- [Bot Framework SDK](https://github.com/microsoft/botframework-sdk)

# Set up GitHub Secrets

The GitHub workflows in this project require several secrets set at the repository level or at the environment level.

---

## Workflow Definitions

- **[1_deploy_infra.yml](./workflows/1_deploy_infra.yml):** Deploys the main.bicep template with all new resources and does nothing else
- **[2_deploy_existing_infra.yml](./workflows/2_deploy_existing_infra.yml):** Deploys the main.bicep template with different params that use some existing resources and does nothing else
- **[3_build_deploy.yml](./workflows/3_build_deploy.yml):** Builds the app and deploys it to Azure - this should happen automatically after each check-in to main
- **[4_scan_build_pr.yml](./workflows/4_scan_build_pr.yml):** Runs each time a Pull Request is submitted and includes the results in the PR
- **[5_scheduled_scan.yml](./workflows/5_scheduled_scan.yml):** Runs a scheduled periodic scan of the app for security vulnerabilities

---

## Azure Credentials

You will need to set up the Azure Credentials secrets in the GitHub Secrets at the Repository level before you do anything else.

> If you require different credentials for your DEV/QA/PROD environments, you can set up the secrets at the Environment level instead of the Repository level.

See [https://docs.microsoft.com/en-us/azure/azure-resource-manager/templates/deploy-github-actions](https://docs.microsoft.com/en-us/azure/azure-resource-manager/templates/deploy-github-actions) for more info on how to create the service principal and set up these credentials.

> Note: this service principal must have contributor rights to your subscription (or resource group) to deploy the resources.
> ADMIN_IP_ADDRESS and ADMIN_PRINCIPAL_ID are optional - set only if you want to get access to the KV and ACR.
You can customize and run the following commands, or you can set these secrets up manually by going to the Settings -> Secrets -> Actions -> Secrets.

These secrets should be set at the environment level, and may or may not be the same.

```bash
gh secret set --env <ENV-NAME> AZURE_SUBSCRIPTION_ID -b <yourAzureSubscriptionId>
gh secret set --env <ENV-NAME> AZURE_TENANT_ID -b <GUID-Entra-tenant-where-SP-lives>
gh secret set --env <ENV-NAME> CICD_CLIENT_ID -b <GUID-application/client-Id>
```

These two secrets are optional if you want to grant an administrator access to the Key Vault and ACR.  

```bash
gh secret set ADMIN_IP_ADDRESS 192.168.1.1
gh secret set ADMIN_PRINCIPAL_ID <yourGuid>
```

---

## Bicep Configuration Values

These values are used by the Bicep templates to configure the resource names that are deployed. Make sure the App_Name variable is unique to your deploy. It will be used as the basis for the application name and for all the other Azure resources, some of which must be globally unique.

> If you desire different names or values for your DEV/QA/PROD environments, you can set up the variables at the Environment level instead of the Repository level.

You can customize and run the following commands (or just set it up manually by going to the Settings -> Secrets -> Actions -> Variables).  Replace '<<YOURAPPNAME>>' with a value that is unique to your deployment, which can contain dashes or underscores (i.e. 'xxx-doc-review'). APP_NAME_NO_DASHES should be the same but without dashes.

These should be set at the repository level and may be the same for all environments, although you could set them up at the environment level if you want them to unique.

```bash
gh variable set APP_NAME -b <<YOUR-APP-NAME>>
gh variable set APP_NAME_NO_DASHES -b <<YOURAPPNAME>>
gh variable set RESOURCEGROUP_PREFIX -b rg_ai_docs
gh variable set RESOURCEGROUP_LOCATION -b eastus2
gh variable set OPENAI_DEPLOY_LOCATION -b eastus2
```

The Bicep templates will use these values to create the Azure resources. The Resource Group Name will be `<RESOURCEGROUP_PREFIX>-<ENVIRONMENT>` and will be created in the `<RESOURCEGROUP_LOCATION>` Azure region. The `APP_NAME` will be used as the basis for all of the resource names, with the environment name (i.e. dev/qa/prod) appended to each resource name.

The `<OPENAI_DEPLOY_LOCATION>` can be specified if you want to deploy the OpenAI resources in a different region than the rest of the resources due to region constraints.

---

## References

- [Deploying ARM Templates with GitHub Actions](https://docs.microsoft.com/en-us/azure/azure-resource-manager/templates/deploy-github-actions)
- [GitHub Secrets CLI](https://cli.github.com/manual/gh_secret_set)

---

[Home Page](../README.md)

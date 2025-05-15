# Role-Based Access Control (RBAC)

## Required Azure Roles

| Service                         | Built-in Role Name             | Scope                                 | Purpose                                    |
| ------------------------------- | ------------------------------ | ------------------------------------- | ------------------------------------------ |
| Azure Blob Storage              | Storage Blob Data Contributor  | Storage account or specific container | Allows read/write access to blobs          |
| Azure Cognitive Services OpenAI | Cognitive Services OpenAI User | OpenAI resource                       | Grants access to the OpenAI data-plane API |
| Azure AI Search                 | Search Service Contributor     | Search service                        | Enables query and index operations         |

---

## Assigning Roles via Azure Portal

1. Navigate to the resource (storage account, OpenAI resource, or Search service) in the Azure Portal.
2. Select **Access control (IAM)** from the left-hand menu.
3. Click **+ Add › Role assignment**.
4. In the **Role** dropdown, select the appropriate built-in role (e.g., *Storage Blob Data Contributor*).
5. Under **Assign access to**, choose **User, group, or service principal**.
6. Search for and select your application’s managed identity or service principal.
7. Click **Save**.

Repeat these steps for each service/resource.

---

## Assigning Roles via Azure CLI

Use the following commands to assign roles from your local shell. Replace:

* `SUBSCRIPTION_ID` with your Azure subscription ID
* `RESOURCE_ID` with the full resource ID of your storage account, OpenAI resource, or search service
* `APP_OBJECT_ID` with the object ID of your app’s identity

```bash
# Login
az login
az account set --subscription $SUBSCRIPTION_ID

# 1. Blob Storage: Storage Blob Data Contributor
az role assignment create \
  --assignee-object-id $APP_OBJECT_ID \
  --role "Storage Blob Data Contributor" \
  --scope "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/<RG>/providers/Microsoft.Storage/storageAccounts/<STORAGE_ACCOUNT>"

# 2. Cognitive Services OpenAI User
az role assignment create \
  --assignee-object-id $APP_OBJECT_ID \
  --role "Cognitive Services OpenAI User" \
  --scope "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/<RG>/providers/Microsoft.CognitiveServices/accounts/<OPENAI_RESOURCE>"

# 3. AI Search: Search Service Contributor
az role assignment create \
  --assignee-object-id $APP_OBJECT_ID \
  --role "Search Service Contributor" \
  --scope "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/<RG>/providers/Microsoft.Search/searchServices/<SEARCH_SERVICE>"
```

---

## Verifying Role Assignments

After assigning roles, verify them:

```bash
# List assignments for your app
az role assignment list \
  --assignee-object-id $APP_OBJECT_ID \
  --all
```

Look for entries corresponding to the roles listed above. Ensure the **scope** matches your intended resource.

---

## Troubleshooting

* **Insufficient Privileges**: You must have the `User Access Administrator` or `Owner` role to create role assignments.
* **Wrong Object ID**: Confirm you’re using the application’s **Object ID** (not the Application ID/Client ID).
* **Propagation Delay**: RBAC changes can take a few minutes to propagate. Wait and retry if you encounter permission errors.
* **Scope Mismatch**: Ensure the `--scope` parameter matches the exact resource path you intend to grant access to.

---

## Additional Resources

* [Azure RBAC documentation](https://learn.microsoft.com/azure/role-based-access-control/)
* [Assign Azure roles using the Azure CLI](https://learn.microsoft.com/cli/azure/role/assignment)
* [Storage Blob Data Contributor role](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles#storage-blob-data-contributor)
* [Cognitive Services OpenAI RBAC](https://learn.microsoft.com/azure/cognitive-services/authentication?tabs=command-line#role-based-access-control)
* [Search Service Contributor role](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles#search-service-contributor)

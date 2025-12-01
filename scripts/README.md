# Scripts

Helper scripts for development and deployment.

## setup-local-config.sh

Extracts Terraform outputs and configures the local .NET application with Azure AD and OBO settings.

### Prerequisites

- Terraform state must exist (run `azd up` first)
- `jq` must be installed: `brew install jq`

### Usage

**Option 1: Update appsettings.Development.json (Recommended)**

```bash
./scripts/setup-local-config.sh
```

This will:
- Extract configuration from Terraform outputs
- Update `src/MCPWrapper/MCPWrapper.Api/appsettings.Development.json`
- Create a backup of the original file

Then run the app:
```bash
cd src/MCPWrapper/MCPWrapper.Api
dotnet run
```

**Option 2: Export Environment Variables**

```bash
./scripts/setup-local-config.sh --env-vars
```

This will:
- Create `.env.local` file with environment variables
- Preserve existing appsettings files

Then source and run:
```bash
source .env.local
cd src/MCPWrapper/MCPWrapper.Api
dotnet run
```

### What Gets Configured

The script extracts and configures:

- **Azure AD Settings**
  - `AzureAd__TenantId` - Your Entra ID tenant
  - `AzureAd__ClientId` - MCP Proxy app registration client ID
  - `AzureAd__ClientSecret` - Client secret for OBO token exchange
  - `AzureAd__Audience` - Expected audience in JWT tokens

- **SuccessFactors Settings**
  - `SuccessFactors__DownstreamScope` - Scope for OBO exchange
  - `SuccessFactors__SuccessFactorsBaseUrl` - API endpoint

### Troubleshooting

**Error: Terraform state not found**
- Run `azd up` first to provision infrastructure

**Error: jq is not installed**
- Install with: `brew install jq`

**Error: Missing required outputs**
- Ensure `enable_entra_setup = true` in Terraform variables
- Check that `azd up` completed successfully

**Client secret shows `<not-configured>`**
- This is expected if using managed identity only
- For local dev with OBO, set `create_app_secret = true` in Terraform variables

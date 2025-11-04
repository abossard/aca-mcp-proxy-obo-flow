# MCP Proxy on Azure Container Apps - AI Agent Instructions

## Architecture Overview

This is an **MCP (Model Context Protocol) wrapper/proxy** deployed on Azure Container Apps with Entra ID authentication using federated identity (no secrets). The project uses Azure Developer CLI (azd) with Terraform for infrastructure.

**Key Components:**
- `.NET 9 Minimal API` with AOT compilation (`PublishAot=true`)
- `Azure Container Apps` for hosting (single app named "api")
- `Entra ID app registration` with federated credentials linked to managed identity
- `AzAPI provider` for Container App authentication (azurerm doesn't support auth blocks yet - GitHub issue #22213)

**Critical Pattern:** Authentication is configured via `azapi_resource` at `Microsoft.App/containerApps/authConfigs@2023-05-01` because the native azurerm provider lacks EasyAuth support.

## Project Structure

```
infra/                    # Terraform IaC
├── identity.tf          # Managed identity + Entra ID app registration with federated credentials
├── containerapps.tf     # Container App + azapi_resource for authentication
├── main.tf              # Resource group, ACR, Container App Environment, Log Analytics, App Insights
├── rbac.tf              # AcrPull role assignment for managed identity
├── provider.tf          # azurerm ~>4.16 + azapi 2.2.0 + azuread ~>2.47
├── main.tfvars.json     # Variable interpolation from azd environment
src/MCPWrapper/
└── MCPWrapper.Api/      # .NET 9 minimal API (currently sample Todo app)
    ├── Program.cs       # Slim builder, JSON serialization context
    └── Dockerfile       # Multi-stage with clang/zlib for AOT
```

## Critical Workflows

### Deploy Everything
```bash
azd up  # Provisions infra + builds/deploys container images
```
Runs from project root. Uses `azure.yaml` to orchestrate Terraform + Docker build with ACR remote build.

### Local Development
```bash
cd src/MCPWrapper/MCPWrapper.Api
dotnet run
```
Uses `UserSecretsId` for local config. Container listens on port 8080.

### Infrastructure Only
```bash
cd infra
terraform plan -var-file=main.tfvars.json
terraform apply -var-file=main.tfvars.json
```
Variables are interpolated from azd environment (see `main.tfvars.json`).

## Authentication Architecture (CRITICAL)

**No secrets pattern:** Federated identity credential links the Container App's managed identity to the Entra ID app registration:

1. **Managed Identity** (`identity.tf`): `azurerm_user_assigned_identity.managed_identity`
2. **App Registration** (`identity.tf`): `azuread_application.mcp_proxy` with redirect URI: `https://api.{cae_domain}/.auth/login/aad/callback`
3. **Federation** (`identity.tf`): `azuread_application_federated_identity_credential` links managed identity principal_id as subject
4. **EasyAuth Config** (`containerapps.tf`): `azapi_resource.api_auth_config` with `unauthenticatedClientAction = "AllowAnonymous"`

**Why azapi?** The `azurerm_container_app` resource has NO native `auth_settings` or `auth_settings_v2` block. This is a known limitation (issue #22213) requiring AzAPI provider for `authConfigs` ARM API.

## Project-Specific Conventions

### Terraform Naming
- Resources prefixed with `${var.environment_name}` (from azd)
- ACR name: `acr${environment_name}domal` (hardcoded suffix for uniqueness)
- All resources tagged with `azd-env-name`

### .NET Conventions
- **Native AOT enabled** - avoid reflection-based JSON serialization
- Use `JsonSerializerContext` for all DTOs (see `AppJsonSerializerContext`)
- Slim builder pattern for minimal startup
- Container runs as non-root user (`USER $APP_UID`)

### Environment Variables
Container apps receive (see `containerapps.tf` locals):
```
AZURE_TENANT_ID, AZURE_CLIENT_ID (managed identity)
APPLICATIONINSIGHTS_CONNECTION_STRING
API_ENDPOINT (container app URL)
ASPNETCORE_ENVIRONMENT=Development
```

## Integration Points

- **ACR Authentication**: Managed identity has `AcrPull` role (rbac.tf)
- **App Insights**: Backed by Log Analytics Workspace (30-day retention)
- **Container App Environment**: Shared by all apps in the environment
- **Entra ID Token Validation**: Audience must match `api://{tenant_id}/{environment_name}-mcp-proxy`

## Gotchas & Constraints

1. **Remote build required** - Dockerfile expects build context at `src/MCPWrapper/` level
2. **AOT compilation** - Add clang/zlib to Dockerfile, avoid dynamic code generation
3. **Auth config name must be "current"** - ARM API requirement for Container App authConfigs
4. **Terraform state** - No remote backend configured; runs locally or in CI

## When Adding New Endpoints

1. Define in `Program.cs` using `MapGroup` pattern
2. Add `[JsonSerializable(typeof(YourDto))]` to `AppJsonSerializerContext`
3. Consider auth requirements: current config allows anonymous; change to `Return401` or `RedirectToLoginPage` if needed
4. Update Application Insights queries if adding custom telemetry

## Key Files for Understanding Flow

- `azure.yaml` → defines azd service mapping
- `infra/main.tfvars.json` → shows azd→Terraform variable injection
- `infra/identity.tf` → federated identity pattern (study this for auth)
- `infra/containerapps.tf` → azapi_resource for EasyAuth configuration

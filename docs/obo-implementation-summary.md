# OBO Token Implementation Summary

## Overview

This document summarizes the On-Behalf-Of (OBO) token exchange implementation for the MCP Proxy on Azure Container Apps. The implementation enables the MCP Proxy to receive JWT tokens from upstream clients and exchange them for tokens to call downstream APIs, maintaining the user's identity context throughout the call chain.

## Architecture

### Flow Diagram

```
User/Client App → JWT Token → MCP Proxy → OBO Exchange → Downstream API
                                  ↓
                         Managed Identity
                         (Federated Credentials)
```

### Key Components

1. **Entra ID App Registration**
   - Configured with `identifier_uris` using standard format: `api://{client_id}`
   - Exposes app roles for daemon/service-to-service calls
   - Supports federated identity credentials for secret-less authentication

2. **JWT Bearer Authentication**
   - Validates incoming JWT tokens from Entra ID
   - Configured with proper authority and audience validation
   - Event handlers for authentication success/failure logging

3. **OBO Token Service**
   - Uses MSAL (Microsoft Authentication Library) for token exchange
   - Leverages managed identity with federated credentials
   - Acquires tokens for downstream APIs on behalf of the user

4. **OpenTelemetry Integration**
   - Comprehensive tracing and metrics
   - Application Insights integration for production
   - Console exporters for local development
   - Structured logging throughout the application

## Implementation Details

### Terraform Configuration

#### New Variables

- `downstream_api_scope`: Configures the scope for downstream API calls (e.g., `https://graph.microsoft.com/.default`)

#### Updated Resources

- **identity.tf**: Updated App Registration to use standard `api://{client_id}` format
- **containerapps.tf**: Added `DOWNSTREAM_API_SCOPE` environment variable

### .NET Application

#### New Components

1. **OboConfig** (`MCPWrapper.Lib/Config/OboConfig.cs`)
   - Configuration class for OBO settings
   - Properties: TenantId, ClientId, DownstreamApiScope

2. **TokenService** (`MCPWrapper.Lib/Services/TokenService.cs`)
   - Interface: `ITokenService`
   - Implementation: Uses MSAL with managed identity assertion
   - Method: `GetOboTokenAsync(string userToken)` → returns downstream API token

3. **Enhanced SuccessFactorsTimeOffService**
   - Updated all methods to accept optional `oboToken` parameter
   - Falls back to API key authentication if OBO token is not available
   - Logs authentication method used for each request

4. **Updated SuccessFactorsTimeOffMcp**
   - Extracts JWT token from HTTP Authorization header
   - Calls TokenService to exchange for OBO token
   - Passes OBO token to service methods

#### Dependencies Added

**MCPWrapper.Api**:
- `Azure.Identity` (1.13.1)
- `Microsoft.Identity.Client` (4.66.2)
- `Microsoft.ApplicationInsights.AspNetCore` (2.22.0)
- OpenTelemetry packages (1.10.x)

**MCPWrapper.Lib**:
- `Azure.Identity` (1.13.1)
- `Microsoft.Identity.Client` (4.66.2)
- Framework reference to `Microsoft.AspNetCore.App` for HTTP abstractions

### Configuration

#### appsettings.json

```json
{
  "Obo": {
    "TenantId": "",
    "ClientId": "",
    "DownstreamApiScope": ""
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "MCPWrapper": "Information"
    }
  }
}
```

#### Environment Variables (Container Apps)

- `AZURE_TENANT_ID`: Tenant ID for authentication
- `ENTRA_CLIENT_ID`: Client ID of the MCP Proxy app registration
- `DOWNSTREAM_API_SCOPE`: Scope for downstream API (e.g., `https://graph.microsoft.com/.default`)
- `APPLICATIONINSIGHTS_CONNECTION_STRING`: App Insights for telemetry

## Security Features

### Zero-Secrets Pattern

- **Managed Identity with Federated Credentials**: No client secrets stored anywhere
- **JWT Token Validation**: Strict validation of incoming tokens
- **Token Exchange Security**: MSAL handles token security best practices

### Authentication Flow

1. Client obtains JWT token from Entra ID
2. Client sends request with `Authorization: Bearer {token}` header
3. MCP Proxy validates the JWT token
4. MCP Proxy extracts the token and exchanges it for a downstream API token using OBO
5. MCP Proxy calls the downstream API with the new token
6. Response flows back to the client

### Fallback Strategy

If OBO token exchange is not configured or fails:
- Application logs a warning
- Falls back to API key authentication (if configured)
- Allows the application to work in both OBO and non-OBO modes

## OpenTelemetry Implementation

### Instrumentation

- **ASP.NET Core**: Automatic request/response tracing
- **HTTP Client**: Outbound HTTP call tracing
- **Custom Logging**: Structured logging with proper log levels

### Exporters

- **Production**: Azure Monitor (Application Insights)
- **Development**: Console exporter for local debugging
- **OTLP**: OpenTelemetry Protocol for standard telemetry export

### Best Practices Followed

- Service name and version configured from appsettings
- Resource attributes properly set
- Log scopes and formatted messages included
- Tracing and metrics integrated

## Testing

### Build Verification

✅ Solution builds successfully without errors
✅ All NuGet packages restored correctly
✅ No dependency conflicts

### Security Scan

✅ CodeQL analysis completed with 0 alerts
✅ No vulnerabilities found in dependencies
✅ All packages checked against GitHub Advisory Database

### Integration Tests

- Updated to include required logger dependencies
- Configured for local development and CI/CD
- Skipped in CI due to requiring actual API credentials

## Deployment Scenarios

### Scenario A: Full Provisioning (Development)

1. Set `enable_entra_setup = true` in Terraform
2. Configure `downstream_api_permissions` for required API roles
3. Set `downstream_api_scope` to target API scope
4. Deploy with `azd up`
5. Terraform creates all Entra ID resources automatically

### Scenario B: Pre-Provisioned Identity (Production)

1. Set `enable_entra_setup = false` in Terraform
2. Configure `existing_entra_config` with pre-created app details
3. Set `downstream_api_scope` to target API scope
4. Deploy with `azd up`
5. Terraform uses existing Entra ID resources

## Monitoring and Troubleshooting

### Key Logs to Monitor

1. **JWT Authentication Events**
   - `OnAuthenticationFailed`: Token validation failures
   - `OnTokenValidated`: Successful token validation

2. **OBO Token Exchange**
   - "Attempting OBO token exchange for scope: {Scope}"
   - "OBO token acquired successfully"
   - "MSAL service error during OBO token exchange: {ErrorCode}"

3. **API Calls**
   - "Using OBO token for SuccessFactors API call"
   - "Using API key for SuccessFactors API call"
   - "No authentication method available"

### Common Issues and Solutions

#### Issue: "Downstream API scope is not configured"
**Solution**: Set the `DOWNSTREAM_API_SCOPE` environment variable in Container Apps

#### Issue: "MSAL service error: invalid_grant"
**Solution**: 
- Verify the incoming user token is valid
- Check that the app registration has the required API permissions
- Ensure admin consent has been granted for the permissions

#### Issue: "JWT authentication failed"
**Solution**:
- Verify the token audience matches `api://{client_id}`
- Check that the token issuer matches the tenant
- Ensure the token hasn't expired

## Future Enhancements

1. **Token Caching**: Implement MSAL token cache for better performance
2. **Retry Logic**: Add retry policies for transient OBO failures
3. **Multiple Downstream APIs**: Support for multiple downstream API scopes
4. **Token Introspection**: Add endpoints to debug token claims
5. **Rate Limiting**: Implement rate limiting for OBO token requests

## References

- [Microsoft Entra ID OBO Flow](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-on-behalf-of-flow)
- [MSAL .NET Documentation](https://learn.microsoft.com/en-us/entra/msal/dotnet/)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/net/)
- [Azure Container Apps Authentication](https://learn.microsoft.com/en-us/azure/container-apps/authentication)
- [Federated Identity Credentials](https://learn.microsoft.com/en-us/entra/workload-id/workload-identity-federation)

## Version History

- **v1.0** (2025-12-07): Initial OBO implementation with JWT auth and OTEL logging

# 🧪 Manual Testing Guide

This guide outlines how to manually test the MCP Proxy with OBO authentication.

## Prerequisites

Before testing, ensure you have:

1. ✅ Azure subscription with appropriate permissions
2. ✅ [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) installed and logged in
3. ✅ [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
4. ✅ An Entra ID App Registration for the MCP Proxy
5. ✅ An Entra ID App Registration for the downstream API (SuccessFactors)
6. ✅ Admin consent granted for the OBO permissions

## Test Scenarios

### 1. Local Development Testing (Without Auth)

For quick local testing without authentication:

```bash
cd src/MCPWrapper/MCPWrapper.Api
dotnet run
```

The API will start on `http://localhost:5000`. Note that without valid Azure AD configuration, authentication will fail.

### 2. Authentication Flow Testing

#### Step 2.1: Obtain a User Token

Use the Azure CLI to get a token for your app:

```bash
# Get an access token for the MCP Proxy API
az account get-access-token \
  --resource api://<mcp-proxy-client-id> \
  --query accessToken \
  -o tsv
```

Or use a test client with interactive login:

```bash
# Using MSAL.NET test client (example)
# This requires a registered client app with delegated permissions
```

#### Step 2.2: Test the MCP Endpoint

Use the token to call the MCP endpoint:

```bash
TOKEN="<your-access-token>"
ENDPOINT="https://<your-container-app>.azurecontainerapps.io"

# Test MCP tools listing
curl -X POST "$ENDPOINT/mcp" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
```

### 3. OBO Token Exchange Testing

#### Step 3.1: Verify Token Exchange

Check application logs to see if the OBO exchange is working:

```bash
# View container app logs
az containerapp logs show \
  --name <container-app-name> \
  --resource-group <resource-group> \
  --follow
```

Look for log messages like:
- `Acquiring SuccessFactors token via OBO for scope {Scope}`
- `Cached SuccessFactors token for {CacheDuration}`

#### Step 3.2: Test SuccessFactors Integration

Call a SuccessFactors tool through the MCP proxy:

```bash
TOKEN="<your-access-token>"
ENDPOINT="https://<your-container-app>.azurecontainerapps.io"

# List time off requests
curl -X POST "$ENDPOINT/mcp" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 2,
    "method": "tools/call",
    "params": {
      "name": "ListTimeOffRequests",
      "arguments": {
        "userId": "your-user-id"
      }
    }
  }'
```

### 4. Error Scenario Testing

#### 4.1: Missing Token Test

```bash
# Should return 401 Unauthorized
curl -X POST "$ENDPOINT/mcp" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
```

Expected: HTTP 401 Unauthorized

#### 4.2: Invalid Token Test

```bash
# Should return 401 Unauthorized
curl -X POST "$ENDPOINT/mcp" \
  -H "Authorization: Bearer invalid-token" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
```

Expected: HTTP 401 Unauthorized

#### 4.3: Wrong Audience Test

Get a token for a different audience and try to use it:

```bash
# Get token for wrong resource
WRONG_TOKEN=$(az account get-access-token \
  --resource https://graph.microsoft.com \
  --query accessToken -o tsv)

curl -X POST "$ENDPOINT/mcp" \
  -H "Authorization: Bearer $WRONG_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
```

Expected: HTTP 401 Unauthorized (audience mismatch)

### 5. Cache Behavior Testing

#### 5.1: Token Caching Verification

1. Make an initial request and note the log message about acquiring a new token
2. Make a second request immediately
3. The second request should show "Using cached SuccessFactors token"

#### 5.2: Cache Expiration Test

1. Make an initial request
2. Wait for the token to expire (check cache duration in logs)
3. Make another request
4. Should show a new token acquisition

### 6. Integration Test (Automated)

Run the automated integration tests:

```bash
cd src/MCPWrapper
dotnet test --filter "FullyQualifiedName~IntegrationTest"
```

⚠️ **Note:** Integration tests require valid SuccessFactors configuration in `appsettings.local.json`:

```json
{
  "SuccessFactors": {
    "SuccessFactorsBaseUrl": "https://your-sandbox.api.sap.com/successfactors/odata/v2",
    "DownstreamScope": "api://<your-sf-api-client-id>/.default"
  },
  "AzureAd": {
    "TenantId": "<your-tenant-id>",
    "ClientId": "<your-client-id>",
    "ClientSecret": "<your-client-secret>",
    "Audience": "api://<your-audience>"
  }
}
```

## Troubleshooting

### Common Issues

| Issue | Possible Cause | Solution |
|-------|---------------|----------|
| 401 Unauthorized | Invalid/expired token | Get a fresh token |
| 401 Unauthorized | Wrong audience | Check `AzureAd:Audience` configuration |
| OBO exchange fails | Missing consent | Grant admin consent for downstream API |
| OBO exchange fails | Wrong scope | Verify `SuccessFactors:DownstreamScope` |
| Token not cached | Cache key collision | Check logs for cache behavior |

### Checking Azure AD Configuration

```bash
# Verify app registration
az ad app show --id <client-id> --query "requiredResourceAccess"

# Check granted permissions
az ad sp show --id <client-id> --query "appRoles"
```

### Log Analysis

Key log messages to look for:

| Log Level | Message Pattern | Meaning |
|-----------|-----------------|---------|
| Information | `Acquiring SuccessFactors token via OBO` | New token being acquired |
| Information | `Using cached SuccessFactors token` | Token retrieved from cache |
| Warning | `No HTTP context available` | Auth handler called outside request context |
| Warning | `No incoming access token found` | Request missing Authorization header |
| Error | `Failed to acquire downstream token` | OBO exchange failed |

## Performance Considerations

- First request: ~200-500ms (token exchange)
- Cached requests: <50ms
- Token cache duration: Until 1 minute before expiration

## Security Checklist

Before production:

- [ ] Verify `ValidateIssuer` is enabled
- [ ] Verify `ValidateAudience` is enabled
- [ ] Confirm admin consent is properly granted
- [ ] Validate downstream API permissions are minimal
- [ ] Enable HTTPS only
- [ ] Review token cache duration settings

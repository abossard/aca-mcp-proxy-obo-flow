

resource "azurerm_user_assigned_identity" "managed_identity" {
  name                = local.managed_identity_name
  resource_group_name = azurerm_resource_group.rg.name
  location            = var.location
  tags                = local.tags

  lifecycle {
    ignore_changes = [tags]
  }
}

# Entra ID App Registration with Federated Identity (no secrets)
resource "azuread_application" "mcp_proxy" {
  display_name = local.app_registration_name

  # API exposure - defines this app as a resource server
  identifier_uris = ["api://${data.azurerm_client_config.current.tenant_id}/${local.app_registration_name}"]

  # Enable OAuth2 implicit flow if needed for SPA/testing
  web {
    # Container App authentication redirect URIs
    redirect_uris = [
      "https://api.${azurerm_container_app_environment.cae.default_domain}/.auth/login/aad/callback",
    ]

    implicit_grant {
      access_token_issuance_enabled = false
      id_token_issuance_enabled     = true
    }
  }

  # Request Microsoft Graph permissions (adjust as needed)
  required_resource_access {
    resource_app_id = "00000003-0000-0000-c000-000000000000" # Microsoft Graph

    resource_access {
      id   = "e1fe6dd8-ba31-4d61-89e7-88639da4683d" # User.Read
      type = "Scope"
    }
  }

  tags = ["MCP", "Proxy", "WorkloadIdentity"]
}

# Service Principal for the app registration
resource "azuread_service_principal" "mcp_proxy" {
  client_id    = azuread_application.mcp_proxy.client_id
  use_existing = true
  tags         = ["MCP", "Proxy", "WorkloadIdentity"]
}

# Federated Identity Credential - links managed identity to app registration
resource "azuread_application_federated_identity_credential" "mcp_proxy_managed_identity" {
  application_id = azuread_application.mcp_proxy.id
  display_name   = local.federated_identity_credential_name
  description    = "Federated credential for ${var.environment_name} managed identity to access app registration without secrets"

  audiences = ["api://AzureADTokenExchange"]
  issuer    = "https://login.microsoftonline.com/${data.azurerm_client_config.current.tenant_id}/v2.0"
  subject   = azurerm_user_assigned_identity.managed_identity.principal_id
}

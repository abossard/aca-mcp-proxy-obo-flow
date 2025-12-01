# ==============================================================================
# Managed Identity
# ==============================================================================

resource "azurerm_user_assigned_identity" "managed_identity" {
  name                = local.managed_identity_name
  resource_group_name = azurerm_resource_group.rg.name
  location            = var.location
  tags                = local.tags

  lifecycle {
    ignore_changes = [tags]
  }
}

# ==============================================================================
# Entra ID App Registration for MCP Proxy (API Server)
# Conditional creation based on enable_entra_setup
# 
# This app registration represents the MCP Proxy as an OAuth2 Resource Server.
# It exposes delegated permission scopes for OBO flow.
# ==============================================================================

resource "random_uuid" "user_impersonation_scope_id" {
  count = var.enable_entra_setup ? 1 : 0
}

resource "random_uuid" "access_as_user_scope_id" {
  count = var.enable_entra_setup ? 1 : 0
}

resource "azuread_application" "mcp_proxy" {
  count        = var.enable_entra_setup ? 1 : 0
  display_name = var.entra_app_name

  # API exposure - defines this app as a resource server
  identifier_uris = ["api://${data.azurerm_client_config.current.tenant_id}/${var.entra_app_name}"]

  # Sign-in audience for single tenant
  sign_in_audience = "AzureADMyOrg"

  # API block for OBO - expose delegated permission scopes
  api {
    # Access token version 2 is required for OBO flow
    requested_access_token_version = 2

    # Known client applications - clients that are pre-authorized for OBO
    # Add client app IDs here to skip consent prompt
    known_client_applications = var.known_client_applications

    # Expose delegated permission scope for user impersonation (OBO)
    oauth2_permission_scope {
      id                         = random_uuid.user_impersonation_scope_id[0].result
      value                      = "user_impersonation"
      type                       = "User"
      admin_consent_display_name = "Access MCP Proxy on behalf of the user"
      admin_consent_description  = "Allow the application to access MCP Proxy API on behalf of the signed-in user."
      user_consent_display_name  = "Access MCP Proxy on your behalf"
      user_consent_description   = "Allow the application to access MCP Proxy API on your behalf."
      enabled                    = true
    }

    # Additional scope for specific access pattern
    oauth2_permission_scope {
      id                         = random_uuid.access_as_user_scope_id[0].result
      value                      = "access_as_user"
      type                       = "User"
      admin_consent_display_name = "Access downstream APIs via MCP Proxy"
      admin_consent_description  = "Allow the application to call downstream APIs (like SuccessFactors) through MCP Proxy on behalf of the user."
      user_consent_display_name  = "Access downstream services on your behalf"
      user_consent_description   = "Allow the application to access SuccessFactors and other services through MCP Proxy on your behalf."
      enabled                    = true
    }
  }

  # Web app configuration with redirect URIs for EasyAuth
  web {
    redirect_uris = [
      "https://api.${azurerm_container_app_environment.cae.default_domain}/.auth/login/aad/callback",
    ]

    implicit_grant {
      access_token_issuance_enabled = false
      id_token_issuance_enabled     = true
    }
  }

  # Optional claims for access tokens
  optional_claims {
    access_token {
      name      = "email"
      essential = false
    }
    access_token {
      name      = "upn"
      essential = false
    }
  }

  # Expose App Role for Daemon/Service-to-Service calls (application permissions)
  app_role {
    allowed_member_types = ["Application"]
    description          = "Allows daemon apps to call the MCP Proxy without user context."
    display_name         = "Daemon.Call"
    enabled              = true
    id                   = "1b1b1b1b-1b1b-1b1b-1b1b-1b1b1b1b1b1b"
    value                = "Daemon.Call"
  }

  # Request delegated permissions for downstream API (for OBO exchange)
  # This configures what permissions the MCP Proxy can request on behalf of users
  dynamic "required_resource_access" {
    for_each = var.downstream_api_permissions
    content {
      resource_app_id = required_resource_access.value.resource_app_id

      dynamic "resource_access" {
        for_each = required_resource_access.value.delegated_permission_ids
        content {
          id   = resource_access.value
          type = "Scope" # Delegated Permission for OBO
        }
      }

      dynamic "resource_access" {
        for_each = required_resource_access.value.application_permission_ids
        content {
          id   = resource_access.value
          type = "Role" # Application Permission
        }
      }
    }
  }

  tags = ["MCP", "Proxy", "WorkloadIdentity", "OBO"]
}

# Service Principal for the MCP Proxy app registration
resource "azuread_service_principal" "mcp_proxy" {
  count        = var.enable_entra_setup ? 1 : 0
  client_id    = azuread_application.mcp_proxy[0].client_id
  use_existing = true
  tags         = ["MCP", "Proxy", "WorkloadIdentity", "OBO"]
}

# Federated Identity Credential - enables managed identity to act as the app registration
resource "azuread_application_federated_identity_credential" "mcp_proxy_managed_identity" {
  count          = var.enable_entra_setup ? 1 : 0
  application_id = azuread_application.mcp_proxy[0].id
  display_name   = local.federated_identity_credential_name
  description    = "Federated credential for ${var.environment_name} managed identity to access app registration without secrets"

  audiences = ["api://AzureADTokenExchange"]
  issuer    = "https://login.microsoftonline.com/${data.azurerm_client_config.current.tenant_id}/v2.0"
  subject   = azurerm_user_assigned_identity.managed_identity.principal_id
}

# Application password (client secret) for OBO token exchange
# The MCP Proxy needs a secret to perform the OBO token exchange with Entra ID
resource "azuread_application_password" "mcp_proxy_secret" {
  count          = var.enable_entra_setup && var.create_app_secret ? 1 : 0
  application_id = azuread_application.mcp_proxy[0].id
  display_name   = "${var.environment_name}-obo-secret"

  # Secret expiration (configurable via variable)
  end_date = timeadd(timestamp(), "${var.app_secret_expiry_hours}h")

  lifecycle {
    ignore_changes = [end_date]
  }
}

# ==============================================================================
# Downstream API App Registration (e.g., SuccessFactors Proxy)
# Optional: Only create if downstream_api_name is provided
# 
# This represents the downstream API that the MCP Proxy will call via OBO.
# In production, this would typically be pre-existing (like SuccessFactors).
# ==============================================================================

resource "random_uuid" "downstream_user_impersonation_scope_id" {
  count = var.enable_entra_setup && var.downstream_api_name != "" ? 1 : 0
}

resource "azuread_application" "downstream_api" {
  count        = var.enable_entra_setup && var.downstream_api_name != "" ? 1 : 0
  display_name = var.downstream_api_name

  identifier_uris  = ["api://${data.azurerm_client_config.current.tenant_id}/${var.downstream_api_name}"]
  sign_in_audience = "AzureADMyOrg"

  api {
    requested_access_token_version = 2

    # Pre-authorize the MCP Proxy to access this API without consent prompt
    known_client_applications = [azuread_application.mcp_proxy[0].client_id]

    # Expose scope for the MCP Proxy to request via OBO
    oauth2_permission_scope {
      id                         = random_uuid.downstream_user_impersonation_scope_id[0].result
      value                      = "user_impersonation"
      type                       = "User"
      admin_consent_display_name = "Access ${var.downstream_api_name} on behalf of user"
      admin_consent_description  = "Allow access to ${var.downstream_api_name} API on behalf of the signed-in user."
      user_consent_display_name  = "Access ${var.downstream_api_name} on your behalf"
      user_consent_description   = "Allow the application to access ${var.downstream_api_name} on your behalf."
      enabled                    = true
    }
  }

  tags = ["DownstreamAPI", "OBO", var.downstream_api_name]
}

resource "azuread_service_principal" "downstream_api" {
  count        = var.enable_entra_setup && var.downstream_api_name != "" ? 1 : 0
  client_id    = azuread_application.downstream_api[0].client_id
  use_existing = true
  tags         = ["DownstreamAPI", "OBO", var.downstream_api_name]
}

# Pre-authorize the MCP Proxy Service Principal for the downstream API scope
# This allows OBO without additional user consent
resource "azuread_application_pre_authorized" "mcp_proxy_to_downstream" {
  count                = var.enable_entra_setup && var.downstream_api_name != "" ? 1 : 0
  application_id       = azuread_application.downstream_api[0].id
  authorized_client_id = azuread_application.mcp_proxy[0].client_id

  permission_ids = [
    random_uuid.downstream_user_impersonation_scope_id[0].result
  ]
}

# ==============================================================================
# Admin Consent / Delegated Permission Grants
# Note: Full admin consent often requires manual action in Azure Portal
# This creates the permission request; admin must still grant consent
# ==============================================================================

# Data source to look up Microsoft Graph Service Principal (common downstream API)
data "azuread_service_principal" "msgraph" {
  count     = var.enable_entra_setup ? 1 : 0
  client_id = "00000003-0000-0000-c000-000000000000" # Microsoft Graph
}

# Grant delegated permissions from MCP Proxy to Microsoft Graph (if configured)
resource "azuread_service_principal_delegated_permission_grant" "mcp_proxy_to_graph" {
  count                                = var.enable_entra_setup && var.grant_graph_permissions ? 1 : 0
  service_principal_object_id          = azuread_service_principal.mcp_proxy[0].object_id
  resource_service_principal_object_id = data.azuread_service_principal.msgraph[0].object_id
  claim_values                         = var.graph_delegated_permissions
}

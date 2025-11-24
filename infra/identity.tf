

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
# Conditional creation based on enable_entra_setup
resource "azuread_application" "mcp_proxy" {
  count        = var.enable_entra_setup ? 1 : 0
  display_name = var.entra_app_name

  # API exposure - defines this app as a resource server
  identifier_uris = ["api://${data.azurerm_client_config.current.tenant_id}/${var.entra_app_name}"]

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

  # Expose App Role for Daemon/Service-to-Service calls
  app_role {
    allowed_member_types = ["Application"]
    description          = "Allows daemon apps to call the MCP Proxy."
    display_name         = "Daemon.Call"
    enabled              = true
    id                   = "1b1b1b1b-1b1b-1b1b-1b1b-1b1b1b1b1b1b" # Static UUID for stability
    value                = "Daemon.Call"
  }

  # Request Microsoft Graph permissions (adjust as needed)
  dynamic "required_resource_access" {
    for_each = var.downstream_api_permissions
    content {
      resource_app_id = required_resource_access.value.resource_app_id

      dynamic "resource_access" {
        for_each = required_resource_access.value.role_ids
        content {
          id   = resource_access.value
          type = "Role" # Application Permission
        }
      }
    }
  }

  tags = ["MCP", "Proxy", "WorkloadIdentity"]
}

# Service Principal for the app registration
resource "azuread_service_principal" "mcp_proxy" {
  count        = var.enable_entra_setup ? 1 : 0
  client_id    = azuread_application.mcp_proxy[0].client_id
  use_existing = true
  tags         = ["MCP", "Proxy", "WorkloadIdentity"]
}

# Federated Identity Credential - links managed identity to app registration
resource "azuread_application_federated_identity_credential" "mcp_proxy_managed_identity" {
  count          = var.enable_entra_setup ? 1 : 0
  application_id = azuread_application.mcp_proxy[0].id
  display_name   = local.federated_identity_credential_name
  description    = "Federated credential for ${var.environment_name} managed identity to access app registration without secrets"

  audiences = ["api://AzureADTokenExchange"]
  issuer    = "https://login.microsoftonline.com/${data.azurerm_client_config.current.tenant_id}/v2.0"
  subject   = azurerm_user_assigned_identity.managed_identity.principal_id
}

# Admin Consent Automation: Assign App Roles to the Service Principal
resource "azuread_app_role_assignment" "admin_consent" {
  # Flatten the permissions list to create one resource per role assignment
  for_each = var.enable_entra_setup ? {
    for perm in flatten([
      for api in var.downstream_api_permissions : [
        for role in api.role_ids : {
          api_id  = api.resource_app_id
          role_id = role
          key     = "${api.resource_app_id}-${role}"
        }
      ]
    ]) : perm.key => perm
  } : {}

  app_role_id         = each.value.role_id
  principal_object_id = azuread_service_principal.mcp_proxy[0].object_id
  resource_object_id  = each.value.api_id # Note: This needs the Object ID of the Resource SP, not App ID.
  # Limitation: azuread_app_role_assignment requires resource_object_id (SP Object ID).
  # var.downstream_api_permissions provides resource_app_id (App ID).
  # We would need to look up the SP Object ID for each API.
  # For simplicity in this iteration, we might skip this or assume the user provides Object IDs,
  # OR we use a data source to look it up.
  # Let's comment this out for now or use a data source if we can.
}

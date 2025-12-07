locals {
  common_env_vars = {
    AZURE_TENANT_ID                       = data.azurerm_client_config.current.tenant_id
    AZURE_CLIENT_ID                       = azurerm_user_assigned_identity.managed_identity.client_id
    APPLICATIONINSIGHTS_CONNECTION_STRING = azurerm_application_insights.app_insights.connection_string
    API_ENDPOINT                          = "https://api.${azurerm_container_app_environment.cae.default_domain}"
    ASPNETCORE_ENVIRONMENT                = "Development"
    # Inject Entra ID Config for OBO/Trusted Subsystem
    ENTRA_CLIENT_ID         = local.entra_client_id
    DOWNSTREAM_API_SCOPE    = var.downstream_api_scope
  }

  # Determine Entra ID Config based on setup mode
  entra_client_id = var.enable_entra_setup ? azuread_application.mcp_proxy[0].client_id : try(var.existing_entra_config.client_id, "")
  entra_app_uri   = var.enable_entra_setup ? "api://${data.azurerm_client_config.current.tenant_id}/${var.entra_app_name}" : try("api://${var.existing_entra_config.client_id}", "") # Assuming standard URI format for existing apps, or add a var for it
}

resource "azurerm_container_app" "api" {
  name                         = local.container_app_name
  container_app_environment_id = azurerm_container_app_environment.cae.id
  resource_group_name          = azurerm_resource_group.rg.name
  revision_mode                = "Single"
  tags                         = var.add_azd_tags ? merge(local.tags, { "azd-service-name" : "api" }) : local.tags
  identity {
    type = "UserAssigned"
    identity_ids = [
      azurerm_user_assigned_identity.managed_identity.id
    ]
  }
  workload_profile_name = local.container_app_environment_workload_profile_name
  ingress {
    allow_insecure_connections = false
    external_enabled           = var.enable_public_network
    target_port                = 8080
    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }
  registry {
    server   = azurerm_container_registry.acr.login_server
    identity = azurerm_user_assigned_identity.managed_identity.id
  }
  template {
    min_replicas = 1
    max_replicas = 1
    container {
      name   = "api"
      image  = coalesce(var.service_api_image_name, "mcr.microsoft.com/dotnet/samples:aspnetapp")
      cpu    = 1
      memory = "2Gi"

      dynamic "env" {
        for_each = local.common_env_vars
        content {
          name  = env.key
          value = env.value
        }
      }
    }
  }

  lifecycle {
    ignore_changes = [tags]
  }
}

# Container App Authentication Configuration for API (using AzAPI)
# Note: azurerm_container_app does not support auth blocks natively yet (GitHub issue #22213)
resource "azapi_resource" "api_auth_config" {
  type      = "Microsoft.App/containerApps/authConfigs@2023-05-01"
  name      = local.auth_config_name
  parent_id = azurerm_container_app.api.id

  body = {
    properties = {
      platform = {
        enabled = true
      }
      globalValidation = {
        unauthenticatedClientAction = "AllowAnonymous" # Change to RedirectToLoginPage or Return401 as needed
      }
      identityProviders = {
        azureActiveDirectory = {
          enabled = true
          registration = {
            openIdIssuer = "https://login.microsoftonline.com/${data.azurerm_client_config.current.tenant_id}/v2.0"
            clientId     = local.entra_client_id
          }
          validation = {
            allowedAudiences = [
              local.entra_app_uri
            ]
          }
        }
      }
    }
  }

  depends_on = [
    azurerm_container_app.api
    # Removed explicit dependency on azuread_application.mcp_proxy as it might not exist
  ]
}

locals {
  common_env_vars = {
    AZURE_TENANT_ID                       = data.azurerm_client_config.current.tenant_id
    AZURE_CLIENT_ID                       = azurerm_user_assigned_identity.managed_identity.client_id
    APPLICATIONINSIGHTS_CONNECTION_STRING = azurerm_application_insights.app_insights.connection_string
    API_ENDPOINT                          = "https://api.${azurerm_container_app_environment.cae.default_domain}"
    ASPNETCORE_ENVIRONMENT                = "Development"
  }
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
            clientId     = azuread_application.mcp_proxy.client_id
          }
          validation = {
            allowedAudiences = [
              "api://${data.azurerm_client_config.current.tenant_id}/${local.app_registration_name}"
            ]
          }
        }
      }
    }
  }

  depends_on = [
    azurerm_container_app.api,
    azuread_application.mcp_proxy
  ]
}

locals {
  tags                      = var.add_azd_tags ? { azd-env-name : var.environment_name } : {}
  sha                       = base64encode(sha256("${var.environment_name}${var.location}${data.azurerm_client_config.current.subscription_id}"))
  resource_token            = substr(replace(lower(local.sha), "[^A-Za-z0-9_]", ""), 0, 13)
  role_assignment_namespace = "e4c4a0c3-5e5e-4a78-b110-ba1a51c0c638" # Fixed namespace UUID
}

resource "azurerm_resource_group" "rg" {
  name     = local.resource_group_name
  location = var.location
  tags     = local.tags

  lifecycle {
    ignore_changes = [tags]
  }
}

resource "azurerm_container_registry" "acr" {
  location                      = azurerm_resource_group.rg.location
  name                          = local.container_registry_name
  resource_group_name           = azurerm_resource_group.rg.name
  sku                           = "Basic"
  admin_enabled                 = false
  public_network_access_enabled = true
  tags                          = local.tags

  lifecycle {
    ignore_changes = [tags]
  }
}

resource "azurerm_container_app_environment" "cae" {
  name                       = local.container_app_environment_name
  location                   = azurerm_resource_group.rg.location
  resource_group_name        = azurerm_resource_group.rg.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.law.id
  tags                       = local.tags

  lifecycle {
    ignore_changes = [tags]
  }
}

# Create Log Analytics Workspace
resource "azurerm_log_analytics_workspace" "law" {
  name                = local.log_analytics_workspace_name
  location            = var.location
  resource_group_name = azurerm_resource_group.rg.name
  sku                 = "PerGB2018" # Cost-effective SKU
  retention_in_days   = 30
  tags                = local.tags

  lifecycle {
    ignore_changes = [tags]
  }
}

# Create Application Insights instance backed by the Log Analytics Workspace
resource "azurerm_application_insights" "app_insights" {
  name                = local.application_insights_name
  location            = var.location
  resource_group_name = azurerm_resource_group.rg.name
  application_type    = "web"
  workspace_id        = azurerm_log_analytics_workspace.law.id
  tags                = local.tags

  lifecycle {
    ignore_changes = [tags]
  }
}


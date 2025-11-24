output "AZURE_LOCATION" {
  value = var.location
}

output "AZURE_TENANT_ID" {
  value = data.azurerm_client_config.current.tenant_id
}

output "MANAGED_IDENTITY_CLIENT_ID" {
  value = azurerm_user_assigned_identity.managed_identity.client_id
}

output "APPLICATIONINSIGHTS_CONNECTION_STRING" {
  sensitive = true
  value     = azurerm_application_insights.app_insights.connection_string
}

output "ENABLE_LOCAL_DEVELOPER" {
  value = var.enable_local_developer
}

output "AZURE_CONTAINER_REGISTRY_ENDPOINT" {
  value = azurerm_container_registry.acr.login_server
}

output "MCP_PROXY_APP_CLIENT_ID" {
  value       = var.enable_entra_setup ? azuread_application.mcp_proxy[0].client_id : try(var.existing_entra_config.client_id, "")
  description = "Client ID of the MCP Proxy Entra ID app registration"
}

output "MCP_PROXY_APP_OBJECT_ID" {
  value       = var.enable_entra_setup ? azuread_application.mcp_proxy[0].object_id : try(var.existing_entra_config.object_id, "")
  description = "Object ID of the MCP Proxy Entra ID app registration"
}

output "MCP_PROXY_IDENTIFIER_URI" {
  value       = var.enable_entra_setup ? "api://${data.azurerm_client_config.current.tenant_id}/${var.entra_app_name}" : try("api://${var.existing_entra_config.client_id}", "")
  description = "Identifier URI for the MCP Proxy app registration"
}
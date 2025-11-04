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
  value       = azuread_application.mcp_proxy.client_id
  description = "Client ID of the MCP Proxy Entra ID app registration"
}

output "MCP_PROXY_APP_OBJECT_ID" {
  value       = azuread_application.mcp_proxy.object_id
  description = "Object ID of the MCP Proxy Entra ID app registration"
}

output "MCP_PROXY_IDENTIFIER_URI" {
  value       = "api://${data.azurerm_client_config.current.tenant_id}/${var.environment_name}-mcp-proxy"
  description = "Identifier URI for the MCP Proxy app registration"
}
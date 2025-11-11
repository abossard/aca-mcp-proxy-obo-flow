# Networking output variables

# ============================================================================
# Container App Endpoints
# ============================================================================

output "CONTAINER_APP_FQDN" {
  value       = azurerm_container_app.api.ingress[0].fqdn
  description = "Fully qualified domain name of the container app"
}

output "CONTAINER_APP_ENVIRONMENT_ID" {
  value       = azurerm_container_app_environment.cae.id
  description = "Resource ID of the Container Apps environment"
}

output "CONTAINER_APP_ENVIRONMENT_DEFAULT_DOMAIN" {
  value       = azurerm_container_app_environment.cae.default_domain
  description = "Default domain of the Container Apps environment"
}

# ============================================================================
# Networking Pattern Decision (What was actually deployed)
# ============================================================================

output "NETWORKING_PATTERN" {
  value = local.enable_vnet_integration && local.enable_private_endpoint ? "Zero Trust (VNet + Private Endpoint)" : (
    local.enable_vnet_integration ? "VNet Integration Only" : (
      local.enable_private_endpoint ? "Private Endpoint Only" : "Default (Public)"
    )
  )
  description = "The networking pattern deployed: 'Default (Public)', 'VNet Integration Only', 'Private Endpoint Only', or 'Zero Trust (VNet + Private Endpoint)'"
}

output "NETWORKING_DECISION_SUMMARY" {
  value = {
    pattern                   = local.enable_vnet_integration && local.enable_private_endpoint ? "Zero Trust (VNet + Private Endpoint)" : (local.enable_vnet_integration ? "VNet Integration Only" : (local.enable_private_endpoint ? "Private Endpoint Only" : "Default (Public)"))
    public_network_enabled    = local.public_network_access_enabled
    vnet_integration_enabled  = local.enable_vnet_integration
    private_endpoint_enabled  = local.enable_private_endpoint
    dummy_vnet_created        = var.create_dummy_vnet
    can_access_private_resources = local.enable_vnet_integration
    secure_inbound            = local.enable_private_endpoint
    egress_control_available  = local.enable_vnet_integration
  }
  description = "Complete summary of networking decisions and capabilities"
}

# ============================================================================
# VNet Integration Details
# ============================================================================

output "VNET_INTEGRATION_ENABLED" {
  value       = local.enable_vnet_integration
  description = "Whether VNet integration is enabled (Container Apps deployed into custom VNet). Enables: access to private resources, egress control via UDR"
}

output "CONTAINER_APPS_SUBNET_ID" {
  value       = local.enable_vnet_integration ? local.resolved_cae_subnet_id : null
  description = "Subnet ID where Container Apps Environment is deployed (null if using default Azure network)"
}

output "CONTAINER_APPS_SUBNET_ADDRESS" {
  value       = local.enable_vnet_integration && var.create_dummy_vnet ? "10.100.0.0/27" : null
  description = "CIDR range of Container Apps subnet (only populated for dummy VNet)"
}

# ============================================================================
# Private Endpoint Details
# ============================================================================

output "PRIVATE_ENDPOINT_ENABLED" {
  value       = local.enable_private_endpoint
  description = "Whether private endpoint is enabled (secure inbound access from VNet). Logic: enable_public_network=false AND (create_dummy_vnet=true OR private_endpoint_subnet_id provided)"
}

output "PRIVATE_ENDPOINT_IP" {
  value       = local.enable_private_endpoint ? azurerm_private_endpoint.aca[0].private_service_connection[0].private_ip_address : null
  description = "Private IP address of the private endpoint (null if not enabled). Use this IP for DNS resolution in your VNet."
}

output "PRIVATE_ENDPOINT_SUBNET_ID" {
  value       = local.enable_private_endpoint ? local.resolved_pe_subnet_id : null
  description = "Subnet ID where private endpoint is deployed (null if not enabled)"
}

output "PRIVATE_DNS_ZONE_NAME" {
  value       = local.enable_private_endpoint ? azurerm_private_dns_zone.aca[0].name : null
  description = "Name of the private DNS zone for Container Apps (null if private endpoint not enabled). Format: privatelink.{region}.azurecontainerapps.io"
}

# ============================================================================
# Public Network Access
# ============================================================================

output "PUBLIC_NETWORK_ENABLED" {
  value       = local.public_network_access_enabled
  description = "Whether public network access is enabled. When false, apps are only accessible via private endpoint or internally within environment."
}

output "PUBLIC_NETWORK_ACCESS_SETTING" {
  value       = azurerm_container_app_environment.cae.public_network_access
  description = "Actual public_network_access setting on Container Apps Environment: 'Enabled' or 'Disabled'"
}

# ============================================================================
# Testing & Development
# ============================================================================

output "DUMMY_VNET_CREATED" {
  value       = var.create_dummy_vnet
  description = "Whether a dummy VNet was created for testing. Creates both Container Apps subnet (10.100.0.0/27) and Private Endpoint subnet (10.100.1.0/24)"
}

output "DUMMY_VNET_ID" {
  value       = var.create_dummy_vnet ? azurerm_virtual_network.dummy_vnet[0].id : null
  description = "Resource ID of dummy VNet (null if not created)"
}

output "DUMMY_VNET_ADDRESS_SPACE" {
  value       = var.create_dummy_vnet ? "10.100.0.0/16" : null
  description = "Address space of dummy VNet (null if not created)"
}

# ============================================================================
# Access Instructions
# ============================================================================

output "ACCESS_INSTRUCTIONS" {
  value = local.enable_private_endpoint ? (
    "Private endpoint enabled. Access from VNet:\n  1. Ensure you're connected to VNet ${local.enable_private_endpoint ? local.resolved_pe_vnet_id : "N/A"}\n  2. DNS should resolve ${azurerm_container_app.api.ingress[0].fqdn} to ${local.enable_private_endpoint ? azurerm_private_endpoint.aca[0].private_service_connection[0].private_ip_address : "N/A"}\n  3. Use: curl https://${azurerm_container_app.api.ingress[0].fqdn}"
  ) : (
    local.public_network_access_enabled ? (
      "Public access enabled. Access from anywhere:\n  curl https://${azurerm_container_app.api.ingress[0].fqdn}"
    ) : (
      "Public access disabled but no private endpoint. Apps only accessible internally within Container Apps environment."
    )
  )
  description = "Instructions for accessing the deployed container app based on networking configuration"
}

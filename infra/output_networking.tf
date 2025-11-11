# Networking output variables

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

output "PRIVATE_ENDPOINT_ENABLED" {
  value       = local.enable_private_endpoint
  description = "Whether private endpoint is enabled for the Container Apps environment"
}

output "PRIVATE_ENDPOINT_IP" {
  value       = local.enable_private_endpoint ? azurerm_private_endpoint.aca[0].private_service_connection[0].private_ip_address : null
  description = "Private IP address of the private endpoint (null if not enabled)"
}

output "PRIVATE_DNS_ZONE_NAME" {
  value       = local.enable_private_endpoint ? azurerm_private_dns_zone.aca[0].name : null
  description = "Name of the private DNS zone (null if private endpoint not enabled)"
}

output "DUMMY_VNET_CREATED" {
  value       = var.create_dummy_vnet
  description = "Whether a dummy VNet was created for testing"
}

output "VNET_INTEGRATION_ENABLED" {
  value       = local.enable_vnet_integration
  description = "Whether VNet integration is enabled for the Container Apps environment"
}

output "CONTAINER_APPS_SUBNET_ID" {
  value       = local.enable_vnet_integration ? local.resolved_cae_subnet_id : null
  description = "Subnet ID where Container Apps Environment is deployed (null if using default Azure network)"
}

output "PUBLIC_NETWORK_ENABLED" {
  value       = var.enable_public_network
  description = "Whether public network access is enabled for container apps"
}

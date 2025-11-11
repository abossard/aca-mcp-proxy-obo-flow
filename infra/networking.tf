# Networking configuration for Azure Container Apps
# Implements Option 1 (Private Endpoint) from docs/networking-options.md

locals {
  # Determine if private endpoint is enabled:
  # - Only create when enable_public_network = false
  # - AND either create_dummy_vnet is true OR private_endpoint_subnet_id is provided
  enable_private_endpoint = !var.enable_public_network && (var.create_dummy_vnet || var.private_endpoint_subnet_id != "")

  # Determine if VNet integration is enabled for Container Apps Environment:
  # - When create_dummy_vnet is true OR container_apps_subnet_id is specified
  enable_vnet_integration = var.create_dummy_vnet || var.container_apps_subnet_id != ""

  # Public network access is directly controlled by enable_public_network variable
  public_network_access_enabled = var.enable_public_network

  # VNet and subnet names for optional dummy network
  vnet_name       = "${var.environment_name}-vnet"
  pe_subnet_name  = "${var.environment_name}-pe-subnet"
  cae_subnet_name = "${var.environment_name}-cae-subnet"

  # Private DNS zone name (region-specific)
  private_dns_zone_name = "privatelink.${var.location}.azurecontainerapps.io"

  # Private endpoint resource names
  private_endpoint_name            = "${var.environment_name}-aca-private-endpoint"
  private_endpoint_connection_name = "${var.environment_name}-aca-pe-connection"
  private_dns_link_name            = "${var.environment_name}-pe-dns-link"
}

# Optional: Create dummy VNet with subnets for Container Apps Environment and Private Endpoint
resource "azurerm_virtual_network" "dummy_vnet" {
  count               = var.create_dummy_vnet ? 1 : 0
  name                = local.vnet_name
  location            = var.location
  resource_group_name = azurerm_resource_group.rg.name
  address_space       = ["10.100.0.0/16"]
  tags                = local.tags

  lifecycle {
    ignore_changes = [tags]
  }
}

# Subnet for Container Apps Environment (requires /27 minimum for Workload Profiles)
resource "azurerm_subnet" "cae_subnet" {
  count                = var.create_dummy_vnet ? 1 : 0
  name                 = local.cae_subnet_name
  resource_group_name  = azurerm_resource_group.rg.name
  virtual_network_name = azurerm_virtual_network.dummy_vnet[0].name
  address_prefixes     = ["10.100.0.0/27"] # 32 IPs - minimum for Workload Profiles

  delegation {
    name = "Microsoft.App.environments"
    service_delegation {
      name = "Microsoft.App/environments"
      actions = [
        "Microsoft.Network/virtualNetworks/subnets/join/action"
      ]
    }
  }
}

# Subnet for Private Endpoint
resource "azurerm_subnet" "pe_subnet" {
  count                = var.create_dummy_vnet ? 1 : 0
  name                 = local.pe_subnet_name
  resource_group_name  = azurerm_resource_group.rg.name
  virtual_network_name = azurerm_virtual_network.dummy_vnet[0].name
  address_prefixes     = ["10.100.1.0/24"]
}

# Determine the subnet and VNet IDs to use (provided or created)
locals {
  # Private endpoint resources
  resolved_pe_subnet_id = var.create_dummy_vnet ? azurerm_subnet.pe_subnet[0].id : var.private_endpoint_subnet_id
  resolved_pe_vnet_id   = var.create_dummy_vnet ? azurerm_virtual_network.dummy_vnet[0].id : var.private_endpoint_vnet_id

  # Container Apps Environment resources
  resolved_cae_subnet_id = var.create_dummy_vnet ? azurerm_subnet.cae_subnet[0].id : var.container_apps_subnet_id
}

# Private Endpoint for Container Apps Environment
resource "azurerm_private_endpoint" "aca" {
  count               = local.enable_private_endpoint ? 1 : 0
  name                = local.private_endpoint_name
  location            = var.location
  resource_group_name = azurerm_resource_group.rg.name
  subnet_id           = local.resolved_pe_subnet_id
  tags                = local.tags

  private_service_connection {
    name                           = local.private_endpoint_connection_name
    private_connection_resource_id = azurerm_container_app_environment.cae.id
    is_manual_connection           = false
    subresource_names              = ["managedEnvironments"]
  }

  private_dns_zone_group {
    name                 = "${local.private_endpoint_name}-dns-group"
    private_dns_zone_ids = [azurerm_private_dns_zone.aca[0].id]
  }

  lifecycle {
    ignore_changes = [tags]
  }
}

# Private DNS Zone for Container Apps
resource "azurerm_private_dns_zone" "aca" {
  count               = local.enable_private_endpoint ? 1 : 0
  name                = local.private_dns_zone_name
  resource_group_name = azurerm_resource_group.rg.name
  tags                = local.tags

  lifecycle {
    ignore_changes = [tags]
  }
}

# Link Private DNS Zone to VNet
resource "azurerm_private_dns_zone_virtual_network_link" "aca" {
  count                 = local.enable_private_endpoint ? 1 : 0
  name                  = local.private_dns_link_name
  resource_group_name   = azurerm_resource_group.rg.name
  private_dns_zone_name = azurerm_private_dns_zone.aca[0].name
  virtual_network_id    = local.resolved_pe_vnet_id
  registration_enabled  = false
  tags                  = local.tags

  lifecycle {
    ignore_changes = [tags]
  }
}

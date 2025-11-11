# ============================================================================
# Azure Container Apps Networking Configuration
# ============================================================================
# This file contains ALL networking logic and resources.
# See docs/networking.md for complete architecture documentation.
#
# Networking Patterns Supported:
#   1. Default (Public)           - No VNet, publicly accessible
#   2. VNet Integration Only      - Custom VNet for egress + private resources
#   3. Private Endpoint Only      - Secure inbound via Private Link
#   4. Zero Trust (VNet + PE)     - Full inbound + outbound control
# ============================================================================

# ============================================================================
# DECISION LOGIC - These locals determine what gets deployed
# ============================================================================

locals {
  # -------------------------------------------------------------------------
  # Private Endpoint Decision Logic
  # -------------------------------------------------------------------------
  # Private endpoints are created ONLY when:
  #   1. enable_public_network = false (explicitly disabled), AND
  #   2. Either:
  #      - create_dummy_vnet = true (auto-create test VNet), OR
  #      - private_endpoint_subnet_id is provided (custom VNet)
  #
  # Why? Private endpoints require public_network_access = "Disabled" on the
  # Container Apps Environment. This prevents accidental misconfiguration.
  #
  # Examples:
  #   enable_public_network=true + PE subnet provided = NO PE (PE ignored)
  #   enable_public_network=false + PE subnet provided = PE created ✓
  #   enable_public_network=false + create_dummy_vnet=true = PE created ✓
  # -------------------------------------------------------------------------
  enable_private_endpoint = !var.enable_public_network && (var.create_dummy_vnet || var.private_endpoint_subnet_id != "")

  # -------------------------------------------------------------------------
  # VNet Integration Decision Logic
  # -------------------------------------------------------------------------
  # VNet integration is enabled when:
  #   - create_dummy_vnet = true (auto-create test VNet), OR
  #   - container_apps_subnet_id is provided (custom VNet)
  #
  # When enabled, Container Apps Environment is deployed into a custom subnet
  # with delegation to Microsoft.App/environments. This enables:
  #   - Access to private resources (SQL MI, Storage with PE, Key Vault, etc.)
  #   - Egress control via User-Defined Routes (UDR) + Azure Firewall
  #   - Predictable IP allocation within subnet
  #
  # Independent of private endpoint - you can have VNet integration WITHOUT
  # private endpoint (public inbound, private egress).
  # -------------------------------------------------------------------------
  enable_vnet_integration = var.create_dummy_vnet || var.container_apps_subnet_id != ""

  # -------------------------------------------------------------------------
  # Public Network Access Decision
  # -------------------------------------------------------------------------
  # Directly controlled by enable_public_network variable.
  # Sets Container Apps Environment public_network_access property to:
  #   - "Enabled" when true (apps accept public traffic)
  #   - "Disabled" when false (apps only accessible via PE or internally)
  # -------------------------------------------------------------------------
  public_network_access_enabled = var.enable_public_network

  # -------------------------------------------------------------------------
  # Resource Naming
  # -------------------------------------------------------------------------
  vnet_name       = "${var.environment_name}-vnet"
  pe_subnet_name  = "${var.environment_name}-pe-subnet"
  cae_subnet_name = "${var.environment_name}-cae-subnet"

  # -------------------------------------------------------------------------
  # Private DNS Configuration
  # -------------------------------------------------------------------------
  # Region-specific private DNS zone for Azure Container Apps
  # Format: privatelink.{region}.azurecontainerapps.io
  # -------------------------------------------------------------------------
  private_dns_zone_name = "privatelink.${var.location}.azurecontainerapps.io"

  # -------------------------------------------------------------------------
  # Private Endpoint Naming
  # -------------------------------------------------------------------------
  private_endpoint_name            = "${var.environment_name}-aca-private-endpoint"
  private_endpoint_connection_name = "${var.environment_name}-aca-pe-connection"
  private_dns_link_name            = "${var.environment_name}-pe-dns-link"
}

# ============================================================================
# DUMMY VNET RESOURCES (Testing Only)
# ============================================================================
# Creates a complete test VNet with TWO subnets:
#   - Container Apps Environment subnet: 10.100.0.0/27 (32 IPs, delegated)
#   - Private Endpoint subnet: 10.100.1.0/24 (256 IPs, not delegated)
#
# Only created when: create_dummy_vnet = true
# Use for testing/demo purposes only - not recommended for production
# ============================================================================
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

# Container Apps Environment subnet - Workload Profiles requires /27 minimum
# IMPORTANT: Must be delegated to Microsoft.App/environments
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

# Private Endpoint subnet - No delegation required
# Can be shared with other private endpoints
resource "azurerm_subnet" "pe_subnet" {
  count                = var.create_dummy_vnet ? 1 : 0
  name                 = local.pe_subnet_name
  resource_group_name  = azurerm_resource_group.rg.name
  virtual_network_name = azurerm_virtual_network.dummy_vnet[0].name
  address_prefixes     = ["10.100.1.0/24"]
}

# ============================================================================
# SUBNET RESOLUTION LOGIC
# ============================================================================
# Determines which subnets to use based on configuration:
#   - If create_dummy_vnet=true: Use auto-created subnets
#   - Otherwise: Use user-provided subnet IDs from variables
#
# This allows flexibility: users can provide their own VNets OR use dummy VNet
# ============================================================================

locals {
  # Private endpoint resources - resolved subnet and VNet IDs
  resolved_pe_subnet_id = var.create_dummy_vnet ? azurerm_subnet.pe_subnet[0].id : var.private_endpoint_subnet_id
  resolved_pe_vnet_id   = var.create_dummy_vnet ? azurerm_virtual_network.dummy_vnet[0].id : var.private_endpoint_vnet_id

  # Container Apps Environment resources - resolved subnet ID
  resolved_cae_subnet_id = var.create_dummy_vnet ? azurerm_subnet.cae_subnet[0].id : var.container_apps_subnet_id
}

# ============================================================================
# PRIVATE ENDPOINT RESOURCES
# ============================================================================
# Created when: enable_private_endpoint = true
# Provides secure inbound access to Container Apps from within VNet
#
# IMPORTANT: Private endpoints control INBOUND traffic only
#   - Inbound: VNet → Private Endpoint → Container Apps ✓
#   - Outbound: Container Apps → Internet (NOT through PE) ✗
#
# For egress control, use VNet integration + UDR + Azure Firewall
# ============================================================================
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

# Private DNS Zone for Container Apps Private Link
# Required for DNS resolution of private endpoint
resource "azurerm_private_dns_zone" "aca" {
  count               = local.enable_private_endpoint ? 1 : 0
  name                = local.private_dns_zone_name
  resource_group_name = azurerm_resource_group.rg.name
  tags                = local.tags

  lifecycle {
    ignore_changes = [tags]
  }
}

# Link Private DNS Zone to VNet for DNS resolution
# Enables automatic DNS resolution of Container Apps FQDN to private IP
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

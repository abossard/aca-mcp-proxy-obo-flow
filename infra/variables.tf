# Input variables for the module

variable "location" {
  description = "The supported Azure location where the resources are deployed"
  type        = string
}

variable "environment_name" {
  description = "The name of the azd environment to be deployed"
  type        = string
}

variable "enable_local_developer" {
  description = "Set to true to enable local developer configurations"
  type        = bool
  default     = true
}

variable "service_api_image_name" {
  description = "The name of the service api image"
  type        = string
}

variable "add_azd_tags" {
  description = "Whether to add Azure Developer CLI (azd) specific tags to resources. Set to false when not using azd for deployment."
  type        = bool
  default     = true
}

# Networking variables

variable "enable_public_network" {
  description = "Whether to enable public network access for the Container Apps environment. When true, no private endpoint is created regardless of other settings. When false, apps are only accessible internally or via private endpoint (if configured)."
  type        = bool
  default     = true
}

variable "private_endpoint_subnet_id" {
  description = "The subnet ID where the private endpoint will be created. If empty, no private endpoint is created. Required format: /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Network/virtualNetworks/{vnet}/subnets/{subnet}"
  type        = string
  default     = ""
}

variable "private_endpoint_vnet_id" {
  description = "The VNet ID to link the private DNS zone to. Required when private_endpoint_subnet_id is provided. Format: /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Network/virtualNetworks/{vnet}"
  type        = string
  default     = ""
}

variable "create_dummy_vnet" {
  description = "Create a dummy VNet with subnets for both Container Apps Environment AND private endpoint. Only used for testing/demo. Set to true to create a new VNet (10.100.0.0/16) with subnets for Container Apps (/27) and private endpoint (/24)."
  type        = bool
  default     = true
}

variable "container_apps_subnet_id" {
  description = "The subnet ID where the Container Apps Environment will be deployed for VNet integration. If empty and create_dummy_vnet is false, environment uses default Azure network. Required format: /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Network/virtualNetworks/{vnet}/subnets/{subnet}. Minimum subnet size: /27 (32 IPs) for Workload Profiles environment."
  type        = string
  default     = ""
}

# ==============================================================================
# Entra ID / OBO Configuration Variables
# ==============================================================================

variable "enable_entra_setup" {
  description = "Master switch to create Entra ID resources (App Registration, SP, Federated Credential). Set to true for 'Team M' (Dev/Full Auto). Set to false for 'Team J' (Prod/Pre-provisioned)."
  type        = bool
  default     = false
}

variable "entra_app_name" {
  description = "The display name for the MCP Proxy Entra ID App Registration. Only used if enable_entra_setup is true."
  type        = string
  default     = "mcp-proxy-app"
}

variable "create_app_secret" {
  description = "Whether to create a client secret for the MCP Proxy app registration. Required for OBO token exchange. Set to false if using managed identity assertion instead."
  type        = bool
  default     = true
}

variable "app_secret_expiry_hours" {
  description = "Number of hours until the client secret expires. Default is 4320 hours (180 days / 6 months). Set to 8760 for 1 year."
  type        = number
  default     = 4320
}

variable "known_client_applications" {
  description = "List of client application IDs that are pre-authorized to access the MCP Proxy API. These clients can use OBO flow without additional consent."
  type        = list(string)
  default     = []
}

variable "downstream_api_name" {
  description = "Display name for the downstream API app registration (e.g., 'successfactors-api'). If empty, no downstream API registration is created. Use this to create a mock downstream API for testing OBO flow."
  type        = string
  default     = ""
}

variable "downstream_api_permissions" {
  description = "List of permissions required for downstream APIs. Supports both delegated (Scope) and application (Role) permissions. Only used if enable_entra_setup is true."
  type = list(object({
    resource_app_id            = string       # e.g. "00000003-0000-0000-c000-000000000000" (Microsoft Graph)
    delegated_permission_ids   = list(string) # e.g. ["e1fe6dd8-ba31-4d61-89e7-88639da4683d"] (User.Read)
    application_permission_ids = list(string) # e.g. ["df021288-bdef-4463-88db-98f22de89214"] (User.Read.All)
  }))
  default = []
}

variable "grant_graph_permissions" {
  description = "Whether to grant delegated permissions for Microsoft Graph. Requires admin consent."
  type        = bool
  default     = false
}

variable "graph_delegated_permissions" {
  description = "List of Microsoft Graph delegated permission claim values to grant (e.g., ['User.Read', 'openid', 'profile']). Only used if grant_graph_permissions is true."
  type        = list(string)
  default     = ["User.Read", "openid", "profile", "email"]
}

variable "existing_entra_config" {
  description = "Configuration for pre-provisioned Entra ID resources. Required if enable_entra_setup is false."
  type = object({
    client_id = string
    tenant_id = string
    object_id = string # Service Principal Object ID (for future use if needed)
  })
  default = null
}
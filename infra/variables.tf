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
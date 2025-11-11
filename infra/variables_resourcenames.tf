variable "resource_group_name" {
  description = "Optional override for resource group name. If empty, uses default naming convention."
  type        = string
  default     = ""
}

variable "container_registry_name" {
  description = "Optional override for container registry name. If empty, uses default naming convention."
  type        = string
  default     = ""
}

variable "container_app_environment_name" {
  description = "Optional override for container app environment name. If empty, uses default naming convention."
  type        = string
  default     = ""
}

variable "log_analytics_workspace_name" {
  description = "Optional override for log analytics workspace name. If empty, uses default naming convention."
  type        = string
  default     = ""
}

variable "application_insights_name" {
  description = "Optional override for application insights name. If empty, uses default naming convention."
  type        = string
  default     = ""
}

variable "managed_identity_name" {
  description = "Optional override for managed identity name. If empty, uses default naming convention."
  type        = string
  default     = ""
}

variable "app_registration_name" {
  description = "Optional override for app registration name. If empty, uses default naming convention."
  type        = string
  default     = ""
}

variable "federated_identity_credential_name" {
  description = "Optional override for federated identity credential name. If empty, uses default naming convention."
  type        = string
  default     = ""
}

variable "container_app_name" {
  description = "Optional override for container app name. If empty, uses default naming convention."
  type        = string
  default     = ""
}

variable "auth_config_name" {
  description = "Optional override for auth config name. If empty, uses default naming convention (must be 'current' per ARM API)."
  type        = string
  default     = ""
}

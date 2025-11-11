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
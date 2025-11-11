# Configure desired versions of terraform, azurerm provider
terraform {
  required_version = ">= 1.1.7, < 2.0.0"
  required_providers {
    azurerm = {
      version = "~>4.52.0"
      source  = "hashicorp/azurerm"
    }
    azapi = {
      source  = "Azure/azapi"
      version = "~>2.7.0"
    }
  }
}


provider "azapi" {
  # Configuration options
}

# Enable features for azurerm
provider "azurerm" {
  storage_use_azuread = true
  features {
    resource_group {
      prevent_deletion_if_contains_resources = false
    }
  }
}

# Access client_id, tenant_id, subscription_id and object_id configuration values
data "azurerm_client_config" "current" {}

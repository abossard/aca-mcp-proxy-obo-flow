locals {
  # Resource Group
  resource_group_name = coalesce(var.resource_group_name, "${var.environment_name}-rg")

  # Container Registry
  container_registry_name = coalesce(var.container_registry_name, "acr${var.environment_name}domal")

  # Container App Environment
  container_app_environment_name = coalesce(var.container_app_environment_name, "cae-${var.environment_name}")

  # Log Analytics Workspace
  log_analytics_workspace_name = coalesce(var.log_analytics_workspace_name, "${var.environment_name}-law")

  # Application Insights
  application_insights_name = coalesce(var.application_insights_name, "${var.environment_name}-appinsights")

  # Managed Identity
  managed_identity_name = coalesce(var.managed_identity_name, "${var.environment_name}-identity")

  # Entra ID App Registration
  app_registration_name = coalesce(var.app_registration_name, "${var.environment_name}-mcp-proxy")

  # Federated Identity Credential
  federated_identity_credential_name = coalesce(var.federated_identity_credential_name, "${var.environment_name}-managed-identity-federation")

  # Container App
  container_app_name = coalesce(var.container_app_name, "api")

  # Auth Config (must be "current" per ARM API requirement)
  auth_config_name = coalesce(var.auth_config_name, "current")
}

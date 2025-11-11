Team M:
# [✅] 2. Terraform: Resource Names TF Variables
# [✅] 2.1. All resource names should calculates in a locals_resource_names.tf where it has a local for each resource name. use the current resource name as this local value (construct it with the existing logic).

# [✅] 2.2 now add a variables_resourcenames.tf that has an entry for each resource name. If the entry is non-empty, it should overide the resource name of the resource in the locals_resource_names.tf.

# [✅] 3. Terraform: Update tags
# [✅] 3.1 the current tags are only relevant when AZD is used to deploy the resources. So add a new variable add_azd_tags (default: true). When this variable is true, the tags should be added to the resources.
# [✅] 3.2 Add lifecycle ignore to the tags, since azure policies might change or add tags

# [ ] 4. VNET Integration -> Create VNET -> (does private endpoint for ACA Env)
# [ ] 5. Configuration: Target Scope and OBO Exchange Function, configurable (API Scopes, App Registrations, Permissions, Admin Consent)


# [ ] 6. Readme file for the setup (e.g. based on the Terraform IaC)


Team J:
# [ ] 7. Integrate with Azure DevOps
# [ ] 8. (MCP Authentication Flow: JWT vs MCP Auth)
Team M:
# [✅] 2. Terraform: Resource Names TF Variables
# [✅] 2.1. All resource names should calculates in a locals_resource_names.tf where it has a local for each resource name. use the current resource name as this local value (construct it with the existing logic).

# [✅] 2.2 now add a variables_resourcenames.tf that has an entry for each resource name. If the entry is non-empty, it should overide the resource name of the resource in the locals_resource_names.tf.

# [✅] 3. Terraform: Update tags
# [✅] 3.1 the current tags are only relevant when AZD is used to deploy the resources. So add a new variable add_azd_tags (default: true). When this variable is true, the tags should be added to the resources.
# [✅] 3.2 Add lifecycle ignore to the tags, since azure policies might change or add tags

# [✅] 4. Network improvments:
# [✅] 4.1 research online what the different Azure Container Apps networking options are (vnet integration, private endpoints, and document them as markdown in a docs folder).
Best would be a completely private setup a hidden vnet and a private endpopint that can be added to another existing subnet.
Second best would be a small as possible subnet with vnet
Third best would be anything else.
This is only reasearch and documentation, no implementation needed.
# [✅] 4.2 implement the best possible networking Options 1 from docs/networking-options.md with this variables:
- always use workload profiles v2
- the containerapp can still be on the consumption plan
- add a variable enable_public_ingress (default: true)
- add variables to configure the private link, e.g. subnet id if it's empty, don't use private link
- add another variable: create dummy vnet/subnet (default: false). If true, create a new vnet and subnet for the private endpoint

# [ ] 5. Configuration: Target Scope and OBO Exchange Function, configurable (API Scopes, App Registrations, Permissions, Admin Consent)


# [ ] 6. Readme file for the setup (e.g. based on the Terraform IaC)


Team J:
# [ ] 7. Integrate with Azure DevOps
# [ ] 8. (MCP Authentication Flow: JWT vs MCP Auth)
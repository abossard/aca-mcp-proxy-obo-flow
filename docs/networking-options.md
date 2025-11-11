# Azure Container Apps Networking Options

This document outlines the different networking options available for Azure Container Apps, with a focus on VNet integration and private endpoints for secure deployments.

## Table of Contents
- [Overview](#overview)
- [Environment Types](#environment-types)
- [Networking Patterns](#networking-patterns)
- [Private Endpoint Setup (Option 1 - Best)](#private-endpoint-setup-option-1---best)
- [VNet Integration (Option 2 - Second Best)](#vnet-integration-option-2---second-best)
- [Default Public Access (Option 3)](#default-public-access-option-3)
- [Comparison Matrix](#comparison-matrix)
- [Cost Considerations](#cost-considerations)
- [Implementation Recommendations](#implementation-recommendations)

---

## Overview

Azure Container Apps provides flexible networking options to meet different security and connectivity requirements. The networking configuration is determined at the **environment level** and affects all container apps within that environment.

### Key Networking Concepts

- **Environment**: A secure boundary around one or more container apps with its own virtual network
- **Ingress**: Controls how traffic reaches your container apps (external vs internal)
- **Accessibility Level**: Determines whether apps are publicly accessible or private
- **VNet Integration**: Custom virtual network configuration for enhanced security

---

## Environment Types

Azure Container Apps supports two environment types with different networking capabilities:

### Workload Profiles Environment (v2) - **Recommended**

| Feature | Details |
|---------|---------|
| **Subnet Size** | Minimum `/27` (32 addresses) |
| **Reserved IPs** | 12 IPs for infrastructure |
| **IP Allocation** | Dedicated: 1 IP per node<br>Consumption: 1 IP per 10 replicas |
| **Private Endpoints** | ✅ Supported |
| **User Defined Routes (UDR)** | ✅ Supported |
| **NAT Gateway Egress** | ✅ Supported |
| **Plans** | Consumption + Dedicated |

### Consumption-Only Environment (v1) - Legacy

| Feature | Details |
|---------|---------|
| **Subnet Size** | Minimum `/23` (512 addresses) |
| **Reserved IPs** | 60-256 IPs for infrastructure |
| **IP Allocation** | 1 IP per replica |
| **Private Endpoints** | ❌ Not supported |
| **User Defined Routes (UDR)** | ❌ Not supported |
| **NAT Gateway Egress** | ❌ Not supported |
| **Plans** | Consumption only |

> **Important**: Always use Workload Profiles (v2) environments for new deployments to access the full networking feature set.

---

## Networking Patterns

### Pattern Comparison

| Pattern | Security Level | Complexity | Cost | Use Case |
|---------|----------------|------------|------|----------|
| **Private Endpoint** | 🔒🔒🔒 Highest | High | $$$ | Zero trust, fully private |
| **VNet Integration (Internal)** | 🔒🔒 High | Medium | $$ | Private within VNet |
| **VNet Integration (External)** | 🔒 Medium | Low | $ | Controlled public access |
| **Default Public** | ⚠️ Low | Minimal | $ | Development/testing |

---

## Private Endpoint Setup (Option 1 - Best)

### 🏆 This is the MOST SECURE option

Private endpoints allow you to connect to your Container Apps environment using a private IP address from your VNet, completely eliminating exposure to the public internet.

### Architecture

```
┌─────────────────────────────────────────────────────┐
│ Your Existing VNet (e.g., 10.1.0.0/16)             │
│                                                      │
│  ┌──────────────────────────────────────┐          │
│  │ Private Endpoint Subnet              │          │
│  │ (e.g., 10.1.0.0/24)                  │          │
│  │                                       │          │
│  │  ┌────────────────────────────┐      │          │
│  │  │ Private Endpoint           │      │          │
│  │  │ IP: 10.1.0.4               │◄─────┼──────────┤ Private DNS Zone
│  │  └────────────────────────────┘      │          │ *.region.azurecontainerapps.io
│  └──────────────────────────────────────┘          │
│                                                      │
│         ▼ Private Link                              │
│  ═══════════════════════                            │
└─────────────────────────────────────────────────────┘
         ║
         ║ Azure Private Link
         ║
┌────────▼──────────────────────────────────────────┐
│ Container Apps Environment (Hidden VNet)          │
│ • Public Network Access: DISABLED                 │
│ • No direct VNet integration needed               │
│ • Environment can be in different VNet            │
│                                                    │
│  ┌──────────────┐  ┌──────────────┐              │
│  │ Container    │  │ Container    │              │
│  │ App 1        │  │ App 2        │              │
│  └──────────────┘  └──────────────┘              │
└────────────────────────────────────────────────────┘
```

### Key Features

✅ **Completely private** - No public IP exposure  
✅ **Flexible placement** - Private endpoint VNet can be separate from Container Apps VNet  
✅ **Minimal subnet** - Only requires space for private endpoint (can share with other resources)  
✅ **Existing VNet integration** - Add to your existing network infrastructure  
✅ **Zero trust compliant** - Meets strictest security requirements

### Requirements

- **Environment Type**: Workload Profiles (v2) only
- **Public Network Access**: Must be DISABLED
- **Private Endpoint Subnet**: Any available subnet in your VNet
- **Private DNS Zone**: `privatelink.<region>.azurecontainerapps.io`
- **Container Apps Environment**: Can be with or without custom VNet

### Implementation Steps

1. **Create Container Apps Environment** (with or without VNet)
   ```bash
   az containerapp env create \
     --name my-environment \
     --resource-group my-rg \
     --location eastus \
     --public-network-access Disabled  # Critical!
   ```

2. **Create Private Endpoint** in your existing VNet
   ```bash
   az network private-endpoint create \
     --resource-group my-rg \
     --name my-private-endpoint \
     --vnet-name my-existing-vnet \
     --subnet my-subnet \
     --private-connection-resource-id <ENVIRONMENT_ID> \
     --group-id managedEnvironments \
     --connection-name my-connection
   ```

3. **Configure Private DNS Zone**
   ```bash
   # Create DNS zone
   az network private-dns zone create \
     --resource-group my-rg \
     --name privatelink.eastus.azurecontainerapps.io
   
   # Link to VNet
   az network private-dns link vnet create \
     --resource-group my-rg \
     --zone-name privatelink.eastus.azurecontainerapps.io \
     --name my-dns-link \
     --virtual-network my-existing-vnet \
     --registration-enabled false
   
   # Add A record
   az network private-dns record-set a add-record \
     --resource-group my-rg \
     --zone-name privatelink.eastus.azurecontainerapps.io \
     --record-set-name <ENVIRONMENT_DEFAULT_DOMAIN_PREFIX> \
     --ipv4-address <PRIVATE_ENDPOINT_IP>
   ```

4. **Access from VNet** - Apps in the VNet can now access via:
   - `https://my-app.<environment-id>.<region>.azurecontainerapps.io`
   - DNS resolves to private IP (e.g., 10.1.0.4)

### Advantages

- ✅ **Zero public exposure** - Ideal for sensitive workloads
- ✅ **Flexible** - Endpoint VNet separate from app VNet
- ✅ **Minimal footprint** - Small subnet requirement
- ✅ **Existing VNet** - Integrates with current infrastructure
- ✅ **Azure Front Door compatible** - Can use Private Link with AFD

### Disadvantages

- ❌ **Additional cost** - Private Link billing + Dedicated Plan Management charge
- ❌ **Complex DNS** - Requires proper DNS zone configuration
- ❌ **v2 only** - Not available for Consumption-only environments
- ❌ **Setup complexity** - More moving parts to configure

---

## VNet Integration (Option 2 - Second Best)

### Option 2A: Internal Environment with Custom VNet

Deploy Container Apps directly into your own VNet with internal-only access.

### Architecture

```
┌─────────────────────────────────────────────────────┐
│ Custom VNet (e.g., 10.0.0.0/16)                     │
│                                                      │
│  ┌──────────────────────────────────────┐          │
│  │ Container Apps Subnet                │          │
│  │ Workload Profiles: /27 or larger     │          │
│  │ Consumption-only: /23 or larger      │          │
│  │                                       │          │
│  │  ┌────────────────────────────┐      │          │
│  │  │ Container Apps Environment │      │          │
│  │  │ Accessibility: INTERNAL    │      │          │
│  │  │                            │      │          │
│  │  │ ┌──────┐  ┌──────┐        │      │          │
│  │  │ │ App1 │  │ App2 │        │      │          │
│  │  │ └──────┘  └──────┘        │      │          │
│  │  │ Internal IP: 10.0.0.10    │      │          │
│  │  └────────────────────────────┘      │          │
│  └──────────────────────────────────────┘          │
│                                                      │
│  ┌──────────────────────────────────────┐          │
│  │ Other Resources Subnet               │          │
│  │ • App Gateway, VMs, etc.             │          │
│  │ • Can access Container Apps          │          │
│  └──────────────────────────────────────┘          │
└─────────────────────────────────────────────────────┘
```

### Key Features

✅ **Private within VNet** - No internet exposure  
✅ **NSG support** - Network Security Groups for traffic control  
✅ **UDR support** - User Defined Routes (v2 only)  
✅ **NAT Gateway** - Custom egress (v2 only)  
✅ **Azure Firewall integration** - Control outbound traffic

### Subnet Sizing Guide

#### Workload Profiles (v2) - **Recommended**

| Subnet | Available IPs | Max Nodes (Dedicated) | Max Replicas (Consumption) |
|--------|---------------|----------------------|---------------------------|
| /27 | 18 | 9 | 90 |
| /26 | 50 | 25 | 250 |
| /25 | 114 | 57 | 570 |
| /24 | 242 | 121 | 1,210 |
| /23 | 498 | 249 | 2,490 |

> **Note**: 12 IPs reserved for infrastructure. Consider single revision mode deployments may temporarily double space requirements.

#### Consumption-only (v1) - Legacy

| Subnet | IPs Available | Notes |
|--------|---------------|-------|
| /23 | 512 | Minimum size; 60-256 IPs reserved |
| /22 | 1,024 | Better for scaling |

### Implementation Steps

```bash
# 1. Create VNet and subnet
az network vnet create \
  --resource-group my-rg \
  --name my-vnet \
  --address-prefix 10.0.0.0/16

az network vnet subnet create \
  --resource-group my-rg \
  --vnet-name my-vnet \
  --name aca-subnet \
  --address-prefixes 10.0.0.0/27 \
  --delegations Microsoft.App/environments  # v2 only

# 2. Create internal environment
az containerapp env create \
  --name my-environment \
  --resource-group my-rg \
  --location eastus \
  --internal-only true \
  --infrastructure-subnet-resource-id <SUBNET_ID>

# 3. Deploy container app
az containerapp create \
  --name my-app \
  --resource-group my-rg \
  --environment my-environment \
  --ingress internal \
  --target-port 8080
```

### Access Patterns

- **From VNet**: `https://my-app.internal.<env-id>.<region>.azurecontainerapps.io`
- **Cross-environment**: Apps in same environment can use name: `http://my-app`
- **Private DNS**: Configure Azure Private DNS for custom domains

### Advantages

- ✅ **Full VNet control** - NSGs, UDRs, Firewall integration
- ✅ **Predictable IPs** - Static IP allocation within subnet
- ✅ **No private endpoint cost** - Just standard consumption/dedicated charges
- ✅ **VNet peering** - Connect to other VNets

### Disadvantages

- ❌ **Larger subnet** - Requires dedicated /27 minimum (v2) or /23 (v1)
- ❌ **Fixed after creation** - Cannot change subnet size
- ❌ **Reserved IPs** - Significant IP allocation for infrastructure

---

### Option 2B: External Environment with Custom VNet

Same as 2A but allows public access (with optional IP restrictions).

```bash
az containerapp env create \
  --name my-environment \
  --resource-group my-rg \
  --location eastus \
  --internal-only false \  # External access
  --infrastructure-subnet-resource-id <SUBNET_ID>
```

**Use when**: You need both VNet integration AND public internet access (e.g., public API with database in VNet).

---

## Default Public Access (Option 3)

### Basic Public Environment

Azure automatically creates a managed VNet with limited networking features.

### Architecture

```
┌───────────────────────────────────────┐
│ Azure-Managed Network                 │
│ (Limited visibility/control)          │
│                                        │
│  ┌──────────────────────────────┐    │
│  │ Container Apps Environment   │    │
│  │ Accessibility: EXTERNAL      │    │
│  │                              │    │
│  │ ┌──────┐  ┌──────┐          │    │
│  │ │ App1 │  │ App2 │          │    │
│  │ └──────┘  └──────┘          │    │
│  │ Public IP: <generated>      │    │
│  └──────────────────────────────┘    │
└───────────────────────────────────────┘
         │
         │ Public Internet
         ▼
    [End Users]
```

### Features

- ✅ **Simple** - No VNet configuration needed
- ✅ **Fast setup** - Quickest deployment option
- ⚠️ **Public by default** - Exposed to internet

### Implementation

```bash
az containerapp env create \
  --name my-environment \
  --resource-group my-rg \
  --location eastus
  # No VNet parameters = default public
```

### When to Use

- ✅ Development/testing
- ✅ Public APIs with no sensitive data
- ✅ Proof of concepts
- ⚠️ NOT for production sensitive workloads

---

## Comparison Matrix

### Feature Availability

| Feature | Private Endpoint | VNet Internal | VNet External | Default Public |
|---------|------------------|---------------|---------------|----------------|
| **Zero Internet Exposure** | ✅ Yes | ✅ Yes | ❌ No | ❌ No |
| **Custom VNet** | Optional | ✅ Required | ✅ Required | ❌ No |
| **NSG Support** | ✅ Yes* | ✅ Yes | ✅ Yes | ❌ No |
| **UDR Support** | ✅ Yes* | ✅ Yes (v2) | ✅ Yes (v2) | ❌ No |
| **Azure Firewall** | ✅ Yes* | ✅ Yes (v2) | ✅ Yes (v2) | ❌ No |
| **Private DNS Required** | ✅ Yes | Recommended | Optional | ❌ No |
| **Public Endpoint** | ❌ Disabled | ❌ No | ✅ Yes | ✅ Yes |
| **VNet Peering** | ✅ Yes | ✅ Yes | ✅ Yes | ❌ No |
| **Minimum Subnet (v2)** | Any | /27 | /27 | N/A |
| **Setup Complexity** | High | Medium | Medium | Low |

*Via the VNet containing the private endpoint

### Security Comparison

| Pattern | Internet Exposure | NSG | Private DNS | Zero Trust |
|---------|-------------------|-----|-------------|------------|
| **Private Endpoint** | None | ✅ | ✅ Required | ✅ Compliant |
| **VNet Internal** | None | ✅ | Optional | ✅ Compliant |
| **VNet External** | Full | ✅ | Optional | ⚠️ Partial |
| **Default Public** | Full | ❌ | ❌ | ❌ No |

---

## Cost Considerations

### Private Endpoint Costs

When you enable a private endpoint, you incur **two separate charges**:

1. **Azure Private Link** - Standard Private Link pricing
   - Inbound data processing: ~$0.01/GB
   - Outbound data processing: ~$0.01/GB
   - Private endpoint per hour: ~$0.01/hour

2. **Azure Container Apps - Dedicated Plan Management**
   - Additional fixed monthly charge
   - Applies to BOTH Consumption and Dedicated plans
   - Same meter used for: Private Endpoints, Planned Maintenance
   - **Charges are additive** if multiple features enabled

> **Important**: Check current pricing at [Azure Container Apps Pricing](https://azure.microsoft.com/pricing/details/container-apps/)

### VNet Integration Costs

- **Subnet IPs**: No additional charge (part of VNet)
- **NAT Gateway**: If used for egress (~$0.045/hour + data processing)
- **Azure Firewall**: If used (~$1.25/hour + data processing)
- **Standard Container Apps consumption/dedicated charges apply**

### Cost Optimization Tips

✅ Use **VNet Internal** instead of Private Endpoint if you control the entire VNet  
✅ Use **Consumption workload profile** for variable workloads  
✅ Use **Dedicated profiles** for predictable, steady workloads  
✅ Right-size your subnet to avoid wasting IPs  

---

## Implementation Recommendations

### For Production Workloads

#### Priority 1: Private Endpoint (Best Security) ✅

**When to use:**
- Sensitive data or compliance requirements (PCI-DSS, HIPAA, etc.)
- Zero trust architecture
- Need to connect from existing hub VNet
- Want smallest possible subnet footprint
- Can justify additional cost

**Implementation:**
```bash
# 1. Create environment without custom VNet (or with if needed)
az containerapp env create \
  --name prod-env \
  --resource-group prod-rg \
  --location eastus \
  --public-network-access Disabled

# 2. Create private endpoint in existing VNet
az network private-endpoint create \
  --resource-group prod-rg \
  --name aca-private-endpoint \
  --vnet-name hub-vnet \
  --subnet private-endpoints-subnet \
  --private-connection-resource-id <ENV_ID> \
  --group-id managedEnvironments \
  --connection-name aca-connection

# 3. Configure private DNS
# ... (see detailed steps above)
```

#### Priority 2: VNet Integration - Internal (Good Balance) ⚖️

**When to use:**
- Need full VNet control (NSG, UDR, Firewall)
- Cost-sensitive projects
- Have available /27 or larger subnet
- Want predictable IP allocation

**Implementation:**
```bash
# Create subnet with delegation (v2)
az network vnet subnet create \
  --resource-group prod-rg \
  --vnet-name my-vnet \
  --name aca-subnet \
  --address-prefixes 10.0.1.0/27 \
  --delegations Microsoft.App/environments

# Create internal environment
az containerapp env create \
  --name prod-env \
  --resource-group prod-rg \
  --location eastus \
  --internal-only true \
  --infrastructure-subnet-resource-id <SUBNET_ID>
```

### For Development/Testing

**Use:** Default Public or VNet External
- Fast iteration
- Minimal cost
- Easy access for testing
- Can add IP restrictions for basic security

### Migration Path

1. **Start**: Default Public (dev/test)
2. **Grow**: VNet External with IP restrictions
3. **Secure**: VNet Internal
4. **Harden**: Private Endpoint

Each step increases security and complexity.

---

## Subnet Address Restrictions

### Reserved Ranges (Cannot Use)

All environment types prohibit these ranges:
- `169.254.0.0/16` - Azure internal
- `172.30.0.0/16` - AKS reserved
- `172.31.0.0/16` - AKS reserved
- `192.0.2.0/24` - Documentation/testing

### Workload Profiles Additional Restrictions

Also reserves:
- `100.100.0.0/17`
- `100.100.128.0/19`
- `100.100.160.0/19`
- `100.100.192.0/19`

---

## Additional Networking Features

### Ingress Configuration

- **External ingress**: Accepts traffic from internet + within environment
- **Internal ingress**: Only within environment
- **IP restrictions**: Allow/deny specific CIDR blocks
- **CORS**: Cross-origin resource sharing support
- **Session affinity**: Sticky sessions to same replica
- **Traffic splitting**: Blue/green deployments between revisions

### DNS Configuration

- **Custom domains**: Bring your own domain with certificates
- **Private DNS zones**: For internal name resolution
- **Apex domains**: Requires private DNS zone configuration
- **Wildcard domains**: Supported for environment default domain

### Integration Options

- **Azure Front Door**: Global load balancing with Private Link support
- **Application Gateway**: Regional load balancer with WAF
- **Azure Firewall**: Centralized egress control
- **Service Endpoints**: Direct Azure service access

---

## Summary

### Quick Decision Guide

**Choose Private Endpoint if:**
- Zero trust security required
- Connecting from existing hub VNet
- Can justify additional cost
- Smallest subnet footprint needed

**Choose VNet Internal if:**
- Need full networking control
- Cost-conscious
- Have /27+ subnet available
- Want no public exposure

**Choose VNet External if:**
- Need public access AND private resource access
- Require NSG/UDR features
- Mixed public/private architecture

**Choose Default Public if:**
- Development/testing only
- Non-sensitive workloads
- Speed over security

### Best Practice Architecture

For most production scenarios:

1. **Workload Profiles (v2) environment**
2. **Private Endpoint** in hub VNet OR **VNet Internal** deployment
3. **Internal ingress** for inter-app communication
4. **Azure Front Door** for public entry point (if needed)
5. **Azure Firewall** for egress control
6. **Private DNS zones** for name resolution
7. **NSGs** for defense in depth

This provides defense-in-depth with minimal public exposure while maintaining flexibility.

---

## References

- [Azure Container Apps Networking Overview](https://learn.microsoft.com/azure/container-apps/networking)
- [Virtual Network Configuration](https://learn.microsoft.com/azure/container-apps/custom-virtual-networks)
- [Private Endpoints with Azure Container Apps](https://learn.microsoft.com/azure/container-apps/how-to-use-private-endpoint)
- [Private Endpoints and DNS](https://learn.microsoft.com/azure/container-apps/private-endpoints-with-dns)
- [Azure Container Apps Billing](https://learn.microsoft.com/azure/container-apps/billing)
- [Workload Profiles Overview](https://learn.microsoft.com/azure/container-apps/workload-profiles-overview)

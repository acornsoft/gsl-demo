// Key Vault, Standard SKU, RBAC only. Holds no secrets at deploy time.
param location string
param tags object
param name string

resource kv 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    tenantId: tenant().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    // Purge protection is off so the demo can be torn down. Turn it on for anything real.
    // Private by default: the firewall denies everything except trusted Azure services. No IP or VNet rules;
    // nothing reads the vault over the network today (no Key Vault references, no vault code in the app).
    // publicNetworkAccess stays 'Enabled' so the firewall (and the trusted-services bypass) applies.
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'AzureServices'
      ipRules: []
      virtualNetworkRules: []
    }
  }
}

output name string = kv.name
output id string = kv.id

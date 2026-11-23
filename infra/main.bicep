// Krushi Prime – Azure infrastructure (free-tier first)
//   • App Service Plan F1 (Free, Linux) hosting API + React UI in ONE web app
//   • Azure SQL Database serverless with the lifetime FREE offer (100k vCore-sec + 32 GB / month)
//   • Application Insights (workspace-based, 5 GB/month free ingestion)
// Deploy:  az deployment group create -g <rg> -f infra/main.bicep -p infra/main.parameters.json -p sqlAdminPassword=... jwtKey=... superAdminPassword=...

@description('Short name used for resource names (lowercase letters/numbers).')
@maxLength(12)
param appName string = 'krushiprime'

@description('Azure region. Central India keeps latency low for Maharashtra.')
param location string = 'centralindia'

@allowed(['F1', 'B1', 'B2', 'S1', 'P0v3'])
@description('F1 = free (60 CPU-min/day, no custom domain, sleeps when idle). Use B1+ for paying customers.')
param appServiceSku string = 'F1'

@description('SQL administrator login')
param sqlAdminLogin string = 'kpadmin'

@secure()
@description('SQL administrator password (store in Key Vault / pipeline secret)')
param sqlAdminPassword string

@secure()
@description('JWT signing key – at least 32 random characters')
param jwtKey string

@secure()
@description('Initial password for the vendor super-admin (username: admin)')
param superAdminPassword string

@description('Seed the demo Krushi Seva Kendra (login 9999999999 / demo@123). Turn off for production.')
param seedDemoData bool = true

@allowed(['AutoPause', 'BillOverUsage'])
@description('What happens when the monthly free SQL limit is used up. AutoPause = never billed but DB pauses till next month. BillOverUsage = keeps running, pay only for the overage (recommended once shops bill daily).')
param sqlFreeLimitExhaustionBehavior string = 'AutoPause'

var suffix = uniqueString(resourceGroup().id)
var webName = '${appName}-${suffix}'
var sqlServerName = '${appName}-sql-${suffix}'
var dbName = 'krushiprime'

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${appName}-logs-${suffix}'
  location: location
  properties: { sku: { name: 'PerGB2018' }, retentionInDays: 30, workspaceCapping: { dailyQuotaGb: 1 } }
}

resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${appName}-ai-${suffix}'
  location: location
  kind: 'web'
  properties: { Application_Type: 'web', WorkspaceResourceId: logs.id, SamplingPercentage: 50 }
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// Lets Azure services (the web app) reach the server. For stricter setups use VNet integration + private endpoint (B1+).
resource allowAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: { startIpAddress: '0.0.0.0', endIpAddress: '0.0.0.0' }
}

resource db 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: dbName
  location: location
  sku: { name: 'GP_S_Gen5_2', tier: 'GeneralPurpose', family: 'Gen5', capacity: 2 }
  properties: {
    useFreeLimit: true
    freeLimitExhaustionBehavior: sqlFreeLimitExhaustionBehavior
    autoPauseDelay: 60
    minCapacity: json('0.5')
    maxSizeBytes: 34359738368 // 32 GB (free offer max)
    requestedBackupStorageRedundancy: 'Local'
    zoneRedundant: false
  }
}

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${appName}-plan-${suffix}'
  location: location
  kind: 'linux'
  sku: { name: appServiceSku }
  properties: { reserved: true }
}

resource web 'Microsoft.Web/sites@2023-12-01' = {
  name: webName
  location: location
  kind: 'app,linux'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: appServiceSku != 'F1'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      healthCheckPath: appServiceSku == 'F1' ? null : '/health'
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        { name: 'Database__Provider', value: 'SqlServer' }
        { name: 'Jwt__Key', value: jwtKey }
        { name: 'Jwt__Issuer', value: 'KrushiPrime' }
        { name: 'Jwt__Audience', value: 'KrushiPrime.Clients' }
        { name: 'Seed__DemoData', value: string(seedDemoData) }
        { name: 'Seed__SuperAdminUsername', value: 'admin' }
        { name: 'Seed__SuperAdminPassword', value: superAdminPassword }
        { name: 'Cors__Origins', value: 'https://${webName}.azurewebsites.net' }
        { name: 'WhatsApp__Mode', value: 'Link' }
        { name: 'App__PublicBaseUrl', value: 'https://${webName}.azurewebsites.net' }
        { name: 'Payments__EncryptionKey', value: jwtKey }
        { name: 'Payments__AllowSimulator', value: string(seedDemoData) }
        { name: 'Payments__Platform__Provider', value: 'Simulator' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: insights.properties.ConnectionString }
        { name: 'WEBSITE_RUN_FROM_PACKAGE', value: '1' }
      ]
      connectionStrings: [
        {
          name: 'Default'
          type: 'SQLAzure'
          connectionString: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${dbName};User ID=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;'
        }
      ]
    }
  }
}

output webAppName string = web.name
output url string = 'https://${web.properties.defaultHostName}'
output sqlServer string = sqlServer.properties.fullyQualifiedDomainName

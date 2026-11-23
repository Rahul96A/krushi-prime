// Krushi Prime – ₹0/month Azure hosting
//   • Azure Container Apps (Consumption): free 180k vCPU-s + 360k GiB-s + 2M requests per month, scales to zero when idle
//   • No container registry needed: stock Microsoft .NET 8 image runs the published app from an Azure Files share
//   • Azure SQL Database serverless with the lifetime FREE offer
//   • Log Analytics / App Insights capped inside the free 5 GB/month
// Deploy with scripts/deploy-azure-free.sh (Cloud Shell) – it builds nothing, just uploads the published app.

@maxLength(12)
param appName string = 'krushiprime'
param location string = 'centralindia'
param sqlAdminLogin string = 'kpadmin'
@secure()
param sqlAdminPassword string
@secure()
param jwtKey string
@secure()
param superAdminPassword string
param seedDemoData bool = true
@allowed(['AutoPause', 'BillOverUsage'])
param sqlFreeLimitExhaustionBehavior string = 'AutoPause'
@description('0.25 vCPU / 0.5 GiB keeps ~200 active hours/month inside the free grant')
param cpu string = '0.25'
param memory string = '0.5Gi'

var suffix = uniqueString(resourceGroup().id)
var dbName = 'krushiprime'

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${appName}-logs-${suffix}'
  location: location
  properties: { sku: { name: 'PerGB2018' }, retentionInDays: 30, workspaceCapping: { dailyQuotaGb: json('0.15') } }
}

resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${appName}-ai-${suffix}'
  location: location
  kind: 'web'
  properties: { Application_Type: 'web', WorkspaceResourceId: logs.id, SamplingPercentage: 25 }
}

// ── Published app lives on an Azure Files share (pennies per month; replaces a paid container registry) ──
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: 'kpst${suffix}'
  location: location
  kind: 'StorageV2'
  sku: { name: 'Standard_LRS' }
  properties: { minimumTlsVersion: 'TLS1_2', allowBlobPublicAccess: false, supportsHttpsTrafficOnly: true }
}
resource fileService 'Microsoft.Storage/storageAccounts/fileServices@2023-05-01' = { parent: storage, name: 'default' }
resource share 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  parent: fileService
  name: 'app'
  properties: { shareQuota: 1 }
}

// ── Azure SQL (free offer) ──
resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: '${appName}-sql-${suffix}'
  location: location
  properties: { administratorLogin: sqlAdminLogin, administratorLoginPassword: sqlAdminPassword, minimalTlsVersion: '1.2', publicNetworkAccess: 'Enabled' }
}
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
    maxSizeBytes: 34359738368
    requestedBackupStorageRedundancy: 'Local'
    zoneRedundant: false
  }
}

// ── Container Apps (Consumption) ──
resource env 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${appName}-env-${suffix}'
  location: location
  properties: {
    appLogsConfiguration: { destination: 'log-analytics', logAnalyticsConfiguration: { customerId: logs.properties.customerId, sharedKey: logs.listKeys().primarySharedKey } }
    workloadProfiles: [ { name: 'Consumption', workloadProfileType: 'Consumption' } ]
  }
}

resource envStorage 'Microsoft.App/managedEnvironments/storages@2024-03-01' = {
  parent: env
  name: 'appfiles'
  properties: {
    azureFile: { accountName: storage.name, accountKey: storage.listKeys().keys[0].value, shareName: share.name, accessMode: 'ReadOnly' }
  }
}

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${appName}-app'
  location: location
  dependsOn: [ envStorage ]
  properties: {
    environmentId: env.id
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: { external: true, targetPort: 8080, transport: 'auto', allowInsecure: false }
      secrets: [
        { name: 'jwt-key', value: jwtKey }
        { name: 'admin-pwd', value: superAdminPassword }
        { name: 'sql-conn', value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${dbName};User ID=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;' }
        { name: 'ai-conn', value: insights.properties.ConnectionString }
      ]
    }
    template: {
      containers: [
        {
          name: 'krushiprime'
          image: 'mcr.microsoft.com/dotnet/aspnet:8.0'
          command: [ 'dotnet' ]
          args: [ '/app/KrushiPrime.Api.dll', '--contentRoot', '/app' ]
          resources: { cpu: json(cpu), memory: memory }
          volumeMounts: [ { volumeName: 'appfiles', mountPath: '/app' } ]
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'ASPNETCORE_HTTP_PORTS', value: '8080' }
            { name: 'Database__Provider', value: 'SqlServer' }
            { name: 'ConnectionStrings__Default', secretRef: 'sql-conn' }
            { name: 'Jwt__Key', secretRef: 'jwt-key' }
            { name: 'Payments__EncryptionKey', secretRef: 'jwt-key' }
            { name: 'Seed__SuperAdminPassword', secretRef: 'admin-pwd' }
            { name: 'Seed__DemoData', value: string(seedDemoData) }
            { name: 'Payments__AllowSimulator', value: string(seedDemoData) }
            { name: 'Payments__Platform__Provider', value: 'Simulator' }
            { name: 'WhatsApp__Mode', value: 'Link' }
            { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', secretRef: 'ai-conn' }
            { name: 'DOTNET_gcServer', value: '0' } // smaller memory footprint on 0.5 GiB
          ]
          probes: [
            // First start migrates the DB and the free SQL tier may be resuming: allow up to 5 minutes
            { type: 'Startup', tcpSocket: { port: 8080 }, initialDelaySeconds: 5, periodSeconds: 30, failureThreshold: 10, timeoutSeconds: 5 }
            { type: 'Liveness', httpGet: { path: '/health', port: 8080 }, periodSeconds: 60, failureThreshold: 3, timeoutSeconds: 10 }
          ]
        }
      ]
      volumes: [ { name: 'appfiles', storageType: 'AzureFile', storageName: 'appfiles' } ]
      scale: {
        minReplicas: 0   // scale to zero when idle → stays within the free grant
        maxReplicas: 1
        rules: [ { name: 'http', http: { metadata: { concurrentRequests: '50' } } } ]
      }
    }
  }
}

output url string = 'https://${app.properties.configuration.ingress.fqdn}'
output containerAppName string = app.name
output storageAccount string = storage.name
output shareName string = share.name
output sqlServer string = sqlServer.properties.fullyQualifiedDomainName

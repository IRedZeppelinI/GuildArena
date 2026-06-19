# Resource Group
resource "azurerm_resource_group" "rg" {
  name     = var.resource_group_name
  location = var.location
  tags     = var.common_tags
}

# Sufixo aleatório para  Storage Account 
resource "random_string" "storage_suffix" {
  length  = 6
  special = false 
  upper   = false 
  numeric = true  
}

# Storage Account
resource "azurerm_storage_account" "storage" {
  name                     = "${var.storage_account_prefix}${random_string.storage_suffix.result}"
  resource_group_name      = azurerm_resource_group.rg.name
  location                 = azurerm_resource_group.rg.location 
  
  account_tier             = "Standard"
  account_replication_type = "LRS"

  access_tier                   = "Hot"
  min_tls_version               = "TLS1_2"
  is_hns_enabled                = false
  
  # Permite que os containers possam ser configurados com acesso público
  allow_nested_items_to_be_public = true

  tags = var.common_tags
}

# Criação dos Containers (Iterando sobre a variável blob_containers)
resource "azurerm_storage_container" "containers" {
  for_each = var.blob_containers

  name                  = each.key
  storage_account_name  = azurerm_storage_account.storage.name
  
  # Define o acesso como "Blob (anonymous read access for blobs only)"
  container_access_type = "blob" 
}

# ---------------------------------------------------------------------------
# INFRAESTRUTURA DA API (Azure Container Apps)
# ---------------------------------------------------------------------------

# 1. Log Analytics Workspace
resource "azurerm_log_analytics_workspace" "law" {
  name                = "law-guildarena"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = var.common_tags
}

# 2. Container App Environment
resource "azurerm_container_app_environment" "env" {
  name                       = "cae-guildarena"
  location                   = azurerm_resource_group.rg.location
  resource_group_name        = azurerm_resource_group.rg.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.law.id
  tags                       = var.common_tags
}

# 3. Container App - API (Backend)
resource "azurerm_container_app" "api" {
  name                         = "ca-guildarena-api"
  container_app_environment_id = azurerm_container_app_environment.env.id
  resource_group_name          = azurerm_resource_group.rg.name
  revision_mode                = "Single"
  tags                         = var.common_tags

  template {
    container {
      name   = "api"
      image  = "mcr.microsoft.com/azuredocs/containerapps-helloworld:latest" # Imagem temporária
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name        = "ConnectionStrings__AzureBlobStorage"
        secret_name = "storage-connection-string"
      }
      env {
        name        = "ConnectionStrings__DefaultConnection" 
        secret_name = "neon-connection-string"
      }
      env {
        name        = "ConnectionStrings__Redis" 
        secret_name = "upstash-redis-string"
      }
    }
  }

  secret {
    name  = "storage-connection-string"
    value = azurerm_storage_account.storage.primary_connection_string
  }
  secret {
    name  = "neon-connection-string"
    value = var.neon_connection_string
  }
  secret {
    name  = "upstash-redis-string"
    value = var.upstash_redis_string
  }

  ingress {
    allow_insecure_connections = false
    external_enabled           = true
    target_port                = 8080 
    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }
}

# ---------------------------------------------------------------------------
# INFRAESTRUTURA DO BLAZOR WASM (Azure Static Web Apps )
# ---------------------------------------------------------------------------

resource "azurerm_static_web_app" "blazor" {
  name                = "swa-guildarena-blazor"
  resource_group_name = azurerm_resource_group.rg.name
  # Nota: Static Web Apps tem regiões limitadas. WestEurope funciona perfeitamente.
  location            = azurerm_resource_group.rg.location 
  sku_tier            = "Free"
  sku_size            = "Free"
  tags                = var.common_tags
}
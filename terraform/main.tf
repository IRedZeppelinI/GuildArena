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
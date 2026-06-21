output "resource_group_name" {
  description = "O nome do Resource Group criado."
  value       = azurerm_resource_group.rg.name
}

output "storage_account_name" {
  description = "O nome da Storage Account gerado."
  value       = azurerm_storage_account.storage.name
}

output "storage_account_blob_endpoint" {
  description = "O URL base público para aceder aos blobs."
  value       = azurerm_storage_account.storage.primary_blob_endpoint
}

output "created_containers" {
  description = "Lista dos containers criados."
  value       = [for container in azurerm_storage_container.containers : container.name]
}

output "storage_account_connection_string" {
  description = "A Connection String principal para a Storage Account."
  value       = azurerm_storage_account.storage.primary_connection_string
  sensitive   = true 
}

# --- ACA and Static Web App ---

output "container_app_api_name" {
  description = "Nome da Container App da API"
  value       = azurerm_container_app.api.name
}

output "api_url" {
  description = "O URL público gerado para a API"
  value       = "https://${azurerm_container_app.api.ingress[0].fqdn}"
}

output "blazor_url" {
  description = "O URL público gerado para Static Web App"
  value       = "https://${azurerm_static_web_app.blazor.default_host_name}"
}


output "swa_deployment_token" {
  description = "O token necessário para o GitHub Actions fazer deploy no Static Web App"
  value       = azurerm_static_web_app.blazor.api_key
  sensitive   = true 
}
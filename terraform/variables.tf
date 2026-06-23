variable "resource_group_name" {
  description = "O nome do Resource Group."
  type        = string
  default     = "rg-guild-arena"
}

variable "location" {
  description = "A região dos recursos."
  type        = string
  default     = "WestEurope"
}

variable "common_tags" {
  description = "Tags a aplicar a todos os recursos."
  type        = map(string)
  default = {
    "Project"   = "GuildArena"
    "CreatedBy" = "Terraform"
  }
}

variable "storage_account_prefix" {
  description = "Prefixo para a Storage Account."
  type        = string
  default     = "stguildarena"
}

variable "blob_containers" {
  description = "Lista dos containers a serem criados no Blob Storage."
  type        = set(string)
  default     = [
    "backgrounds",
    "portraits",
    "abilities",
    "modifiers"
  ]
}

variable "neon_connection_string" {
  description = "A Connection String da base de dados PostgreSQL no Neon.tech"
  type        = string
  sensitive   = true
}

variable "upstash_redis_string" {
  description = "A Connection String do Redis no Upstash"
  type        = string
  sensitive   = true
}

# Conta Admin
variable "admin_email" {
  description = "Email do Administrador de Produção"
  type        = string
  sensitive   = true
}

variable "admin_password" {
  description = "Password do Administrador de Produção"
  type        = string
  sensitive   = true
}
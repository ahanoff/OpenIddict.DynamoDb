variable "applications_table_name" {
  description = "Name of the DynamoDB table for OpenIddict applications"
  type        = string
  default     = "OpenIddictApplications"
}

variable "applications_billing_mode" {
  description = "Billing mode for the applications table"
  type        = string
  default     = "PAY_PER_REQUEST"
}

variable "applications_read_capacity" {
  description = "Read capacity for the applications table (PROVISIONED mode only)"
  type        = number
  default     = 5
}

variable "applications_write_capacity" {
  description = "Write capacity for the applications table (PROVISIONED mode only)"
  type        = number
  default     = 5
}

variable "authorizations_table_name" {
  description = "Name of the DynamoDB table for OpenIddict authorizations"
  type        = string
  default     = "OpenIddictAuthorizations"
}

variable "authorizations_billing_mode" {
  description = "Billing mode for the authorizations table"
  type        = string
  default     = "PAY_PER_REQUEST"
}

variable "authorizations_read_capacity" {
  description = "Read capacity for the authorizations table (PROVISIONED mode only)"
  type        = number
  default     = 5
}

variable "authorizations_write_capacity" {
  description = "Write capacity for the authorizations table (PROVISIONED mode only)"
  type        = number
  default     = 5
}

variable "scopes_table_name" {
  description = "Name of the DynamoDB table for OpenIddict scopes"
  type        = string
  default     = "OpenIddictScopes"
}

variable "scopes_billing_mode" {
  description = "Billing mode for the scopes table"
  type        = string
  default     = "PAY_PER_REQUEST"
}

variable "scopes_read_capacity" {
  description = "Read capacity for the scopes table (PROVISIONED mode only)"
  type        = number
  default     = 5
}

variable "scopes_write_capacity" {
  description = "Write capacity for the scopes table (PROVISIONED mode only)"
  type        = number
  default     = 5
}

variable "tokens_table_name" {
  description = "Name of the DynamoDB table for OpenIddict tokens"
  type        = string
  default     = "OpenIddictTokens"
}

variable "tokens_billing_mode" {
  description = "Billing mode for the tokens table"
  type        = string
  default     = "PAY_PER_REQUEST"
}

variable "tokens_read_capacity" {
  description = "Read capacity for the tokens table (PROVISIONED mode only)"
  type        = number
  default     = 5
}

variable "tokens_write_capacity" {
  description = "Write capacity for the tokens table (PROVISIONED mode only)"
  type        = number
  default     = 5
}

variable "tags" {
  description = "Tags to apply to all resources"
  type        = map(string)
  default     = {}
}

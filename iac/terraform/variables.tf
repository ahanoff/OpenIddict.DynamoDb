variable "applications_table_name" {
  description = "Name of the DynamoDB table for OpenIddict applications"
  type        = string
  default     = "OpenIddictApplications"
}

variable "applications_billing_mode" {
  description = "Billing mode for the applications table"
  type        = string
  default     = "PAY_PER_REQUEST"

  validation {
    condition     = contains(["PAY_PER_REQUEST", "PROVISIONED"], var.applications_billing_mode)
    error_message = "applications_billing_mode must be PAY_PER_REQUEST or PROVISIONED."
  }
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

  validation {
    condition     = contains(["PAY_PER_REQUEST", "PROVISIONED"], var.authorizations_billing_mode)
    error_message = "authorizations_billing_mode must be PAY_PER_REQUEST or PROVISIONED."
  }
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

variable "authorizations_gsi_read_capacity" {
  description = "Read capacity for each authorization GSI (PROVISIONED mode only)"
  type        = number
  default     = 5
}

variable "authorizations_gsi_write_capacity" {
  description = "Write capacity for each authorization GSI (PROVISIONED mode only)"
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

  validation {
    condition     = contains(["PAY_PER_REQUEST", "PROVISIONED"], var.scopes_billing_mode)
    error_message = "scopes_billing_mode must be PAY_PER_REQUEST or PROVISIONED."
  }
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

  validation {
    condition     = contains(["PAY_PER_REQUEST", "PROVISIONED"], var.tokens_billing_mode)
    error_message = "tokens_billing_mode must be PAY_PER_REQUEST or PROVISIONED."
  }
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

variable "tokens_gsi_read_capacity" {
  description = "Read capacity for each token GSI (PROVISIONED mode only)"
  type        = number
  default     = 5
}

variable "tokens_gsi_write_capacity" {
  description = "Write capacity for each token GSI (PROVISIONED mode only)"
  type        = number
  default     = 5
}

variable "point_in_time_recovery_enabled" {
  description = "Enable point-in-time recovery for all tables"
  type        = bool
  default     = true
}

variable "deletion_protection_enabled" {
  description = "Enable deletion protection for all tables"
  type        = bool
  default     = true
}

variable "kms_key_arn" {
  description = "Optional customer-managed KMS key ARN. Null uses the AWS-owned DynamoDB key"
  type        = string
  default     = null
}

variable "tags" {
  description = "Tags to apply to all resources"
  type        = map(string)
  default     = {}
}

variable "applications_table_name" {
  description = "Name of the DynamoDB table for OpenIddict applications"
  type        = string
  default     = "OpenIddictApplications"
}

variable "authorizations_table_name" {
  description = "Name of the DynamoDB table for OpenIddict authorizations"
  type        = string
  default     = "OpenIddictAuthorizations"
}

variable "scopes_table_name" {
  description = "Name of the DynamoDB table for OpenIddict scopes"
  type        = string
  default     = "OpenIddictScopes"
}

variable "tokens_table_name" {
  description = "Name of the DynamoDB table for OpenIddict tokens"
  type        = string
  default     = "OpenIddictTokens"
}

variable "tags" {
  description = "Tags to apply to all resources"
  type        = map(string)
  default     = {}
}

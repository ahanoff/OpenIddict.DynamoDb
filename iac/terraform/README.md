# terraform-aws-openiddict-dynamodb

Terraform module that creates the DynamoDB tables for [OpenIddict.DynamoDb](https://github.com/ahanoff/OpenIddict.DynamoDb).

## Usage

```hcl
module "openiddict" {
  source = "github.com/ahanoff/OpenIddict.DynamoDb//iac/terraform"

  tags = {
    Environment = "production"
  }
}
```

## Inputs

| Name | Description | Type | Default |
|------|-------------|------|---------|
| `applications_table_name` | DynamoDB table name for applications | `string` | `OpenIddictApplications` |
| `authorizations_table_name` | DynamoDB table name for authorizations | `string` | `OpenIddictAuthorizations` |
| `scopes_table_name` | DynamoDB table name for scopes | `string` | `OpenIddictScopes` |
| `tokens_table_name` | DynamoDB table name for tokens | `string` | `OpenIddictTokens` |
| `tags` | Tags applied to all resources | `map(string)` | `{}` |

## Outputs

| Name | Description |
|------|-------------|
| `applications_table_name` | Applications table name |
| `applications_table_arn` | Applications table ARN |
| `authorizations_table_name` | Authorizations table name |
| `authorizations_table_arn` | Authorizations table ARN |
| `scopes_table_name` | Scopes table name |
| `scopes_table_arn` | Scopes table ARN |
| `tokens_table_name` | Tokens table name |
| `tokens_table_arn` | Tokens table ARN |

# terraform-aws-openiddict-dynamodb

Terraform module that creates the DynamoDB tables for [OpenIddict.DynamoDb](https://github.com/ahanoff/OpenIddict.DynamoDb).

## Usage

### Default settings

All tables use on-demand billing (`PAY_PER_REQUEST`):

```hcl
module "openiddict" {
  source = "github.com/ahanoff/OpenIddict.DynamoDb//iac/terraform"
}
```

### Custom table names and tags

```hcl
module "openiddict" {
  source = "github.com/ahanoff/OpenIddict.DynamoDb//iac/terraform"

  applications_table_name = "my-app-OpenIddictApplications"
  tokens_table_name       = "my-app-OpenIddictTokens"

  tags = {
    Environment = "production"
    Project     = "my-app"
  }
}
```

### Provisioned billing for specific tables

```hcl
module "openiddict" {
  source = "github.com/ahanoff/OpenIddict.DynamoDb//iac/terraform"

  tokens_billing_mode   = "PROVISIONED"
  tokens_read_capacity  = 20
  tokens_write_capacity = 10
}
```

## Inputs

| Name | Description | Type | Default |
|------|-------------|------|---------|
| `applications_table_name` | Table name for applications | `string` | `OpenIddictApplications` |
| `applications_billing_mode` | Billing mode (`PAY_PER_REQUEST` or `PROVISIONED`) | `string` | `PAY_PER_REQUEST` |
| `applications_read_capacity` | Read capacity (provisioned only) | `number` | `5` |
| `applications_write_capacity` | Write capacity (provisioned only) | `number` | `5` |
| `authorizations_table_name` | Table name for authorizations | `string` | `OpenIddictAuthorizations` |
| `authorizations_billing_mode` | Billing mode | `string` | `PAY_PER_REQUEST` |
| `authorizations_read_capacity` | Read capacity (provisioned only) | `number` | `5` |
| `authorizations_write_capacity` | Write capacity (provisioned only) | `number` | `5` |
| `scopes_table_name` | Table name for scopes | `string` | `OpenIddictScopes` |
| `scopes_billing_mode` | Billing mode | `string` | `PAY_PER_REQUEST` |
| `scopes_read_capacity` | Read capacity (provisioned only) | `number` | `5` |
| `scopes_write_capacity` | Write capacity (provisioned only) | `number` | `5` |
| `tokens_table_name` | Table name for tokens | `string` | `OpenIddictTokens` |
| `tokens_billing_mode` | Billing mode | `string` | `PAY_PER_REQUEST` |
| `tokens_read_capacity` | Read capacity (provisioned only) | `number` | `5` |
| `tokens_write_capacity` | Write capacity (provisioned only) | `number` | `5` |
| `tags` | Tags applied to all tables | `map(string)` | `{}` |

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

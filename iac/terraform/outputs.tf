output "applications_table_name" {
  value = aws_dynamodb_table.applications.name
}

output "applications_table_arn" {
  value = aws_dynamodb_table.applications.arn
}

output "authorizations_table_name" {
  value = aws_dynamodb_table.authorizations.name
}

output "authorizations_table_arn" {
  value = aws_dynamodb_table.authorizations.arn
}

output "scopes_table_name" {
  value = aws_dynamodb_table.scopes.name
}

output "scopes_table_arn" {
  value = aws_dynamodb_table.scopes.arn
}

output "tokens_table_name" {
  value = aws_dynamodb_table.tokens.name
}

output "tokens_table_arn" {
  value = aws_dynamodb_table.tokens.arn
}

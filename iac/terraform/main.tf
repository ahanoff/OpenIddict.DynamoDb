terraform {
  required_version = ">= 1.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = ">= 5.0"
    }
  }
}

resource "aws_dynamodb_table" "applications" {
  name         = var.applications_table_name
  billing_mode = var.applications_billing_mode
  hash_key     = "pk"
  range_key    = "sk"

  attribute {
    name = "pk"
    type = "S"
  }

  attribute {
    name = "sk"
    type = "S"
  }

  dynamic "read_capacity" {
    for_each = var.applications_billing_mode == "PROVISIONED" ? [1] : []
    content {}
  }

  write_capacity = var.applications_billing_mode == "PROVISIONED" ? var.applications_write_capacity : null
  read_capacity  = var.applications_billing_mode == "PROVISIONED" ? var.applications_read_capacity : null

  tags = var.tags
}

resource "aws_dynamodb_table" "authorizations" {
  name         = var.authorizations_table_name
  billing_mode = var.authorizations_billing_mode
  hash_key     = "pk"
  range_key    = "sk"

  attribute {
    name = "pk"
    type = "S"
  }

  attribute {
    name = "sk"
    type = "S"
  }

  attribute {
    name = "gsi1_pk"
    type = "S"
  }

  attribute {
    name = "gsi1_sk"
    type = "S"
  }

  attribute {
    name = "gsi2_pk"
    type = "S"
  }

  attribute {
    name = "gsi2_sk"
    type = "S"
  }

  global_secondary_index {
    name            = "SubjectIndex"
    hash_key        = "gsi1_pk"
    range_key       = "gsi1_sk"
    projection_type = "ALL"
  }

  global_secondary_index {
    name            = "ApplicationIndex"
    hash_key        = "gsi2_pk"
    range_key       = "gsi2_sk"
    projection_type = "ALL"
  }

  write_capacity = var.authorizations_billing_mode == "PROVISIONED" ? var.authorizations_write_capacity : null
  read_capacity  = var.authorizations_billing_mode == "PROVISIONED" ? var.authorizations_read_capacity : null

  tags = var.tags
}

resource "aws_dynamodb_table" "scopes" {
  name         = var.scopes_table_name
  billing_mode = var.scopes_billing_mode
  hash_key     = "pk"
  range_key    = "sk"

  attribute {
    name = "pk"
    type = "S"
  }

  attribute {
    name = "sk"
    type = "S"
  }

  write_capacity = var.scopes_billing_mode == "PROVISIONED" ? var.scopes_write_capacity : null
  read_capacity  = var.scopes_billing_mode == "PROVISIONED" ? var.scopes_read_capacity : null

  tags = var.tags
}

resource "aws_dynamodb_table" "tokens" {
  name         = var.tokens_table_name
  billing_mode = var.tokens_billing_mode
  hash_key     = "pk"
  range_key    = "sk"

  attribute {
    name = "pk"
    type = "S"
  }

  attribute {
    name = "sk"
    type = "S"
  }

  attribute {
    name = "gsi1_pk"
    type = "S"
  }

  attribute {
    name = "gsi1_sk"
    type = "S"
  }

  attribute {
    name = "gsi2_pk"
    type = "S"
  }

  attribute {
    name = "gsi2_sk"
    type = "S"
  }

  attribute {
    name = "gsi3_pk"
    type = "S"
  }

  attribute {
    name = "gsi3_sk"
    type = "S"
  }

  attribute {
    name = "gsi4_pk"
    type = "S"
  }

  attribute {
    name = "gsi4_sk"
    type = "S"
  }

  global_secondary_index {
    name            = "SubjectAppIndex"
    hash_key        = "gsi1_pk"
    range_key       = "gsi1_sk"
    projection_type = "ALL"
  }

  global_secondary_index {
    name            = "SubjectIndex"
    hash_key        = "gsi2_pk"
    range_key       = "gsi2_sk"
    projection_type = "ALL"
  }

  global_secondary_index {
    name            = "ApplicationShardedIndex"
    hash_key        = "gsi3_pk"
    range_key       = "gsi3_sk"
    projection_type = "ALL"
  }

  global_secondary_index {
    name            = "AuthorizationIndex"
    hash_key        = "gsi4_pk"
    range_key       = "gsi4_sk"
    projection_type = "ALL"
  }

  ttl {
    attribute_name = "ttl"
    enabled        = true
  }

  write_capacity = var.tokens_billing_mode == "PROVISIONED" ? var.tokens_write_capacity : null
  read_capacity  = var.tokens_billing_mode == "PROVISIONED" ? var.tokens_read_capacity : null

  tags = var.tags
}

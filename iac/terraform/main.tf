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
  billing_mode = "PAY_PER_REQUEST"
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

  tags = var.tags
}

resource "aws_dynamodb_table" "authorizations" {
  name         = var.authorizations_table_name
  billing_mode = "PAY_PER_REQUEST"
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

  tags = var.tags
}

resource "aws_dynamodb_table" "scopes" {
  name         = var.scopes_table_name
  billing_mode = "PAY_PER_REQUEST"
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

  tags = var.tags
}

resource "aws_dynamodb_table" "tokens" {
  name         = var.tokens_table_name
  billing_mode = "PAY_PER_REQUEST"
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

  tags = var.tags
}

import * as aws from "@pulumi/aws";
import * as pulumi from "@pulumi/pulumi";

interface TableConfig {
  billingMode?: pulumi.Input<string>;
  readCapacity?: pulumi.Input<number>;
  writeCapacity?: pulumi.Input<number>;
}

export interface OpenIddictDynamoDbArgs {
  applicationsTableName?: string;
  authorizationsTableName?: string;
  scopesTableName?: string;
  tokensTableName?: string;
  tags?: Record<string, string>;
  tableConfig?: TableConfig;
  applicationsTableConfig?: TableConfig;
  authorizationsTableConfig?: TableConfig;
  scopesTableConfig?: TableConfig;
  tokensTableConfig?: TableConfig;
}

function resolveConfig(base: TableConfig, override: TableConfig | undefined): {
  billingMode: pulumi.Input<string>;
  readCapacity?: pulumi.Input<number>;
  writeCapacity?: pulumi.Input<number>;
} {
  const merged = { ...base, ...override };
  return {
    billingMode: merged.billingMode ?? "PAY_PER_REQUEST",
    ...(merged.billingMode === "PROVISIONED" || merged.readCapacity ? { readCapacity: merged.readCapacity ?? 5 } : {}),
    ...(merged.billingMode === "PROVISIONED" || merged.writeCapacity ? { writeCapacity: merged.writeCapacity ?? 5 } : {}),
  };
}

export class OpenIddictDynamoDb extends pulumi.ComponentResource {
  public readonly applicationsTable: aws.dynamodb.Table;
  public readonly authorizationsTable: aws.dynamodb.Table;
  public readonly scopesTable: aws.dynamodb.Table;
  public readonly tokensTable: aws.dynamodb.Table;

  constructor(
    name: string,
    args?: OpenIddictDynamoDbArgs,
    opts?: pulumi.ComponentResourceOptions,
  ) {
    super("ahanoff:openiddict:DynamoDb", name, {}, opts);

    const defaultOpts = { parent: this };
    const tags = args?.tags ?? {};
    const baseConfig = args?.tableConfig ?? {};

    this.applicationsTable = new aws.dynamodb.Table(
      `${name}-applications`,
      {
        name: args?.applicationsTableName ?? "OpenIddictApplications",
        hashKey: "pk",
        rangeKey: "sk",
        attributes: [
          { name: "pk", type: "S" },
          { name: "sk", type: "S" },
        ],
        ...resolveConfig(baseConfig, args?.applicationsTableConfig),
        tags,
      },
      defaultOpts,
    );

    this.scopesTable = new aws.dynamodb.Table(
      `${name}-scopes`,
      {
        name: args?.scopesTableName ?? "OpenIddictScopes",
        hashKey: "pk",
        rangeKey: "sk",
        attributes: [
          { name: "pk", type: "S" },
          { name: "sk", type: "S" },
        ],
        ...resolveConfig(baseConfig, args?.scopesTableConfig),
        tags,
      },
      defaultOpts,
    );

    this.authorizationsTable = new aws.dynamodb.Table(
      `${name}-authorizations`,
      {
        name: args?.authorizationsTableName ?? "OpenIddictAuthorizations",
        hashKey: "pk",
        rangeKey: "sk",
        attributes: [
          { name: "pk", type: "S" },
          { name: "sk", type: "S" },
          { name: "gsi1_pk", type: "S" },
          { name: "gsi1_sk", type: "S" },
          { name: "gsi2_pk", type: "S" },
          { name: "gsi2_sk", type: "S" },
        ],
        globalSecondaryIndexes: [
          {
            name: "SubjectIndex",
            hashKey: "gsi1_pk",
            rangeKey: "gsi1_sk",
            projectionType: "ALL",
          },
          {
            name: "ApplicationIndex",
            hashKey: "gsi2_pk",
            rangeKey: "gsi2_sk",
            projectionType: "ALL",
          },
        ],
        ...resolveConfig(baseConfig, args?.authorizationsTableConfig),
        tags,
      },
      defaultOpts,
    );

    this.tokensTable = new aws.dynamodb.Table(
      `${name}-tokens`,
      {
        name: args?.tokensTableName ?? "OpenIddictTokens",
        hashKey: "pk",
        rangeKey: "sk",
        attributes: [
          { name: "pk", type: "S" },
          { name: "sk", type: "S" },
          { name: "gsi1_pk", type: "S" },
          { name: "gsi1_sk", type: "S" },
          { name: "gsi2_pk", type: "S" },
          { name: "gsi2_sk", type: "S" },
          { name: "gsi3_pk", type: "S" },
          { name: "gsi3_sk", type: "S" },
          { name: "gsi4_pk", type: "S" },
          { name: "gsi4_sk", type: "S" },
        ],
        globalSecondaryIndexes: [
          {
            name: "SubjectAppIndex",
            hashKey: "gsi1_pk",
            rangeKey: "gsi1_sk",
            projectionType: "ALL",
          },
          {
            name: "SubjectIndex",
            hashKey: "gsi2_pk",
            rangeKey: "gsi2_sk",
            projectionType: "ALL",
          },
          {
            name: "ApplicationShardedIndex",
            hashKey: "gsi3_pk",
            rangeKey: "gsi3_sk",
            projectionType: "ALL",
          },
          {
            name: "AuthorizationIndex",
            hashKey: "gsi4_pk",
            rangeKey: "gsi4_sk",
            projectionType: "ALL",
          },
        ],
        ttl: {
          attributeName: "ttl",
          enabled: true,
        },
        ...resolveConfig(baseConfig, args?.tokensTableConfig),
        tags,
      },
      defaultOpts,
    );

    this.registerOutputs();
  }
}

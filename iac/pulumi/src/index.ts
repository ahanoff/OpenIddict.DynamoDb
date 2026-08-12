import * as aws from "@pulumi/aws";
import * as pulumi from "@pulumi/pulumi";

type TableArgs = Omit<aws.dynamodb.TableArgs, "hashKey" | "rangeKey" | "attributes" | "globalSecondaryIndexes" | "ttl" | "tags">;

export interface OpenIddictDynamoDbArgs {
  readonly tags?: Readonly<Record<string, string>>;
  readonly tableArgs?: TableArgs;
  readonly applicationsTableArgs?: TableArgs;
  readonly authorizationsTableArgs?: TableArgs;
  readonly scopesTableArgs?: TableArgs;
  readonly tokensTableArgs?: TableArgs;
  readonly globalSecondaryIndexReadCapacity?: pulumi.Input<number>;
  readonly globalSecondaryIndexWriteCapacity?: pulumi.Input<number>;
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
    const base = {
      billingMode: "PAY_PER_REQUEST" as const,
      deletionProtectionEnabled: true,
      pointInTimeRecovery: { enabled: true },
      serverSideEncryption: { enabled: true },
      ...args?.tableArgs,
    };
    const sharedBillingMode = args?.tableArgs?.billingMode ?? "PAY_PER_REQUEST";
    const authorizationBillingMode = args?.authorizationsTableArgs?.billingMode ?? sharedBillingMode;
    const tokenBillingMode = args?.tokensTableArgs?.billingMode ?? sharedBillingMode;
    const createGlobalSecondaryIndexes = (
      billingMode: pulumi.Input<string | undefined>,
      indexes: ReadonlyArray<{
        readonly name: string;
        readonly hashKey: string;
        readonly rangeKey: string;
        readonly projectionType: string;
      }>,
    ) => pulumi.all([
      billingMode,
      args?.globalSecondaryIndexReadCapacity ?? 5,
      args?.globalSecondaryIndexWriteCapacity ?? 5,
    ]).apply(([mode, readCapacity, writeCapacity]) => indexes.map(index => ({
      ...index,
      ...(mode === "PROVISIONED" ? { readCapacity, writeCapacity } : {}),
    })));

    this.applicationsTable = new aws.dynamodb.Table(
      `${name}-applications`,
      {
        name: "OpenIddictApplications",
        hashKey: "pk",
        rangeKey: "sk",
        attributes: [
          { name: "pk", type: "S" },
          { name: "sk", type: "S" },
        ],
        ...base,
        ...args?.applicationsTableArgs,
        tags,
      },
      defaultOpts,
    );

    this.scopesTable = new aws.dynamodb.Table(
      `${name}-scopes`,
      {
        name: "OpenIddictScopes",
        hashKey: "pk",
        rangeKey: "sk",
        attributes: [
          { name: "pk", type: "S" },
          { name: "sk", type: "S" },
        ],
        ...base,
        ...args?.scopesTableArgs,
        tags,
      },
      defaultOpts,
    );

    this.authorizationsTable = new aws.dynamodb.Table(
      `${name}-authorizations`,
      {
        name: "OpenIddictAuthorizations",
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
        globalSecondaryIndexes: createGlobalSecondaryIndexes(authorizationBillingMode, [
          {
            name: "SubjectIndex",
            hashKey: "gsi1_pk",
            rangeKey: "gsi1_sk",
            projectionType: "KEYS_ONLY",
          },
          {
            name: "ApplicationIndex",
            hashKey: "gsi2_pk",
            rangeKey: "gsi2_sk",
            projectionType: "KEYS_ONLY",
          },
        ]),
        ...base,
        ...args?.authorizationsTableArgs,
        tags,
      },
      defaultOpts,
    );

    this.tokensTable = new aws.dynamodb.Table(
      `${name}-tokens`,
      {
        name: "OpenIddictTokens",
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
        globalSecondaryIndexes: createGlobalSecondaryIndexes(tokenBillingMode, [
          {
            name: "SubjectAppIndex",
            hashKey: "gsi1_pk",
            rangeKey: "gsi1_sk",
            projectionType: "KEYS_ONLY",
          },
          {
            name: "SubjectIndex",
            hashKey: "gsi2_pk",
            rangeKey: "gsi2_sk",
            projectionType: "KEYS_ONLY",
          },
          {
            name: "ApplicationShardedIndex",
            hashKey: "gsi3_pk",
            rangeKey: "gsi3_sk",
            projectionType: "KEYS_ONLY",
          },
          {
            name: "AuthorizationIndex",
            hashKey: "gsi4_pk",
            rangeKey: "gsi4_sk",
            projectionType: "KEYS_ONLY",
          },
        ]),
        ttl: {
          attributeName: "ttl",
          enabled: true,
        },
        ...base,
        ...args?.tokensTableArgs,
        tags,
      },
      defaultOpts,
    );

    this.registerOutputs();
  }
}

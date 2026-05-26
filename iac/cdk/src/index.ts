import { AttributeType, BillingMode, ProjectionType, Table, TableProps } from "aws-cdk-lib/aws-dynamodb";
import { Construct } from "constructs";

export interface OpenIddictDynamoDbProps {
  readonly applicationsTableName?: string;
  readonly authorizationsTableName?: string;
  readonly scopesTableName?: string;
  readonly tokensTableName?: string;
  readonly tableProps?: Omit<TableProps, "tableName" | "partitionKey" | "sortKey">;
}

export class OpenIddictDynamoDb extends Construct {
  public readonly applicationsTable: Table;
  public readonly authorizationsTable: Table;
  public readonly scopesTable: Table;
  public readonly tokensTable: Table;

  constructor(scope: Construct, id: string, props?: OpenIddictDynamoDbProps) {
    super(scope, id);

    const baseProps: Omit<TableProps, "tableName" | "partitionKey" | "sortKey"> = {
      billingMode: BillingMode.PAY_PER_REQUEST,
      ...props?.tableProps,
    };

    this.applicationsTable = new Table(this, "Applications", {
      tableName: props?.applicationsTableName ?? "OpenIddictApplications",
      partitionKey: { name: "pk", type: AttributeType.STRING },
      sortKey: { name: "sk", type: AttributeType.STRING },
      ...baseProps,
    });

    this.scopesTable = new Table(this, "Scopes", {
      tableName: props?.scopesTableName ?? "OpenIddictScopes",
      partitionKey: { name: "pk", type: AttributeType.STRING },
      sortKey: { name: "sk", type: AttributeType.STRING },
      ...baseProps,
    });

    this.authorizationsTable = new Table(this, "Authorizations", {
      tableName: props?.authorizationsTableName ?? "OpenIddictAuthorizations",
      partitionKey: { name: "pk", type: AttributeType.STRING },
      sortKey: { name: "sk", type: AttributeType.STRING },
      ...baseProps,
    });

    this.authorizationsTable.addGlobalSecondaryIndex({
      indexName: "SubjectIndex",
      partitionKey: { name: "gsi1_pk", type: AttributeType.STRING },
      sortKey: { name: "gsi1_sk", type: AttributeType.STRING },
      projectionType: ProjectionType.ALL,
    });

    this.authorizationsTable.addGlobalSecondaryIndex({
      indexName: "ApplicationIndex",
      partitionKey: { name: "gsi2_pk", type: AttributeType.STRING },
      sortKey: { name: "gsi2_sk", type: AttributeType.STRING },
      projectionType: ProjectionType.ALL,
    });

    this.tokensTable = new Table(this, "Tokens", {
      tableName: props?.tokensTableName ?? "OpenIddictTokens",
      partitionKey: { name: "pk", type: AttributeType.STRING },
      sortKey: { name: "sk", type: AttributeType.STRING },
      ...baseProps,
    });

    this.tokensTable.addGlobalSecondaryIndex({
      indexName: "SubjectAppIndex",
      partitionKey: { name: "gsi1_pk", type: AttributeType.STRING },
      sortKey: { name: "gsi1_sk", type: AttributeType.STRING },
      projectionType: ProjectionType.ALL,
    });

    this.tokensTable.addGlobalSecondaryIndex({
      indexName: "SubjectIndex",
      partitionKey: { name: "gsi2_pk", type: AttributeType.STRING },
      sortKey: { name: "gsi2_sk", type: AttributeType.STRING },
      projectionType: ProjectionType.ALL,
    });

    this.tokensTable.addGlobalSecondaryIndex({
      indexName: "ApplicationShardedIndex",
      partitionKey: { name: "gsi3_pk", type: AttributeType.STRING },
      sortKey: { name: "gsi3_sk", type: AttributeType.STRING },
      projectionType: ProjectionType.ALL,
    });

    this.tokensTable.addGlobalSecondaryIndex({
      indexName: "AuthorizationIndex",
      partitionKey: { name: "gsi4_pk", type: AttributeType.STRING },
      sortKey: { name: "gsi4_sk", type: AttributeType.STRING },
      projectionType: ProjectionType.ALL,
    });
  }
}

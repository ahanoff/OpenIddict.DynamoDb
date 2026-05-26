import { AttributeType, BillingMode, ProjectionType, Table, type TableProps } from "aws-cdk-lib/aws-dynamodb";
import { Construct } from "constructs";

type TableConfig = Omit<TableProps, "partitionKey" | "sortKey">;

export interface OpenIddictDynamoDbProps {
  readonly tableProps?: TableConfig;
  readonly applicationsTableProps?: TableConfig;
  readonly authorizationsTableProps?: TableConfig;
  readonly scopesTableProps?: TableConfig;
  readonly tokensTableProps?: TableConfig;
}

export class OpenIddictDynamoDb extends Construct {
  public readonly applicationsTable: Table;
  public readonly authorizationsTable: Table;
  public readonly scopesTable: Table;
  public readonly tokensTable: Table;

  constructor(scope: Construct, id: string, props?: OpenIddictDynamoDbProps) {
    super(scope, id);

    const base: TableConfig = {
      billingMode: BillingMode.PAY_PER_REQUEST,
      ...props?.tableProps,
    };

    this.applicationsTable = new Table(this, "Applications", {
      tableName: "OpenIddictApplications",
      partitionKey: { name: "pk", type: AttributeType.STRING },
      sortKey: { name: "sk", type: AttributeType.STRING },
      ...base,
      ...props?.applicationsTableProps,
    });

    this.scopesTable = new Table(this, "Scopes", {
      tableName: "OpenIddictScopes",
      partitionKey: { name: "pk", type: AttributeType.STRING },
      sortKey: { name: "sk", type: AttributeType.STRING },
      ...base,
      ...props?.scopesTableProps,
    });

    this.authorizationsTable = new Table(this, "Authorizations", {
      tableName: "OpenIddictAuthorizations",
      partitionKey: { name: "pk", type: AttributeType.STRING },
      sortKey: { name: "sk", type: AttributeType.STRING },
      ...base,
      ...props?.authorizationsTableProps,
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
      tableName: "OpenIddictTokens",
      partitionKey: { name: "pk", type: AttributeType.STRING },
      sortKey: { name: "sk", type: AttributeType.STRING },
      ...base,
      ...props?.tokensTableProps,
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

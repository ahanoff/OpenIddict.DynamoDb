import { App, Stack } from "aws-cdk-lib";
import { BillingMode } from "aws-cdk-lib/aws-dynamodb";
import { OpenIddictDynamoDb } from "../lib/index.js";

const app = new App();
const onDemandStack = new Stack(app, "OnDemand");
new OpenIddictDynamoDb(onDemandStack, "OpenIddict", {
  globalSecondaryIndexReadCapacity: 20,
  globalSecondaryIndexWriteCapacity: 10,
});
const provisionedStack = new Stack(app, "Provisioned");
new OpenIddictDynamoDb(provisionedStack, "OpenIddict", {
  tableProps: {
    billingMode: BillingMode.PROVISIONED,
    readCapacity: 5,
    writeCapacity: 5,
  },
  globalSecondaryIndexReadCapacity: 20,
  globalSecondaryIndexWriteCapacity: 10,
});

const assembly = app.synth();
const indexes = stack => Object.values(stack.Resources)
  .filter(resource => resource.Type === "AWS::DynamoDB::Table")
  .flatMap(resource => resource.Properties.GlobalSecondaryIndexes ?? []);
const onDemandIndexes = indexes(assembly.getStackArtifact(onDemandStack.artifactId).template);
const provisionedIndexes = indexes(assembly.getStackArtifact(provisionedStack.artifactId).template);

if (onDemandIndexes.length !== 6 || onDemandIndexes.some(index => "ProvisionedThroughput" in index)) {
  throw new Error("On-demand GSIs must omit provisioned throughput.");
}
if (provisionedIndexes.length !== 6 || provisionedIndexes.some(index =>
  index.ProvisionedThroughput.ReadCapacityUnits !== 20 ||
  index.ProvisionedThroughput.WriteCapacityUnits !== 10)) {
  throw new Error("Provisioned GSIs must use the configured throughput.");
}

console.log("CDK billing-mode GSI capacity tests passed.");

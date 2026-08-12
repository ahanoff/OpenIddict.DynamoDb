import * as pulumi from "@pulumi/pulumi";
import { OpenIddictDynamoDb } from "../bin/index.js";

const registrations = [];
pulumi.runtime.setMocks({
  newResource(args) {
    registrations.push(args);
    return { id: `${args.name}_id`, state: args.inputs };
  },
  call(args) {
    return args.inputs;
  },
}, "production-qa", "stack", false);

await pulumi.runtime.runInPulumiStack(async () => {
  const onDemand = new OpenIddictDynamoDb("on-demand", {
    globalSecondaryIndexReadCapacity: 20,
    globalSecondaryIndexWriteCapacity: 10,
  });
  const provisioned = new OpenIddictDynamoDb("provisioned", {
    tableArgs: {
      billingMode: "PROVISIONED",
      readCapacity: 5,
      writeCapacity: 5,
    },
    globalSecondaryIndexReadCapacity: 20,
    globalSecondaryIndexWriteCapacity: 10,
  });
  await Promise.all([
    onDemand.tokensTable.name.promise(),
    provisioned.tokensTable.name.promise(),
  ]);
});

const tables = registrations.filter(resource => resource.type === "aws:dynamodb/table:Table");
const indexes = prefix => tables
  .filter(table => table.name.startsWith(prefix))
  .flatMap(table => table.inputs.globalSecondaryIndexes ?? []);
const onDemandIndexes = indexes("on-demand");
const provisionedIndexes = indexes("provisioned");

if (onDemandIndexes.length !== 6 || onDemandIndexes.some(index =>
  "readCapacity" in index || "writeCapacity" in index)) {
  throw new Error("On-demand GSIs must omit provisioned throughput.");
}
if (provisionedIndexes.length !== 6 || provisionedIndexes.some(index =>
  index.readCapacity !== 20 || index.writeCapacity !== 10)) {
  throw new Error("Provisioned GSIs must use the configured throughput.");
}

console.log("Pulumi billing-mode GSI capacity tests passed.");

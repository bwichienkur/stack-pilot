# Multi-Region Deployment

## Current state

StackPilot runs as a single-region stack via Docker Compose (Postgres/pgvector, Redis, API, Workers, Frontend).

## Configuration

```json
{
  "Deployment": {
    "Region": "us-east-1",
    "DataResidency": "US",
    "ReadReplicaConnectionString": ""
  }
}
```

Environment variables: `Deployment__Region`, `Deployment__DataResidency`.

## Enterprise data residency

For Enterprise customers requiring EU or dedicated VPC:

1. Deploy isolated Compose stack or K8s namespace per tenant.
2. Set `Deployment:DataResidency` in org metadata (Platform Admin).
3. Route traffic via regional load balancer; no cross-region DB replication in MVP.

## Future

- Read replicas for analytics
- Cross-region failover with Postgres streaming replication
- Geo-routed API gateway

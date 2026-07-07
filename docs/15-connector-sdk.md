# StackPilot Connector SDK

## Overview

StackPilot connectors implement `IConnector` with `TestConnectionAsync`, `SyncAsync`, and optional scan hooks. Built-in types are registered in `ConnectorDefinitionCatalog` and `DependencyInjection.cs`.

## Building a custom connector

1. Add a `ConnectorDefinition` entry (type slug, category, config schema).
2. Implement `IConnector` in `src/StackPilot.Infrastructure/Connectors/`.
3. Register in `DependencyInjection.cs` connector registry.
4. Optionally implement `IRepositoryScanner` or `IDatabaseScanner` for deep scans.

## Config schema

Connector instances store `ConfigJson` (non-secret) and encrypted credentials in `ConnectorCredentials`.

## Webhooks

Customer outbound webhooks (`OutboundWebhookSubscription`) receive signed POSTs for events: `ticket.approved`, `release.scheduled`, `release.deployed`.

## Marketplace (roadmap)

Future: upload connector manifests, signed packages, and org-level enablement in Settings.

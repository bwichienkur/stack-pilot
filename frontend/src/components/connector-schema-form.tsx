"use client";

import { useMemo } from "react";
import { Input } from "@/components/ui";

interface SchemaProperty {
  type?: string;
  description?: string;
}

interface ConnectorSchema {
  properties?: Record<string, SchemaProperty>;
}

const CREDENTIAL_FIELDS: Record<string, { key: string; label: string; secret?: boolean }[]> = {
  github_repository: [{ key: "pat", label: "Personal Access Token", secret: true }],
  github_actions: [{ key: "pat", label: "Personal Access Token", secret: true }],
  gitlab_repository: [{ key: "pat", label: "Personal Access Token", secret: true }],
  azure_devops_repository: [{ key: "pat", label: "Personal Access Token", secret: true }],
  azure_pipelines: [{ key: "pat", label: "Personal Access Token", secret: true }],
  bitbucket_repository: [{ key: "pat", label: "App Password", secret: true }],
  jira: [
    { key: "email", label: "Jira account email" },
    { key: "api_token", label: "API token", secret: true },
  ],
  servicenow: [
    { key: "username", label: "Username" },
    { key: "password", label: "Password", secret: true },
  ],
  sql_server: [
    { key: "username", label: "Username" },
    { key: "password", label: "Password", secret: true },
  ],
  postgresql: [
    { key: "username", label: "Username" },
    { key: "password", label: "Password", secret: true },
  ],
  mysql: [
    { key: "username", label: "Username" },
    { key: "password", label: "Password", secret: true },
  ],
  mongodb: [{ key: "connection_string", label: "Connection string", secret: true }],
  jenkins: [
    { key: "username", label: "Username" },
    { key: "api_token", label: "API token", secret: true },
  ],
};

function labelize(key: string) {
  return key.replace(/([A-Z])/g, " $1").replace(/^./, (s) => s.toUpperCase());
}

export interface ConnectorFormValues {
  config: Record<string, string>;
  credentials: Record<string, string>;
}

interface ConnectorSchemaFormProps {
  connectorType: string;
  configSchemaJson: string;
  values: ConnectorFormValues;
  onChange: (values: ConnectorFormValues) => void;
}

export function ConnectorSchemaForm({ connectorType, configSchemaJson, values, onChange }: ConnectorSchemaFormProps) {
  const schema = useMemo<ConnectorSchema>(() => {
    try {
      return JSON.parse(configSchemaJson) as ConnectorSchema;
    } catch {
      return {};
    }
  }, [configSchemaJson]);

  const credentialFields = CREDENTIAL_FIELDS[connectorType] ?? [{ key: "api_token", label: "API token", secret: true }];

  const setConfig = (key: string, value: string) => {
    onChange({ ...values, config: { ...values.config, [key]: value } });
  };

  const setCredential = (key: string, value: string) => {
    onChange({ ...values, credentials: { ...values.credentials, [key]: value } });
  };

  return (
    <div className="space-y-4">
      {schema.properties && Object.entries(schema.properties).map(([key, prop]) => (
        <div key={key}>
          <label className="text-sm text-zinc-400 mb-1 block">{labelize(key)}</label>
          <Input
            value={values.config[key] ?? ""}
            onChange={(e) => setConfig(key, e.target.value)}
            placeholder={prop.description ?? labelize(key)}
          />
        </div>
      ))}

      <div className="pt-2 border-t border-zinc-800 space-y-3">
        <p className="text-xs text-zinc-500 uppercase tracking-wide">Credentials</p>
        {credentialFields.map((field) => (
          <div key={field.key}>
            <label className="text-sm text-zinc-400 mb-1 block">{field.label}</label>
            <Input
              type={field.secret ? "password" : "text"}
              value={values.credentials[field.key] ?? ""}
              onChange={(e) => setCredential(field.key, e.target.value)}
              autoComplete="off"
            />
          </div>
        ))}
      </div>
    </div>
  );
}

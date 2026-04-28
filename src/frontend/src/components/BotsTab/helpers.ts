import type { AgentParameterManifest } from './types';

export function formatDate(dateStr: string | null): string {
  if (!dateStr) return '-';
  return new Date(dateStr).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

export function parseAgentConfig(json: string | null): Record<string, string> {
  if (!json) return {};
  try {
    return JSON.parse(json) as Record<string, string>;
  } catch {
    return {};
  }
}

export function buildDefaultParamValues(
  params: AgentParameterManifest[],
): Record<string, string> {
  const result: Record<string, string> = {};
  for (const p of params) {
    result[p.name] = p.defaultValue ?? '';
  }
  return result;
}

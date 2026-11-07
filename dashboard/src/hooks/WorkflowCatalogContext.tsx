import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import type { ModuleInfo, WorkflowInfo } from '../types';
import { fetchModules, fetchWorkflows } from '../api/client';

export const SELECTION_STORAGE_KEY = 'flowgent.selection';
const REFRESH_INTERVAL_MS = 10000;

export interface WorkflowSelection {
  moduleId: string;
  workflowId: string;
}

interface WorkflowCatalogValue {
  modules: ModuleInfo[];
  workflows: WorkflowInfo[];
  isLoading: boolean;
  error: string | null;
  selection: WorkflowSelection | null;
  selectedModule: ModuleInfo | null;
  selectedWorkflow: WorkflowInfo | null;
  select: (selection: WorkflowSelection) => void;
  /** Step display names by workflow id and step id. */
  stepName: (workflowId: string, stepId: string | null) => string;
  workflowName: (workflowId: string) => string;
  refresh: () => Promise<void>;
}

const WorkflowCatalogCtx = createContext<WorkflowCatalogValue | null>(null);

export function readStoredSelection(storage: Pick<Storage, 'getItem'> = window.localStorage): WorkflowSelection | null {
  try {
    const raw = storage.getItem(SELECTION_STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Partial<WorkflowSelection>;
    return typeof parsed.moduleId === 'string' && typeof parsed.workflowId === 'string'
      ? { moduleId: parsed.moduleId, workflowId: parsed.workflowId }
      : null;
  } catch {
    return null;
  }
}

/**
 * Keeps the stored selection when it still exists, otherwise falls back to the first available
 * workflow (then to the first workflow at all).
 */
export function resolveSelection(
  workflows: WorkflowInfo[],
  preferred: WorkflowSelection | null,
): WorkflowSelection | null {
  if (preferred && workflows.some((w) => w.id === preferred.workflowId && w.moduleId === preferred.moduleId)) {
    return preferred;
  }
  const fallback = workflows.find((w) => w.available) ?? workflows[0];
  return fallback ? { moduleId: fallback.moduleId, workflowId: fallback.id } : null;
}

/** Loads modules and workflows from the engine and keeps the selected module/workflow (persisted). */
export function WorkflowCatalogProvider({ children }: { children: ReactNode }) {
  const [modules, setModules] = useState<ModuleInfo[]>([]);
  const [workflows, setWorkflows] = useState<WorkflowInfo[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [preferred, setPreferred] = useState<WorkflowSelection | null>(() => readStoredSelection());

  const refresh = useCallback(async () => {
    try {
      const [moduleData, workflowData] = await Promise.all([fetchModules(), fetchWorkflows()]);
      setModules(moduleData);
      setWorkflows(workflowData);
      setError(null);
    } catch (err) {
      setError((err as { message?: string }).message ?? 'Failed to load workflows');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    refresh();
    const interval = setInterval(refresh, REFRESH_INTERVAL_MS);
    return () => clearInterval(interval);
  }, [refresh]);

  const select = useCallback((next: WorkflowSelection) => {
    setPreferred(next);
    try {
      window.localStorage.setItem(SELECTION_STORAGE_KEY, JSON.stringify(next));
    } catch {
      // Storage unavailable (private mode): keep the selection in memory only
    }
  }, []);

  const value = useMemo<WorkflowCatalogValue>(() => {
    const selection = resolveSelection(workflows, preferred);
    const selectedWorkflow = selection ? workflows.find((w) => w.id === selection.workflowId) ?? null : null;
    const selectedModule = selection ? modules.find((m) => m.id === selection.moduleId) ?? null : null;
    const names = new Map(workflows.map((w) => [w.id, new Map(w.graph.nodes.map((n) => [n.id, n.name]))]));

    return {
      modules,
      workflows,
      isLoading,
      error,
      selection,
      selectedModule,
      selectedWorkflow,
      select,
      stepName: (workflowId, stepId) => (stepId ? names.get(workflowId)?.get(stepId) ?? stepId : '—'),
      workflowName: (workflowId) => workflows.find((w) => w.id === workflowId)?.name ?? workflowId,
      refresh,
    };
  }, [modules, workflows, isLoading, error, preferred, select, refresh]);

  return <WorkflowCatalogCtx.Provider value={value}>{children}</WorkflowCatalogCtx.Provider>;
}

const EMPTY_CATALOG: WorkflowCatalogValue = {
  modules: [],
  workflows: [],
  isLoading: false,
  error: null,
  selection: null,
  selectedModule: null,
  selectedWorkflow: null,
  select: () => {},
  stepName: (_workflowId, stepId) => stepId ?? '—',
  workflowName: (workflowId) => workflowId,
  refresh: async () => {},
};

/** Modules, workflows and the current selection (works without a provider in isolated component tests). */
export function useWorkflowCatalog(): WorkflowCatalogValue {
  return useContext(WorkflowCatalogCtx) ?? EMPTY_CATALOG;
}

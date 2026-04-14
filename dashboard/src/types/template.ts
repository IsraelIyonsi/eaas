// ============================================================
// EaaS Dashboard - Template Types
// ============================================================

export interface Template {
  id: string;
  name: string;
  subjectTemplate: string;
  htmlTemplate?: string;
  textTemplate?: string;
  variablesSchema?: string;
  version: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string;
}

export interface CreateTemplateRequest {
  name: string;
  subjectTemplate: string;
  htmlTemplate?: string;
  textTemplate?: string;
  variablesSchema?: string;
}

export interface UpdateTemplateRequest {
  name?: string;
  subjectTemplate?: string;
  htmlTemplate?: string;
  textTemplate?: string;
  variablesSchema?: string;
}

export interface TemplateVersion {
  id: string;
  version: number;
  name: string;
  subject: string;
  htmlTemplate?: string;
  textTemplate?: string;
  description?: string;
  createdAt: string;
}

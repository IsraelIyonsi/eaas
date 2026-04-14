"use client";

import { useState } from "react";
import { useParams, useRouter } from "next/navigation";
import {
  useTemplate,
  useUpdateTemplate,
  usePreviewTemplate,
  useTemplateVersions,
  useRollbackTemplate,
} from "@/lib/hooks/use-templates";
import { PageHeader } from "@/components/shared/page-header";
import { DetailSkeleton } from "@/components/shared/loading-skeleton";
import { ConfirmDialog } from "@/components/shared/confirm-dialog";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";
import { Routes } from "@/lib/constants/routes";
import { toast } from "sonner";
import { Save, Eye, RotateCcw, History, Loader2 } from "lucide-react";
import { format, parseISO } from "date-fns";
import type { TemplateVersion } from "@/types/template";

export default function TemplateEditorPage() {
  const params = useParams();
  const router = useRouter();
  const id = params.id as string;

  const { data: template, isLoading } = useTemplate(id);
  const { data: versionsData, isLoading: versionsLoading } = useTemplateVersions(id);

  const updateMutation = useUpdateTemplate();
  const previewMutation = usePreviewTemplate();
  const rollbackMutation = useRollbackTemplate();

  const [form, setForm] = useState<{
    name: string;
    subjectTemplate: string;
    htmlTemplate: string;
    textTemplate: string;
  } | null>(null);

  const [previewHtml, setPreviewHtml] = useState<string | null>(null);
  const [previewOpen, setPreviewOpen] = useState(false);
  const [rollbackVersion, setRollbackVersion] = useState<TemplateVersion | null>(null);

  // Sync form from loaded template (only once)
  if (template && !form) {
    setForm({
      name: template.name,
      subjectTemplate: template.subjectTemplate,
      htmlTemplate: template.htmlTemplate ?? "",
      textTemplate: template.textTemplate ?? "",
    });
  }

  function handleSave() {
    if (!form) return;
    updateMutation.mutate(
      { id, data: form },
      {
        onSuccess: (tpl) => {
          toast.success(`Template "${tpl.name}" saved. Now at v${tpl.version}.`);
        },
        onError: () => {
          toast.error("Failed to save template.");
        },
      },
    );
  }

  function handlePreview() {
    previewMutation.mutate(
      { id, variables: {} },
      {
        onSuccess: (result) => {
          setPreviewHtml(result.htmlTemplate);
          setPreviewOpen(true);
        },
        onError: () => {
          toast.error("Preview failed.");
        },
      },
    );
  }

  function handleRollback(version: TemplateVersion) {
    rollbackMutation.mutate(
      { id, version: version.version },
      {
        onSuccess: (tpl) => {
          toast.success(`Rolled back to v${version.version}. Now at v${tpl.version}.`);
          setRollbackVersion(null);
          // Update form with rolled-back content
          setForm({
            name: tpl.name,
            subjectTemplate: tpl.subjectTemplate,
            htmlTemplate: tpl.htmlTemplate ?? "",
            textTemplate: tpl.textTemplate ?? "",
          });
        },
        onError: () => {
          toast.error("Rollback failed.");
          setRollbackVersion(null);
        },
      },
    );
  }

  if (isLoading || !form) {
    return (
      <div className="space-y-6">
        <PageHeader
          title="Template Editor"
          backHref={Routes.TEMPLATES}
          backLabel="Back to Templates"
        />
        <DetailSkeleton />
      </div>
    );
  }

  if (!template) {
    return (
      <div className="space-y-6">
        <PageHeader
          title="Template Editor"
          backHref={Routes.TEMPLATES}
          backLabel="Back to Templates"
        />
        <div className="rounded-lg border border-border bg-card p-8 text-center text-muted-foreground">
          Template not found.
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={template.name}
        description={`Version v${template.version} · Last updated ${format(parseISO(template.updatedAt), "MMM d, yyyy")}`}
        backHref={Routes.TEMPLATES}
        backLabel="Back to Templates"
        action={
          <div className="flex gap-2">
            <Button
              variant="outline"
              onClick={handlePreview}
              disabled={previewMutation.isPending}
              className="border-border text-muted-foreground hover:text-foreground"
            >
              {previewMutation.isPending ? (
                <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />
              ) : (
                <Eye className="mr-1.5 h-4 w-4" />
              )}
              Preview
            </Button>
            <Button
              onClick={handleSave}
              disabled={updateMutation.isPending}
              className="bg-primary text-primary-foreground hover:bg-primary/90"
            >
              {updateMutation.isPending ? (
                <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />
              ) : (
                <Save className="mr-1.5 h-4 w-4" />
              )}
              Save
            </Button>
          </div>
        }
      />

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        {/* Editor — takes 2/3 */}
        <div className="lg:col-span-2 space-y-6">
          <Card className="border-border bg-card">
            <CardHeader className="pb-4">
              <CardTitle className="text-sm font-semibold uppercase tracking-wider text-muted-foreground/60">
                Template Details
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-5">
              <div className="space-y-2">
                <Label className="text-foreground/80">Template Name</Label>
                <Input
                  value={form.name}
                  onChange={(e) => setForm({ ...form, name: e.target.value })}
                  placeholder="e.g., Welcome Email"
                  className="border-border bg-muted text-foreground"
                />
              </div>
              <div className="space-y-2">
                <Label className="text-foreground/80">Subject Template</Label>
                <Input
                  value={form.subjectTemplate}
                  onChange={(e) => setForm({ ...form, subjectTemplate: e.target.value })}
                  placeholder="e.g., Welcome to {{company_name}}"
                  className="border-border bg-muted text-foreground font-[var(--font-jetbrains-mono)] text-sm"
                />
              </div>
              <Tabs defaultValue="html" className="mt-2">
                <TabsList className="bg-muted">
                  <TabsTrigger value="html">HTML Body</TabsTrigger>
                  <TabsTrigger value="text">Text Body</TabsTrigger>
                </TabsList>
                <TabsContent value="html" className="mt-4">
                  <Textarea
                    value={form.htmlTemplate}
                    onChange={(e) => setForm({ ...form, htmlTemplate: e.target.value })}
                    placeholder="<html><body>Your email here...</body></html>"
                    className="min-h-[360px] border-border bg-background font-[var(--font-jetbrains-mono)] text-sm text-[var(--chart-1)] leading-relaxed tracking-wide"
                  />
                </TabsContent>
                <TabsContent value="text" className="mt-4">
                  <Textarea
                    value={form.textTemplate}
                    onChange={(e) => setForm({ ...form, textTemplate: e.target.value })}
                    placeholder="Plain text version of your email..."
                    className="min-h-[360px] border-border bg-background font-[var(--font-jetbrains-mono)] text-sm text-foreground leading-relaxed"
                  />
                </TabsContent>
              </Tabs>
            </CardContent>
          </Card>
        </div>

        {/* Version History — takes 1/3 */}
        <div className="space-y-4">
          <Card className="border-border bg-card">
            <CardHeader className="pb-3">
              <CardTitle className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wider text-muted-foreground/60">
                <History className="h-4 w-4" />
                Version History
              </CardTitle>
            </CardHeader>
            <CardContent className="p-0">
              {versionsLoading ? (
                <div className="space-y-2 p-4">
                  <Skeleton className="h-12 rounded bg-muted" />
                  <Skeleton className="h-12 rounded bg-muted" />
                  <Skeleton className="h-12 rounded bg-muted" />
                </div>
              ) : !versionsData?.items.length ? (
                <p className="p-4 text-sm text-muted-foreground/60">No previous versions.</p>
              ) : (
                <ul className="divide-y divide-border">
                  {versionsData.items.map((v) => (
                    <li
                      key={v.id}
                      className="flex items-center justify-between px-4 py-3 hover:bg-muted/40 transition-colors"
                    >
                      <div className="min-w-0">
                        <div className="flex items-center gap-2">
                          <Badge
                            variant="outline"
                            className="border-border text-xs text-muted-foreground"
                          >
                            v{v.version}
                          </Badge>
                          {v.version === template.version && (
                            <Badge className="bg-primary/20 text-primary text-xs">
                              current
                            </Badge>
                          )}
                        </div>
                        <p className="mt-1 truncate text-xs text-muted-foreground/60">
                          {format(parseISO(v.createdAt), "MMM d, yyyy HH:mm")}
                        </p>
                        {v.description && (
                          <p className="mt-0.5 truncate text-xs text-muted-foreground/50 italic">
                            {v.description}
                          </p>
                        )}
                      </div>
                      {v.version !== template.version && (
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => setRollbackVersion(v)}
                          className="ml-2 shrink-0 text-muted-foreground hover:text-foreground"
                        >
                          <RotateCcw className="h-3.5 w-3.5" />
                        </Button>
                      )}
                    </li>
                  ))}
                </ul>
              )}
            </CardContent>
          </Card>
        </div>
      </div>

      {/* Preview Dialog */}
      {previewOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4">
          <div className="relative w-full max-w-2xl rounded-lg border border-border bg-card shadow-xl">
            <div className="flex items-center justify-between border-b border-border px-6 py-4">
              <h2 className="text-base font-semibold text-foreground">
                Preview: {template.name}
              </h2>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setPreviewOpen(false)}
                className="text-muted-foreground hover:text-foreground"
              >
                Close
              </Button>
            </div>
            <div className="overflow-hidden rounded-b-lg bg-white">
              <iframe
                srcDoc={previewHtml ?? ""}
                title="Template preview"
                className="h-[420px] w-full"
                sandbox=""
              />
            </div>
          </div>
        </div>
      )}

      {/* Rollback Confirm Dialog */}
      <ConfirmDialog
        open={!!rollbackVersion}
        onOpenChange={() => setRollbackVersion(null)}
        title={`Rollback to v${rollbackVersion?.version}`}
        description={`This will restore the template to version ${rollbackVersion?.version} and create a new version. The current content will be saved in history.`}
        confirmLabel="Rollback"
        variant="destructive"
        loading={rollbackMutation.isPending}
        onConfirm={() => rollbackVersion && handleRollback(rollbackVersion)}
      />
    </div>
  );
}

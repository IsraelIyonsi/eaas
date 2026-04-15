"use client";

import { useState } from "react";
import {
  useTemplates,
  useCreateTemplate,
  useUpdateTemplate,
  useDeleteTemplate,
} from "@/lib/hooks/use-templates";
import { repositories } from "@/lib/api";
import { PageHeader } from "@/components/shared/page-header";
import { EmptyState } from "@/components/shared/empty-state";
import { ConfirmDialog } from "@/components/shared/confirm-dialog";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Skeleton } from "@/components/ui/skeleton";
import { toast } from "sonner";
import { Plus, MoreVertical, Pencil, Eye, Trash2, Search, FileText } from "lucide-react";
import { format, parseISO } from "date-fns";
import { useRouter } from "next/navigation";
import { Routes } from "@/lib/constants/routes";
import type { Template } from "@/types";

const emptyTemplate = {
  name: "",
  subjectTemplate: "",
  htmlTemplate: "",
  textTemplate: "",
};

export default function TemplatesPage() {
  const router = useRouter();
  const [search, setSearch] = useState("");
  const [dialogOpen, setDialogOpen] = useState(false);
  const [previewOpen, setPreviewOpen] = useState(false);
  const [editingTemplate, setEditingTemplate] = useState<Template | null>(null);
  const [previewTemplate, setPreviewTemplate] = useState<Template | null>(null);
  const [form, setForm] = useState(emptyTemplate);
  const [deleteConfirmId, setDeleteConfirmId] = useState<string | null>(null);

  const { data, isLoading } = useTemplates({ search: search || undefined });
  const createMutation = useCreateTemplate();
  const updateMutation = useUpdateTemplate();
  const deleteMutation = useDeleteTemplate();

  function openCreate() {
    setEditingTemplate(null);
    setForm(emptyTemplate);
    setDialogOpen(true);
  }

  async function openEdit(tpl: Template) {
    // List endpoint returns a summary without htmlTemplate/textTemplate, so fetch
    // the full template to pre-populate body fields — otherwise saving would
    // wipe the existing body content.
    setEditingTemplate(tpl);
    setForm({
      name: tpl.name,
      subjectTemplate: tpl.subjectTemplate,
      htmlTemplate: tpl.htmlTemplate ?? "",
      textTemplate: tpl.textTemplate ?? "",
    });
    setDialogOpen(true);
    try {
      const full = await repositories.template.getById(tpl.id);
      setEditingTemplate(full);
      setForm({
        name: full.name,
        subjectTemplate: full.subjectTemplate,
        htmlTemplate: full.htmlTemplate ?? "",
        textTemplate: full.textTemplate ?? "",
      });
    } catch {
      toast.error("Failed to load template content. Please close and retry.");
    }
  }

  function closeDialog() {
    setDialogOpen(false);
    setEditingTemplate(null);
    setForm(emptyTemplate);
  }

  function handleSave() {
    if (editingTemplate) {
      updateMutation.mutate(
        { id: editingTemplate.id, data: form },
        {
          onSuccess: (tpl) => {
            toast.success(`Template "${tpl.name}" saved. Version ${tpl.version}.`);
            closeDialog();
          },
        },
      );
    } else {
      createMutation.mutate(form, {
        onSuccess: (tpl) => {
          toast.success(`Template "${tpl.name}" created successfully.`);
          closeDialog();
        },
      });
    }
  }

  function handleDelete() {
    if (!deleteConfirmId) return;
    deleteMutation.mutate(deleteConfirmId, {
      onSuccess: () => {
        toast.success("Template deleted. It can be restored within 30 days.");
        setDeleteConfirmId(null);
      },
    });
  }

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <PageHeader
        title="Templates"
        description="Manage email templates with Liquid syntax and variable schemas."
        action={
          <Button
            onClick={openCreate}
            className="bg-primary text-primary-foreground hover:bg-primary/90"
          >
            <Plus className="mr-1.5 h-4 w-4" />
            Create Template
          </Button>
        }
      />

      {/* Search */}
      <div className="relative max-w-xs">
        <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground/40" />
        <Input
          placeholder="Search templates..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="border-border bg-muted pl-9 text-foreground placeholder:text-muted-foreground/40"
        />
      </div>

      {/* Table */}
      {isLoading ? (
        <Skeleton className="h-[300px] rounded-lg bg-muted" />
      ) : data?.items.length === 0 ? (
        <div className="rounded-lg border border-border bg-card">
          <EmptyState
            icon={FileText}
            title="No templates yet"
            description="Templates let you reuse email layouts with dynamic variables. Create your first template or use the API to send raw HTML."
            action={{ label: "Create Template", onClick: openCreate }}
          />
        </div>
      ) : (
        <div className="rounded-lg border border-border bg-card">
          <Table>
            <TableHeader>
              <TableRow className="border-border hover:bg-transparent">
                <TableHead className="text-xs font-semibold uppercase tracking-wider text-muted-foreground/60">
                  Name
                </TableHead>
                <TableHead className="text-xs font-semibold uppercase tracking-wider text-muted-foreground/60">
                  Subject
                </TableHead>
                <TableHead className="hidden text-xs font-semibold uppercase tracking-wider text-muted-foreground/60 md:table-cell">
                  Version
                </TableHead>
                <TableHead className="hidden text-xs font-semibold uppercase tracking-wider text-muted-foreground/60 sm:table-cell">
                  Updated
                </TableHead>
                <TableHead className="w-12" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {data?.items.map((tpl) => (
                <TableRow
                  key={tpl.id}
                  className="cursor-pointer border-border transition-colors hover:bg-muted even:bg-muted/30"
                  onClick={() => router.push(Routes.TEMPLATE_EDITOR(tpl.id))}
                >
                  <TableCell className="font-medium text-foreground">
                    {tpl.name}
                  </TableCell>
                  <TableCell className="max-w-[200px] truncate text-sm text-muted-foreground">
                    {tpl.subjectTemplate}
                  </TableCell>
                  <TableCell className="hidden text-sm text-muted-foreground/60 md:table-cell">
                    v{tpl.version}
                  </TableCell>
                  <TableCell className="hidden text-xs text-muted-foreground/60 whitespace-nowrap sm:table-cell">
                    {format(parseISO(tpl.updatedAt), "MMM d, yyyy")}
                  </TableCell>
                  <TableCell onClick={(e) => e.stopPropagation()}>
                    <DropdownMenu>
                      <DropdownMenuTrigger
                        className="inline-flex h-8 w-8 items-center justify-center rounded-md text-muted-foreground/60 hover:text-foreground hover:bg-muted"
                      >
                        <MoreVertical className="h-4 w-4" />
                      </DropdownMenuTrigger>
                      <DropdownMenuContent
                        align="end"
                        className="border-border bg-muted"
                      >
                        <DropdownMenuItem onClick={() => openEdit(tpl)}>
                          <Pencil className="mr-2 h-3.5 w-3.5" />
                          Edit
                        </DropdownMenuItem>
                        <DropdownMenuItem
                          onClick={() => {
                            setPreviewTemplate(tpl);
                            setPreviewOpen(true);
                          }}
                        >
                          <Eye className="mr-2 h-3.5 w-3.5" />
                          Preview
                        </DropdownMenuItem>
                        <DropdownMenuItem
                          className="text-red-400 focus:text-red-400"
                          onClick={() => setDeleteConfirmId(tpl.id)}
                        >
                          <Trash2 className="mr-2 h-3.5 w-3.5" />
                          Delete Template
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      {/* Create / Edit Dialog */}
      <Dialog open={dialogOpen} onOpenChange={closeDialog}>
        <DialogContent className="max-w-2xl border-border bg-card">
          <DialogHeader>
            <DialogTitle className="text-foreground">
              {editingTemplate
                ? `Edit Template: ${editingTemplate.name}`
                : "Create Template"}
            </DialogTitle>
          </DialogHeader>
          <Tabs defaultValue="edit" className="mt-4">
            <TabsList className="bg-muted">
              <TabsTrigger value="edit">Edit</TabsTrigger>
              <TabsTrigger value="preview">Preview</TabsTrigger>
            </TabsList>
            <TabsContent value="edit" className="mt-6 space-y-5">
              <div className="space-y-2">
                <Label className="text-foreground/80">Template Name</Label>
                <Input
                  value={form.name}
                  onChange={(e) => setForm({ ...form, name: e.target.value })}
                  placeholder="e.g., Invoice Notification"
                  className="border-border bg-muted text-foreground"
                />
              </div>
              <div className="space-y-2">
                <Label className="text-foreground/80">Subject Template</Label>
                <Input
                  value={form.subjectTemplate}
                  onChange={(e) =>
                    setForm({ ...form, subjectTemplate: e.target.value })
                  }
                  placeholder="e.g., Invoice #{{invoice_number}}"
                  className="border-border bg-muted text-foreground font-[var(--font-jetbrains-mono)] text-sm"
                />
              </div>
              <div className="space-y-2">
                <Label className="text-foreground/80">HTML Body</Label>
                <Textarea
                  value={form.htmlTemplate}
                  onChange={(e) =>
                    setForm({ ...form, htmlTemplate: e.target.value })
                  }
                  placeholder="<html><body>Your email here...</body></html>"
                  className="min-h-[220px] border-border bg-background font-[var(--font-jetbrains-mono)] text-sm text-[var(--chart-1)] leading-relaxed tracking-wide"
                />
              </div>
              <div className="space-y-2">
                <Label className="text-foreground/80">Text Body</Label>
                <Textarea
                  value={form.textTemplate}
                  onChange={(e) =>
                    setForm({ ...form, textTemplate: e.target.value })
                  }
                  placeholder="Plain text version..."
                  className="min-h-[120px] border-border bg-background font-[var(--font-jetbrains-mono)] text-sm text-foreground leading-relaxed"
                />
              </div>
            </TabsContent>
            <TabsContent value="preview" className="mt-6">
              <div className="rounded-lg border border-border bg-white overflow-hidden">
                <iframe
                  srcDoc={form.htmlTemplate || "<div style='padding:32px;color:#888;font-family:sans-serif;text-align:center'><p>Enter HTML in the editor to see a live preview here.</p></div>"}
                  title="Template preview"
                  className="h-[340px] w-full"
                  sandbox=""
                />
              </div>
            </TabsContent>
          </Tabs>
          <DialogFooter className="mt-4">
            <Button variant="ghost" onClick={closeDialog} className="text-muted-foreground">
              Cancel
            </Button>
            <Button
              onClick={handleSave}
              disabled={!form.name || !form.subjectTemplate}
              className="bg-primary text-primary-foreground hover:bg-primary/90"
            >
              {editingTemplate ? "Save Template" : "Create Template"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Preview Dialog */}
      <Dialog open={previewOpen} onOpenChange={setPreviewOpen}>
        <DialogContent className="max-w-2xl border-border bg-card">
          <DialogHeader>
            <DialogTitle className="text-foreground">
              Preview: {previewTemplate?.name}
            </DialogTitle>
          </DialogHeader>
          <div className="mt-4 rounded-lg border border-border bg-white overflow-hidden">
            <iframe
              srcDoc={previewTemplate?.htmlTemplate ?? ""}
              title="Template preview"
              className="h-[400px] w-full"
              sandbox=""
            />
          </div>
        </DialogContent>
      </Dialog>

      {/* Delete Confirmation Dialog */}
      <ConfirmDialog
        open={!!deleteConfirmId}
        onOpenChange={() => setDeleteConfirmId(null)}
        title="Delete Template"
        description="Are you sure you want to delete this template? It can be restored within 30 days."
        confirmLabel="Delete Template"
        variant="destructive"
        loading={deleteMutation.isPending}
        onConfirm={handleDelete}
      />
    </div>
  );
}

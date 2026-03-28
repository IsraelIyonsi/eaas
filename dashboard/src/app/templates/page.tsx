"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
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
import { Plus, MoreVertical, Pencil, Eye, Trash2, Search } from "lucide-react";
import { format, parseISO } from "date-fns";
import type { Template } from "@/types";

const emptyTemplate = {
  name: "",
  subject: "",
  html_body: "",
  text_body: "",
};

export default function TemplatesPage() {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [dialogOpen, setDialogOpen] = useState(false);
  const [previewOpen, setPreviewOpen] = useState(false);
  const [editingTemplate, setEditingTemplate] = useState<Template | null>(null);
  const [previewTemplate, setPreviewTemplate] = useState<Template | null>(null);
  const [form, setForm] = useState(emptyTemplate);
  const [deleteConfirmId, setDeleteConfirmId] = useState<string | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: ["templates", search],
    queryFn: () => api.getTemplates(search || undefined),
  });

  const createMutation = useMutation({
    mutationFn: (data: typeof emptyTemplate) => api.createTemplate(data),
    onSuccess: (tpl) => {
      queryClient.invalidateQueries({ queryKey: ["templates"] });
      toast.success(`Template "${tpl.name}" created successfully.`);
      closeDialog();
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, data }: { id: string; data: Partial<typeof emptyTemplate> }) =>
      api.updateTemplate(id, data),
    onSuccess: (tpl) => {
      queryClient.invalidateQueries({ queryKey: ["templates"] });
      toast.success(`Template "${tpl.name}" saved. Version ${tpl.version}.`);
      closeDialog();
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => api.deleteTemplate(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["templates"] });
      toast.success("Template deleted. It can be restored within 30 days.");
      setDeleteConfirmId(null);
    },
  });

  function openCreate() {
    setEditingTemplate(null);
    setForm(emptyTemplate);
    setDialogOpen(true);
  }

  function openEdit(tpl: Template) {
    setEditingTemplate(tpl);
    setForm({
      name: tpl.name,
      subject: tpl.subject,
      html_body: tpl.html_body,
      text_body: tpl.text_body,
    });
    setDialogOpen(true);
  }

  function closeDialog() {
    setDialogOpen(false);
    setEditingTemplate(null);
    setForm(emptyTemplate);
  }

  function handleSave() {
    if (editingTemplate) {
      updateMutation.mutate({ id: editingTemplate.id, data: form });
    } else {
      createMutation.mutate(form);
    }
  }

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-xl font-bold text-white">Templates</h1>
          <p className="text-sm text-white/50">
            Manage email templates with Liquid syntax and variable schemas.
          </p>
        </div>
        <Button
          onClick={openCreate}
          className="bg-[#7C4DFF] text-white hover:bg-[#6B3FE8]"
        >
          <Plus className="mr-1.5 h-4 w-4" />
          Create Template
        </Button>
      </div>

      {/* Search */}
      <div className="relative max-w-xs">
        <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-white/30" />
        <Input
          placeholder="Search templates..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="border-white/10 bg-[#27293D] pl-9 text-white placeholder:text-white/30"
        />
      </div>

      {/* Table */}
      {isLoading ? (
        <Skeleton className="h-[300px] rounded-lg bg-white/5" />
      ) : data?.items.length === 0 ? (
        <div className="flex flex-col items-center justify-center rounded-lg border border-white/10 bg-[#1E1E2E] py-16 text-center">
          <p className="text-lg font-semibold text-white">No templates yet</p>
          <p className="mt-2 max-w-sm text-sm text-white/50">
            Templates let you reuse email layouts with dynamic variables. Create
            your first template or use the API to send raw HTML.
          </p>
          <Button
            onClick={openCreate}
            className="mt-4 bg-[#7C4DFF] text-white hover:bg-[#6B3FE8]"
          >
            Create Template
          </Button>
        </div>
      ) : (
        <div className="rounded-lg border border-white/10 bg-[#1E1E2E]">
          <Table>
            <TableHeader>
              <TableRow className="border-white/10 hover:bg-transparent">
                <TableHead className="text-xs font-semibold uppercase tracking-wider text-white/40">
                  Name
                </TableHead>
                <TableHead className="text-xs font-semibold uppercase tracking-wider text-white/40">
                  Subject
                </TableHead>
                <TableHead className="hidden text-xs font-semibold uppercase tracking-wider text-white/40 md:table-cell">
                  Version
                </TableHead>
                <TableHead className="text-xs font-semibold uppercase tracking-wider text-white/40">
                  Updated
                </TableHead>
                <TableHead className="w-12" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {data?.items.map((tpl) => (
                <TableRow
                  key={tpl.id}
                  className="border-white/5 hover:bg-white/[0.03]"
                >
                  <TableCell className="font-medium text-white">
                    {tpl.name}
                  </TableCell>
                  <TableCell className="max-w-[200px] truncate text-sm text-white/60">
                    {tpl.subject}
                  </TableCell>
                  <TableCell className="hidden text-sm text-white/40 md:table-cell">
                    v{tpl.version}
                  </TableCell>
                  <TableCell className="text-xs text-white/40 whitespace-nowrap">
                    {format(parseISO(tpl.updated_at), "MMM d, yyyy")}
                  </TableCell>
                  <TableCell>
                    <DropdownMenu>
                      <DropdownMenuTrigger
                        className="inline-flex h-8 w-8 items-center justify-center rounded-md text-white/40 hover:text-white hover:bg-white/5"
                      >
                        <MoreVertical className="h-4 w-4" />
                      </DropdownMenuTrigger>
                      <DropdownMenuContent
                        align="end"
                        className="border-white/10 bg-[#27293D]"
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
        <DialogContent className="max-w-2xl border-white/10 bg-[#1E1E2E]">
          <DialogHeader>
            <DialogTitle className="text-white">
              {editingTemplate
                ? `Edit Template: ${editingTemplate.name}`
                : "Create Template"}
            </DialogTitle>
          </DialogHeader>
          <Tabs defaultValue="edit" className="mt-2">
            <TabsList className="bg-white/5">
              <TabsTrigger value="edit">Edit</TabsTrigger>
              <TabsTrigger value="preview">Preview</TabsTrigger>
            </TabsList>
            <TabsContent value="edit" className="mt-4 space-y-4">
              <div className="space-y-2">
                <Label className="text-white/70">Template Name</Label>
                <Input
                  value={form.name}
                  onChange={(e) => setForm({ ...form, name: e.target.value })}
                  placeholder="e.g., Invoice Notification"
                  className="border-white/10 bg-[#27293D] text-white"
                />
              </div>
              <div className="space-y-2">
                <Label className="text-white/70">Subject Template</Label>
                <Input
                  value={form.subject}
                  onChange={(e) =>
                    setForm({ ...form, subject: e.target.value })
                  }
                  placeholder="e.g., Invoice #{{invoice_number}}"
                  className="border-white/10 bg-[#27293D] text-white font-mono text-sm"
                />
              </div>
              <div className="space-y-2">
                <Label className="text-white/70">HTML Body</Label>
                <Textarea
                  value={form.html_body}
                  onChange={(e) =>
                    setForm({ ...form, html_body: e.target.value })
                  }
                  placeholder="<html><body>Your email here...</body></html>"
                  className="min-h-[200px] border-white/10 bg-[#27293D] font-mono text-sm text-white"
                />
              </div>
              <div className="space-y-2">
                <Label className="text-white/70">Text Body</Label>
                <Textarea
                  value={form.text_body}
                  onChange={(e) =>
                    setForm({ ...form, text_body: e.target.value })
                  }
                  placeholder="Plain text version..."
                  className="min-h-[100px] border-white/10 bg-[#27293D] font-mono text-sm text-white"
                />
              </div>
            </TabsContent>
            <TabsContent value="preview" className="mt-4">
              <div className="rounded-lg border border-white/10 bg-white overflow-hidden">
                <iframe
                  srcDoc={form.html_body || "<p style='padding:20px;color:#888'>Enter HTML to see preview</p>"}
                  title="Template preview"
                  className="h-[300px] w-full"
                  sandbox=""
                />
              </div>
            </TabsContent>
          </Tabs>
          <DialogFooter className="mt-4">
            <Button variant="ghost" onClick={closeDialog} className="text-white/60">
              Cancel
            </Button>
            <Button
              onClick={handleSave}
              disabled={!form.name || !form.subject}
              className="bg-[#7C4DFF] text-white hover:bg-[#6B3FE8]"
            >
              {editingTemplate ? "Save Template" : "Create Template"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Preview Dialog */}
      <Dialog open={previewOpen} onOpenChange={setPreviewOpen}>
        <DialogContent className="max-w-2xl border-white/10 bg-[#1E1E2E]">
          <DialogHeader>
            <DialogTitle className="text-white">
              Preview: {previewTemplate?.name}
            </DialogTitle>
          </DialogHeader>
          <div className="mt-4 rounded-lg border border-white/10 bg-white overflow-hidden">
            <iframe
              srcDoc={previewTemplate?.html_body ?? ""}
              title="Template preview"
              className="h-[400px] w-full"
              sandbox=""
            />
          </div>
        </DialogContent>
      </Dialog>

      {/* Delete Confirmation Dialog */}
      <Dialog
        open={!!deleteConfirmId}
        onOpenChange={() => setDeleteConfirmId(null)}
      >
        <DialogContent className="max-w-sm border-white/10 bg-[#1E1E2E]">
          <DialogHeader>
            <DialogTitle className="text-white">Delete Template</DialogTitle>
          </DialogHeader>
          <p className="text-sm text-white/60">
            Are you sure you want to delete this template? It can be restored
            within 30 days.
          </p>
          <DialogFooter>
            <Button
              variant="ghost"
              onClick={() => setDeleteConfirmId(null)}
              className="text-white/60"
            >
              Cancel
            </Button>
            <Button
              variant="destructive"
              onClick={() => deleteConfirmId && deleteMutation.mutate(deleteConfirmId)}
            >
              Delete Template
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

"use client";

import { Search, X } from "lucide-react";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";

interface FilterDefinition {
  key: string;
  label: string;
  type: "select" | "date-range" | "toggle";
  options?: { value: string; label: string }[];
  value: unknown;
  onChange: (value: unknown) => void;
}

interface FilterBarProps {
  search?: { value: string; onChange: (v: string) => void; placeholder?: string };
  filters?: FilterDefinition[];
  onClear?: () => void;
}

export function FilterBar({ search, filters, onClear }: FilterBarProps) {
  const hasActiveFilters = filters?.some((f) => {
    if (f.type === "toggle") return f.value === true;
    if (f.type === "select") return f.value !== "" && f.value !== undefined;
    return false;
  });

  return (
    /* Hi-fi filter bar: flex, items-center, gap 10px, mb 16px, flex-wrap, padding 12px 16px, bg-surface (#f8fafc), border, rounded-md (8px) */
    <div className="hifi-filter-bar">
      {search && (
        <div className="relative min-w-[200px] flex-1 sm:max-w-xs">
          <Search className="absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          {/* Select/input: padding 6px 10px, border, rounded-sm (6px), 13px */}
          <Input
            value={search.value}
            onChange={(e) => search.onChange(e.target.value)}
            placeholder={search.placeholder ?? "Search..."}
            className="h-8 rounded-[6px] border-border bg-background px-[10px] py-[6px] pl-8 text-[13px] text-foreground placeholder:text-muted-foreground focus-visible:border-primary/50 focus-visible:ring-primary/20"
          />
        </div>
      )}

      {filters?.map((filter) => {
        if (filter.type === "select" && filter.options) {
          return (
            <div key={filter.key} className="flex items-center gap-1.5">
              {/* Label: 12px, font-weight 500, text-secondary, margin-right 6px */}
              <span className="text-xs font-medium text-muted-foreground mr-1.5">{filter.label}</span>
              <Select
                value={filter.value as string}
                onValueChange={(v) => filter.onChange(v)}
              >
                {/* Select: padding 6px 10px, border, rounded-sm (6px), 13px */}
                <SelectTrigger
                  size="sm"
                  className="rounded-[6px] border-border bg-background px-[10px] py-[6px] text-[13px] text-foreground hover:bg-muted"
                >
                  <SelectValue placeholder={filter.label} />
                </SelectTrigger>
                <SelectContent>
                  {filter.options.map((opt) => (
                    <SelectItem key={opt.value} value={opt.value}>
                      {opt.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          );
        }

        if (filter.type === "toggle") {
          return (
            <label
              key={filter.key}
              className="flex items-center gap-2 text-[13px] text-muted-foreground"
            >
              <Switch
                size="sm"
                checked={filter.value as boolean}
                onCheckedChange={(checked) => filter.onChange(checked)}
              />
              {filter.label}
            </label>
          );
        }

        return null;
      })}

      {/* Clear filters: 13px, text-primary, cursor pointer, margin-left auto, font-weight 500 */}
      {onClear && hasActiveFilters && (
        <button
          onClick={onClear}
          className="ml-auto flex items-center gap-1 text-[13px] font-medium text-primary cursor-pointer hover:text-primary/80 transition-colors"
        >
          <X className="h-3 w-3" />
          Clear Filters
        </button>
      )}
    </div>
  );
}

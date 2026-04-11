"use client";

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { HealthDot } from "@/components/shared/status-badge";
import type { SystemHealth } from "@/types";
import { Activity } from "lucide-react";

interface HealthStatusProps {
  health: SystemHealth;
}

export function HealthStatus({ health }: HealthStatusProps) {
  const services = health?.services ?? [];

  return (
    <Card className="border-border bg-card shadow-sm">
      <CardHeader className="pb-3">
        <CardTitle className="flex items-center gap-2 text-sm font-semibold text-foreground">
          <Activity className="h-4 w-4 text-primary" />
          System Health
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3 pt-0">
        {services.map((service) => (
          <div
            key={service.name}
            className="flex items-center justify-between rounded-lg bg-muted px-3 py-2"
          >
            <span className="text-sm text-foreground/70">{service.name}</span>
            <div className="flex items-center gap-3">
              {service.latencyMs !== undefined && (
                <span className="text-xs text-muted-foreground">
                  {service.latencyMs}ms
                </span>
              )}
              <HealthDot status={service.status} />
            </div>
          </div>
        ))}
      </CardContent>
    </Card>
  );
}

"use client";

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { HealthDot } from "@/components/shared/status-badge";
import type { SystemHealth } from "@/types";
import { Activity } from "lucide-react";

interface HealthStatusProps {
  health: SystemHealth;
}

export function HealthStatus({ health }: HealthStatusProps) {
  return (
    <Card className="border-white/10 bg-[#1E1E2E] shadow-none">
      <CardHeader className="pb-3">
        <CardTitle className="flex items-center gap-2 text-sm font-semibold text-white">
          <Activity className="h-4 w-4 text-[#7C4DFF]" />
          System Health
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3 pt-0">
        {health.services.map((service) => (
          <div
            key={service.name}
            className="flex items-center justify-between rounded-lg bg-white/[0.03] px-3 py-2"
          >
            <span className="text-sm text-white/70">{service.name}</span>
            <div className="flex items-center gap-3">
              {service.latency_ms !== undefined && (
                <span className="text-xs text-white/30">
                  {service.latency_ms}ms
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

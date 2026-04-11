"use client";

import { useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { PageHeader } from "@/components/shared/page-header";
import {
  StepIndicator,
  DomainStep,
  MxRecordsStep,
  WebhookStep,
  TestEmailStep,
  CompleteStep,
} from "@/components/inbound/setup-wizard";
import { useDomain } from "@/lib/hooks/use-domains";
import { Routes } from "@/lib/constants/routes";

const STEPS = ["Domain", "MX Records", "Webhook", "Test", "Complete"];

export default function InboundSetupPage() {
  const params = useParams();
  const router = useRouter();
  const domainId = params.id as string;

  const { data: domain } = useDomain(domainId !== "new" ? domainId : undefined);

  const [currentStep, setCurrentStep] = useState(0);
  const [selectedDomainId, setSelectedDomainId] = useState(domainId !== "new" ? domainId : "");
  const [webhookUrl, setWebhookUrl] = useState("");

  const domainName = domain?.domainName ?? "";

  const canNext =
    (currentStep === 0 && selectedDomainId !== "") ||
    currentStep === 1 ||
    (currentStep === 2 && webhookUrl !== "") ||
    currentStep === 3;

  function handleNext() {
    if (currentStep < STEPS.length - 1) {
      setCurrentStep((s) => s + 1);
    }
  }

  function handleBack() {
    if (currentStep > 0) {
      setCurrentStep((s) => s - 1);
    }
  }

  function handleFinish() {
    router.push(Routes.INBOUND_RULES);
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Inbound Email Setup"
        description="Configure your domain to receive inbound emails in 5 easy steps."
        backHref={Routes.DOMAINS}
        backLabel="Back to Domains"
      />

      <div className="mx-auto max-w-2xl">
        <StepIndicator steps={STEPS} currentStep={currentStep} />

        <div className="rounded-lg border border-border bg-card p-6">
          {currentStep === 0 && (
            <DomainStep
              domainId={selectedDomainId}
              onDomainChange={setSelectedDomainId}
            />
          )}
          {currentStep === 1 && <MxRecordsStep domain={domainName} />}
          {currentStep === 2 && (
            <WebhookStep
              webhookUrl={webhookUrl}
              onWebhookUrlChange={setWebhookUrl}
            />
          )}
          {currentStep === 3 && <TestEmailStep domain={domainName} />}
          {currentStep === 4 && <CompleteStep />}
        </div>

        {/* Navigation */}
        <div className="mt-6 flex items-center justify-between">
          <div>
            {currentStep > 0 && currentStep < STEPS.length - 1 && (
              <Button
                variant="outline"
                onClick={handleBack}
                className="border-border text-muted-foreground hover:bg-muted"
              >
                Back
              </Button>
            )}
          </div>

          <div className="flex items-center gap-3">
            {currentStep < STEPS.length - 1 && (
              <Button
                variant="ghost"
                size="sm"
                onClick={() => router.push(Routes.DOMAINS)}
                className="text-muted-foreground/60 hover:text-foreground"
              >
                Save & Continue Later
              </Button>
            )}

            {currentStep < STEPS.length - 1 ? (
              <Button onClick={handleNext} disabled={!canNext}>
                Next
              </Button>
            ) : (
              <Button onClick={handleFinish}>
                Go to Rules
              </Button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

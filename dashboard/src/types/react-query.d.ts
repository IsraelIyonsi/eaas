import "@tanstack/react-query";

declare module "@tanstack/react-query" {
  interface Register {
    mutationMeta: {
      /**
       * Set to true on a useMutation call to suppress the global error toast
       * fired by the MutationCache onError handler in providers/query-provider.tsx.
       * Use when the mutation handles its own error UI (inline form errors, custom toast wording).
       */
      suppressErrorToast?: boolean;
    };
  }
}

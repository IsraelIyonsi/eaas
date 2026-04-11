# Review Checklist

## Backend
- [ ] `dotnet build`: 0 errors, 0 warnings
- [ ] `dotnet test`: all pass
- [ ] New enum registered in BOTH AppDbContext AND DependencyInjection
- [ ] Status enums returned as strings, not integers
- [ ] JSON columns parsed before returning
- [ ] Routes from RouteConstants, tags from TagConstants
- [ ] `.WithName()` and `.WithOpenApi()` on every endpoint
- [ ] Handler tests: success + each failure case
- [ ] Validator tests: valid + each invalid field

## Frontend
- [ ] `npx next build`: 0 errors
- [ ] `npx playwright test`: all pass
- [ ] No hardcoded strings (routes, API paths, query keys, colors)
- [ ] `extractItems()` used for list data
- [ ] `safeConfigLookup()` for status display
- [ ] Loading, empty, error states handled
- [ ] `"use client"` on pages using hooks
- [ ] Constants referenced, not duplicated
- [ ] New page has Playwright test

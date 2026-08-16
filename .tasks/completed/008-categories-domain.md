# 008 – Categories Domain

## Status

`completed`

## Goal

Implement API contract rows #11–13: category tree, subcategories, soft delete, and protected
(system) category enforcement.

## Context

Fourth phase of the full Meezan backend implementation plan approved on 2026-08-07. Depends on
Phase 005/006. Full technical detail lives in the approved plan at
`C:\Users\lenovo\.claude\plans\we-are-starting-a-lively-pudding.md`.

Covers: BR-06; UC-09, UC-10, UC-11; API #11–13.

## Sub-Tasks

| #   | Description                                                                                                                          | Status     |
| --- | ------------------------------------------------------------------------------------------------------------------------------------- | ---------- |
| 1   | `CategoryDto` (tree-shaped), `CreateCategoryDto` + `CategoryMapper`                                                                  | ✅ done |
| 2   | `ICategoryRepository.GetTreeByKindAsync`                                                                                             | ✅ done |
| 3   | `ICategoryService`/`CategoryService`: `GetTree`, `Add` (max-depth-1 guard), `Update`/`Delete` (soft-delete-if-referenced, reject `IsProtected`) | ✅ done |
| 4   | `CategoryController`                                                                                                                 | ✅ done |
| 5   | Localization keys: `CategoryNotFound`, `SubcategoryParentMustBeTopLevel`, `CategoryDeleted`, `ProtectedCategoryCannotBeModified`     | ✅ done |
| 6   | DI registration, manual verification of depth-guard + dropdown soft-delete semantics                                                | ✅ done |

## Notes

- Sub-task 1 (2026-08-08): created `CategoryDto` (tree-shaped: `Id`/`Name`/`Kind`, nullable `Color`/`Icon`, `SortOrder`, `IsProtected`, `Children` list defaulted to `new()`), `CreateCategoryDto` (nullable `Kind`/`ParentId` — one DTO for both top-level and subcategory creation, matching the plan's "top-level: name+color+icon+kind; subcategory: parentId+name only"), and `UpdateCategoryDto` (`Id`/`Name`/`Color`/`Icon` — `Kind`/`ParentId` not editable after creation, not spec'd anywhere) in `Meezan.Dto/DTOs/Category/`, plus `CategoryMapper` (`Category → CategoryDto`; `Children` is populated by the service's tree-building logic, not Mapster). `UpdateCategoryDto` wasn't explicitly named in this sub-task's description but is added now since sub-task 3's `Update` needs a body shape — same bundling pattern as Phase 007's three Wallet DTOs. `dotnet build Meezan.sln` succeeds with 0 errors (99 pre-existing warnings, unrelated).
- Sub-task 2 (2026-08-08): added `ICategoryRepository.GetTreeByKindAsync(accountId, kind)` — flat, non-deleted, `SortOrder`-ordered list of both top-level and child categories for a kind; tree-building and color/icon inheritance resolution are the service's job (sub-task 3), not the repository's. `dotnet build Meezan.sln` succeeds with 0 errors (159 pre-existing warnings, unrelated).
- Sub-task 3 (2026-08-08): pulled forward the 4 localization keys (`CategoryNotFound`, `SubcategoryParentMustBeTopLevel`, `CategoryDeleted`, `ProtectedCategoryCannotBeModified`) from sub-task 5. **Cross-cutting refactor**: the "resolve caller's account by userId, 404 if missing" pattern was about to be duplicated a third time (already inline in `AccountService`, already private-helper'd in `WalletService`), so promoted it to `BaseService.GetAccountByUserIdAsync` and updated `WalletService` and `AccountService.GetByUser`/`UpdateSettings` to use it (rebuilt clean, no behavior change — `AccountService.Create` keeps its own `ParseUserId` since it deliberately must NOT 404 when no account exists yet). Added `ICategoryRepository.HasChildrenAsync` and `ITransactionRepository.ExistsForCategoryAsync` (mirrors Phase 007's `ExistsForWalletAsync`). Created `ICategoryService`/`CategoryService`: `GetTree` (builds the tree from the flat repository list; child `Color`/`Icon` resolved from the parent at read time, matching BR-06 §2 — never stored on the child row), `Add` (UC-09/UC-10: top-level requires `Kind`; subcategory requires an existing top-level `ParentId` — rejects with `SubcategoryParentMustBeTopLevel` if the target parent is itself a subcategory, the max-depth-1 guard; inherits the parent's `Kind`, leaves `Color`/`Icon` null), `Update`/`Delete` (422 `UnprocessableEntityException` when `IsProtected`; BR-06 soft-delete triggered by either a transaction reference **or** live children — the latter avoids violating the self-referencing FK's `Restrict` behavior from Phase 005 on a hard delete). DI registration added. `dotnet build Meezan.sln` succeeds with 0 errors (159 pre-existing warnings, unrelated).
- Sub-task 4 (2026-08-08): created `CategoryController` (`GET /api/categories?kind=`, `POST /api/categories`, `PUT/DELETE /api/categories/{id}`) — explicit `[Route("api/categories")]` (plural, same reasoning as `WalletController`), `[Authorize(AuthenticationSchemes = "Bearer")]` from the start. `dotnet build Meezan.sln` succeeds with 0 errors (93 pre-existing warnings, unrelated).
- Sub-task 5 (2026-08-08): verification only, no new code — confirmed all 4 keys are wired end-to-end and actually used via `Localization.*` in `CategoryService`, completed as part of sub-task 3's pull-forward.
- Sub-task 6 (2026-08-08): DI registration confirmed (already done in sub-task 3). Ran the API live and exercised the full matrix: `GET /api/categories?kind=` for both kinds (confirmed the 4/2 default categories from Phase 006 incl. `Zakat/Charity`/`isProtected:true`); `PUT`/`DELETE` on the protected category both blocked with 422; created a top-level category + a subcategory and confirmed **color/icon inheritance** at read time (child showed the parent's `#FF0000`/`bolt` despite storing `null`); attempted a sub-subcategory → 400 `SubcategoryParentMustBeTopLevel` (max-depth-1 guard); deleted a transaction-referenced subcategory → soft delete (verified via SQL: row present, `IsDeleted=1`) and confirmed dropdown-absence from the tree; deleted a parent category that still had a live child (no transaction reference) → **also correctly soft-deleted rather than hard-deleted**, verified via SQL this avoided violating the self-referencing FK's `Restrict` behavior; deleted a genuinely unreferenced, childless category → confirmed fully gone from the DB (hard delete); a second registered user got 404 `CategoryNotFound` trying to delete the first user's category (cross-account scoping). Cleaned up all test transaction rows afterward and stopped the app.
- **Phase 008 complete.** All 3 API contract rows (#11–13) implemented and live-verified.

## Approval Log

| Sub-Task | Approved By | Date       |
| -------- | ----------- | ---------- |
| Plan     | User        | 2026-08-07 |

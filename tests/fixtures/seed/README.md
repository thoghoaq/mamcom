# Seed fixture

A frozen copy of the dev seed (PLAN 1.8) that the tests read instead of the live `data/` folder: 33 ingredients, 20 dishes (all drafts), the plan lists and 5 FDC suggestions, as they were when ADR-018 was written.

**Why:** `data/` is the real data and changes whenever someone approves a dish or confirms an ingredient in Studio. Tests that assert "the seed has seven soups" or "no dish is reviewed" must not move with it. The live data is checked by `tests/MamCom.LiveData.Tests`.

**What stays live:** `data/schemas` and `data/config` are still copied from `data/`, so a change to a schema or a config value that breaks the fixture fails the tests that load it (for example `LoadingTests`). When that happens, update the fixture files here to the new shape; do not copy real data over them.

Do not put real nutrition numbers, real reviews or real photos in here. Numbers a test needs are test-only and marked as such in the test.

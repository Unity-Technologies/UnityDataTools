---
name: hybrid-duplication-audit
description: Audits asset duplication across the AssetBundle / ContentDirectory boundary in a hybrid Addressables 4.x build, where some groups build to .bundle files and others build through BuildPipeline.BuildContentDirectory. Use when asked to compare duplication in a hybrid project, find what's duplicated between bundles and the content directory, explain why Addressables' own DuplicatedAssetCount reads 0 or looks too low despite shared shaders, textures, or meshes across bundle and content-directory groups, or size the "hybrid tax" of assets baked twice because an AssetBundle cannot reference content-directory content. Runs one UnityDataTool analyze combining the Addressables build layout report with the content directory build output, then matches source-asset paths across both to find what's genuinely duplicated — including cases a plain CRC/hash diff misses, such as the same shader built with a different variant set on each side.
compatibility: Requires UnityDataTool (github.com/Unity-Technologies/UnityDataTools) built from a version with ContentLayout.json support, sqlite3 on PATH, and a completed hybrid Addressables 4.x build (at least one AssetBundle-producing group and one ContentDirectoryGroupSchema group).
---

# Hybrid duplication audit

## What this audits

A hybrid Addressables build produces two independent content builds
that only merge at the catalog: one for groups using
`BundledAssetGroupSchema` (AssetBundles), one for groups using
`ContentDirectoryGroupSchema` (a content directory, `BuildPipeline.
BuildContentDirectory`). An AssetBundle cannot reference content that
lives in a content directory, so any source asset both sides need
gets a full copy baked into the bundle. This skill finds and sizes
exactly that copy.

## Why Addressables' own report misses it

Addressables' build layout JSON has a top-level `DuplicatedAssetCount`,
but it only scans AssetBundle-to-AssetBundle duplication. A copy that
lives in the content directory is invisible to it, so it reads low or
zero even when real cross-boundary duplication exists.

UnityDataTool's own `view_potential_duplicates` (see
[analyzer.md](../../Documentation/analyzer.md)) doesn't fill that gap
either, because it matches by object CRC. A source asset built into
both forms doesn't necessarily produce byte-identical objects — a
shader, for example, gets its variants stripped independently on each
side, so the two copies can have different CRCs despite being the
same shader. Matching by **source asset path** instead of CRC catches
this; that's what this skill does.

## When to use this

- "Compare duplication in a hybrid project"
- "What's duplicated between the bundles and the content directory?"
- "Why does DuplicatedAssetCount say 0 when I have shared shaders?"
- Sizing the cost of moving a group from local (content directory) to
  remote (AssetBundle), or vice versa.

## Prerequisites

- A completed Addressables build containing at least one
  AssetBundle-producing group and one `ContentDirectoryGroupSchema`
  group.
- `UnityDataTool` built (`dotnet build -c Release` in this repo) —
  needs a version with `ContentLayout.json` ingestion
  (`content_layout_*` tables; check with
  `UnityDataTool --version`, or that `Documentation/contentlayout.md`
  exists in your checkout).
- `sqlite3` on PATH.

## Running it

Two equivalent scripts, same defaults, same queries, same output —
pick the one for your platform:

```powershell
# Windows
Skills/hybrid-duplication-audit/scripts/Compare-HybridDuplication.ps1 -ProjectRoot "<path to the Unity project>"
```

```bash
# Linux / macOS
Skills/hybrid-duplication-audit/scripts/compare-hybrid-duplication.sh --project-root "<path to the Unity project>"
```

By default either one locates everything it needs on its own: the
mirrored `Library/com.unity.addressables/buildlayout.json`
(Addressables keeps this in sync with its latest build), the one
content directory output folder under
`Library/com.unity.addressables/aa`, and `Library/BuildHistory`.
Override any of them with `-BuildLayout`/`--build-layout`,
`-ContentDirectory`/`--content-directory`, or
`-BuildHistory`/`--build-history` — useful for auditing an older
build, or when more than one platform has been built (the script
requires the content-directory override explicitly in that case,
since it can't guess which platform you mean).

**When overriding, make sure the build layout and content directory
came from the same build.** Addressables can rebuild only some groups
at a time, so the two can legitimately drift out of sync — nothing
ties an Addressables build layout to a specific content-directory
build the way `--build-history` ties a content directory to its own
`ContentLayout.json`. Pairing a stale report with a newer content
directory (or vice versa) won't error; it'll just report a wrong,
usually much larger, "duplicate" total. The defaults are always safe
in this respect, since both point at whatever is currently on disk.

Other flags (PowerShell / bash): `-ToolPath`/`--tool-path` (if
`UnityDataTool` isn't on PATH or set via `UNITYDATATOOL_PATH` — on
Linux/macOS the built executable has no extension, e.g.
`UnityDataTool/bin/Release/net9.0/UnityDataTool`),
`-KeepDatabase`/`--keep-database` (preserve the generated database for
follow-up queries), `-MaxRows`/`--max-rows`.

## Reading the output

- **Cross-boundary duplicates** — the source assets built into both
  forms, with the bytes they cost on the bundle side. Every row here
  is pure hybrid-layout tax: bytes that exist only because the two
  builds can't share.
- **Summary** — bundle-side and content-directory-side total payload,
  the duplicated total, and what percentage of the bundle payload it
  represents.
- **`DuplicatedAssetCount`** — printed for contrast, with the caveat
  above. Expect it to understate the real total, sometimes to zero.

## If it reports "this is not a hybrid build"

The build layout has no AssetBundle groups, or the content directory
has no source assets (or wasn't paired with its `ContentLayout.json`
— check that `-BuildHistory` points at the project's actual build
history folder). Both counts, printed in the error, need to be
non-zero for the audit to mean anything.

## Installing this skill into a Unity project

Claude Code doesn't discover skills living in an unrelated repo. Copy
or symlink this whole folder into the project you want to audit:

```
<YourUnityProject>/.claude/skills/hybrid-duplication-audit/
```

Once installed there, `-ToolPath` (or `UNITYDATATOOL_PATH`) will
usually be needed explicitly, since the script's own
UnityDataTools-checkout fallback won't resolve outside this repo.

## Going deeper

The core query, if you want to adapt it directly instead of running
the script:

```sql
WITH bundle_assets AS (
    SELECT asset_path, serialized_size + streamed_size AS bytes
    FROM addressables_build_explicit_assets
    UNION ALL
    SELECT asset_path, serialized_size + streamed_size AS bytes
    FROM addressables_build_data_from_other_assets
),
content_dir_assets AS (
    SELECT DISTINCT asset_path FROM content_layout_source_assets
)
SELECT b.asset_path, SUM(b.bytes) AS bundle_bytes, COUNT(*) AS instances
FROM bundle_assets b
WHERE b.asset_path IN (SELECT asset_path FROM content_dir_assets)
GROUP BY b.asset_path
ORDER BY bundle_bytes DESC;
```

Run with `-KeepDatabase` and query it directly with `sqlite3`, or see
[analyze-examples-contentlayout.md](../../Documentation/analyze-examples-contentlayout.md)
and [addressables-build-reports.md](../../Documentation/addressables-build-reports.md)
for the surrounding schema.
